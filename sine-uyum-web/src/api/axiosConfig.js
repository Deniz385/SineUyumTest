// src/api/axiosConfig.js
import axios from 'axios';

export const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

// Yeniden kullanılabilir bir axios instance oluşturuyoruz
const api = axios.create({
    baseURL: API_URL
});

// Bu fonksiyon, interceptor'ı kuracak
export const setupInterceptors = (authContext) => {
    
    // İstek Interceptor'ı: Her istek gönderilmeden önce token'ı başlığa ekler
    api.interceptors.request.use(
        (config) => {
            const token = authContext.token;
            if (token) {
                config.headers['Authorization'] = `Bearer ${token}`;
            }
            return config;
        },
        (error) => {
            return Promise.reject(error);
        }
    );

    // Yanıt Interceptor'ı: Her yanıttan sonra çalışır
    api.interceptors.response.use(
        (response) => {
            // Başarılı bir yanıt gelirse, hiçbir şey yapma
            return response;
        },
        (error) => {
            // Eğer 401 (Unauthorized) hatası gelirse...
            if (error.response && error.response.status === 401) {
                console.log("Token süresi doldu veya geçersiz. Çıkış yapılıyor...");
                // AuthContext'ten gelen logout fonksiyonunu çağır
                authContext.logoutAction();
            }
            return Promise.reject(error);
        }
    );
};

export default api;