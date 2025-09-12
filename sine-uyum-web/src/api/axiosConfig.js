import axios from 'axios';

export const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

const api = axios.create({
    baseURL: API_URL
});

// Bu fonksiyon, AuthContext'ten aldığı logoutAction ile interceptor'ları kurar.
export const setupInterceptors = (logoutAction) => {
    
    // İstek gönderilmeden önce çalışır
    api.interceptors.request.use(
        (config) => {
            // Her istekte token'ı doğrudan localStorage'dan okur ve başlığa ekler.
            // Bu, React state'inin gecikmelerinden etkilenmemesini sağlar.
            const token = localStorage.getItem('token');
            if (token) {
                config.headers['Authorization'] = `Bearer ${token}`;
            }
            return config;
        },
        (error) => Promise.reject(error)
    );

    // API'den yanıt geldikten sonra çalışır
    api.interceptors.response.use(
        (response) => response,
        (error) => {
            // Eğer 401 (Yetkisiz) hatası alınırsa, bu token'ın geçersiz olduğu anlamına gelir.
            if (error.response && error.response.status === 401) {
                console.error("Yetkisiz istek (401). Token geçersiz veya süresi dolmuş. Çıkış yapılıyor.");
                // logoutAction'ı çağırarak kullanıcıyı temizle ve giriş sayfasına yönlendir.
                if (logoutAction) logoutAction();
            }
            return Promise.reject(error);
        }
    );
};

export default api;