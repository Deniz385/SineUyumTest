import axios from 'axios';

// API_URL'i .env dosyasından al. Eğer tanımlı değilse, localhost'u varsay.
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5074';

const api = axios.create({
    baseURL: API_URL
});

// Sunucuya yüklenen medya (ör. profil fotoğrafı) göreli yolla gelir
// ("/media/avatars/..."); tarayıcıya API adresiyle birlikte verilmeli.
// Eski kayıtlarda tam URL olabilir, o zaman olduğu gibi kullanılır.
export const mediaUrl = (url) => (url && url.startsWith('/') ? `${API_URL}${url}` : url || undefined);

// Token'ları tek yerden okuyup yazmak için yardımcılar.
// Access token artık kısa ömürlü (varsayılan 60 dk); süresi dolduğunda
// refresh token ile sessizce yenileniyor.
export const TOKEN_KEY = 'token';
export const REFRESH_TOKEN_KEY = 'refreshToken';

export const saveTokens = ({ token, refreshToken }) => {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    if (refreshToken) localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
};

export const clearTokens = () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
};

// Yenileme uçları Authorization başlığı istemez ve 401'de tekrar
// yenilenmeye çalışılmamalıdır.
const AUTH_PATHS = ['/api/v1/account/login', '/api/v1/account/register', '/api/v1/account/refresh'];
const isAuthPath = (url = '') => AUTH_PATHS.some((path) => url.startsWith(path));

// Aynı anda birden fazla istek 401 alırsa tek bir yenileme isteği atılır.
// (Sunucu refresh token rotation uyguladığı için ikinci istek geçersiz olurdu.)
let refreshPromise = null;

// Yeni access token döner (ve token'ları kaydeder); yenilenemezse null.
// Eşzamanlı çağrılar tek isteğe indirgenir (sunucu rotation uyguluyor).
export const refreshAccessTokenOnce = () => {
    refreshPromise = refreshPromise || refreshAccessToken().finally(() => {
        refreshPromise = null;
    });
    return refreshPromise;
};

const refreshAccessToken = async () => {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) return null;

    try {
        // Interceptor döngüsüne girmemek için ayrı bir axios örneği kullanılır.
        const response = await axios.post(`${API_URL}/api/v1/account/refresh`, { refreshToken });
        const { token, refreshToken: newRefreshToken } = response.data || {};
        if (!token || !newRefreshToken) return null;

        saveTokens({ token, refreshToken: newRefreshToken });
        return token;
    } catch {
        return null;
    }
};

// Interceptor'lar modül yüklenirken BİR KEZ kurulur. Önceden AuthContext'in
// useEffect'i içinde kuruluyordu:
//   * React çocuk bileşenlerin effect'lerini ebeveynden ÖNCE çalıştırdığı için
//     sayfa açılışındaki ilk istekler Authorization başlığı olmadan gidip 401
//     alıyor, ardından gereksiz bir token yenileme + tekrar deneme yapılıyordu
//     (refresh token yoksa kullanıcı her yenilemede çıkışa düşüyordu).
//   * effect her yeniden çalıştığında interceptor'lar üst üste ekleniyordu.
// AuthContext artık yalnızca geri çağırımları verir (setupInterceptors).
let onLogout = null;
let onTokenRefreshed = null;

// İstek gönderilmeden önce çalışır: token her istekte doğrudan localStorage'dan
// okunur (React state'inin gecikmelerinden etkilenmez).
api.interceptors.request.use(
    (config) => {
        if (!isAuthPath(config.url)) {
            const token = localStorage.getItem(TOKEN_KEY);
            if (token) {
                config.headers['Authorization'] = `Bearer ${token}`;
            }
        }
        return config;
    },
    (error) => Promise.reject(error)
);

// API'den yanıt geldikten sonra çalışır
api.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;
        const isUnauthorized = error.response?.status === 401;

        // 401 alındıysa önce refresh token ile yenilemeyi dene; başarılıysa
        // kullanıcı hiçbir şey fark etmeden istek tekrarlanır.
        if (isUnauthorized && originalRequest && !originalRequest._retried && !isAuthPath(originalRequest.url)) {
            originalRequest._retried = true;

            const newToken = await refreshAccessTokenOnce();

            if (newToken) {
                if (onTokenRefreshed) onTokenRefreshed(newToken);
                originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
                return api(originalRequest);
            }

            console.error("Oturum yenilenemedi. Çıkış yapılıyor.");
            if (onLogout) onLogout();
        }

        return Promise.reject(error);
    }
);

// AuthContext çıkış ve token yenilendi geri çağırımlarını buradan verir.
// Tekrar çağrılması güvenlidir; yalnızca geri çağırımlar güncellenir.
export const setupInterceptors = (logoutAction, tokenRefreshedAction) => {
    onLogout = logoutAction;
    onTokenRefreshed = tokenRefreshedAction;
};

export default api;
