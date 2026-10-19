import axios from 'axios';

// API_URL'i .env dosyasından al. Eğer tanımlı değilse, localhost'u varsay.
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5074';

const api = axios.create({
    baseURL: API_URL
});

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
const AUTH_PATHS = ['/api/account/login', '/api/account/register', '/api/account/refresh'];
const isAuthPath = (url = '') => AUTH_PATHS.some((path) => url.startsWith(path));

// Aynı anda birden fazla istek 401 alırsa tek bir yenileme isteği atılır.
// (Sunucu refresh token rotation uyguladığı için ikinci istek geçersiz olurdu.)
let refreshPromise = null;

const refreshAccessToken = async () => {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) return null;

    try {
        // Interceptor döngüsüne girmemek için ayrı bir axios örneği kullanılır.
        const response = await axios.post(`${API_URL}/api/account/refresh`, { refreshToken });
        const { token, refreshToken: newRefreshToken } = response.data || {};
        if (!token || !newRefreshToken) return null;

        saveTokens({ token, refreshToken: newRefreshToken });
        return token;
    } catch {
        return null;
    }
};

// Bu fonksiyon, AuthContext'ten aldığı logoutAction ile interceptor'ları kurar.
export const setupInterceptors = (logoutAction, onTokenRefreshed) => {

    // İstek gönderilmeden önce çalışır
    api.interceptors.request.use(
        (config) => {
            // Her istekte token'ı doğrudan localStorage'dan okur ve başlığa ekler.
            // Bu, React state'inin gecikmelerinden etkilenmemesini sağlar.
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

                refreshPromise = refreshPromise || refreshAccessToken().finally(() => {
                    refreshPromise = null;
                });

                const newToken = await refreshPromise;

                if (newToken) {
                    if (onTokenRefreshed) onTokenRefreshed(newToken);
                    originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
                    return api(originalRequest);
                }

                console.error("Oturum yenilenemedi. Çıkış yapılıyor.");
                if (logoutAction) logoutAction();
            }

            return Promise.reject(error);
        }
    );
};

export default api;
