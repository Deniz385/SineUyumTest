import axios from 'axios';

// API_URL'i .env dosyasından al. Eğer tanımlı değilse, localhost'u varsay.
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5074';

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
