import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';
import api, { API_URL } from '../api/axiosConfig';
import { useSnackbar } from './SnackbarProvider';

const NotificationContext = createContext(null);

export const useNotifications = () => {
    return useContext(NotificationContext);
};

export const NotificationProvider = ({ children }) => {
    const [notifications, setNotifications] = useState([]);
    const [unreadCount, setUnreadCount] = useState(0);
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar();

    const fetchNotifications = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get('/api/notification');
            const data = response.data;
            setNotifications(data);
            setUnreadCount(data.filter(n => !n.isRead).length);
        } catch (error) {
            console.error('Bildirimler alınamadı:', error);
        }
    }, [user]);

    // --- DÜZELTME BAŞLANGICI ---
    // SignalR bağlantı mantığını daha sağlam hale getiriyoruz.
    useEffect(() => {
        // Sadece kullanıcı giriş yapmışsa bir bağlantı oluştur.
        if (user) {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl(`${API_URL}/notificationHub`, {
                    accessTokenFactory: () => localStorage.getItem('token')
                })
                .withAutomaticReconnect()
                .build();

            // Sadece bağlantı durumu "Disconnected" ise başlat.
            // Bu, React Strict Mode'un neden olduğu çift render sorununu çözer.
            if (connection.state === signalR.HubConnectionState.Disconnected) {
                connection.start()
                    .then(() => {
                        console.log('SignalR bağlantısı kuruldu.');
                        fetchNotifications(); 

                        connection.on('ReceiveNotification', (notification) => {
                            showSnackbar(notification.message, 'info');
                            setNotifications(prev => [notification, ...prev]);
                            setUnreadCount(prev => prev + 1);
                        });
                    })
                    .catch(e => console.error('SignalR bağlantı hatası: ', e));
            }
            
            // Temizleme fonksiyonu: Bileşen kaldırıldığında çalışır.
            return () => {
                // Sadece bağlantı "Connected" durumundaysa durdur.
                if (connection.state === signalR.HubConnectionState.Connected) {
                    connection.stop();
                }
            };
        }
    }, [user, fetchNotifications, showSnackbar]);
    // --- DÜZELTME BİTİŞİ ---

    const markAsRead = async (notificationId) => {
        try {
            await api.post(`/api/notification/${notificationId}/mark-as-read`);
            setNotifications(prev =>
                prev.map(n => (n.id === notificationId ? { ...n, isRead: true } : n))
            );
            setUnreadCount(prev => Math.max(0, prev - 1));
        } catch (error) {
            console.error('Bildirim okundu olarak işaretlenemedi:', error);
        }
    };
    
    const markAllAsRead = async () => {
        try {
            await api.post('/api/notification/mark-all-as-read');
            setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
            setUnreadCount(0);
        } catch (error) {
            console.error('Tüm bildirimler okundu olarak işaretlenemedi:', error);
        }
    };

    const value = {
        notifications,
        unreadCount,
        markAsRead,
        markAllAsRead,
    };

    return (
        <NotificationContext.Provider value={value}>
            {children}
        </NotificationContext.Provider>
    );
};