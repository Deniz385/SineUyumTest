import React, { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from './AuthContext';
import api, { API_URL } from '../api/axiosConfig';
import { useSnackbar } from './SnackbarProvider';

const NotificationContext = createContext(null);

export const useNotifications = () => {
    return useContext(NotificationContext);
};

// Uygulamada TEK SignalR bağlantısı vardır ve burada yaşar. İki olay taşır:
//   ReceiveNotification → bildirim listesi
//   ReceiveMessage      → canlı sohbet (subscribeToMessages ile dinlenir)
//   MessagesRead        → "Görüldü" (subscribeToReads ile dinlenir)
export const NotificationProvider = ({ children }) => {
    const [notifications, setNotifications] = useState([]);
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar();

    const messageListeners = useRef(new Set());
    const reconnectListeners = useRef(new Set());
    const readListeners = useRef(new Set());
    // Şu an açık olan sohbetin karşı kullanıcısı: ondan gelen mesaj bildirimi
    // gösterilmez, doğrudan okundu işaretlenir (mesaj zaten ekranda).
    const activeConversation = useRef(null);
    const notificationsRef = useRef(notifications);
    notificationsRef.current = notifications;

    const fetchNotifications = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get('/api/v1/notification');
            setNotifications(response.data);
        } catch (error) {
            console.error('Bildirimler alınamadı:', error);
        }
    }, [user]);

    const markAsRead = useCallback(async (notificationId) => {
        setNotifications(prev => prev.map(n => (n.id === notificationId ? { ...n, isRead: true } : n)));
        try {
            await api.post(`/api/v1/notification/${notificationId}/mark-as-read`);
        } catch (error) {
            console.error('Bildirim okundu olarak işaretlenemedi:', error);
        }
    }, []);

    useEffect(() => {
        if (!user) return undefined;

        let cancelled = false;
        let connection = null;

        // Dinleyiciler start'tan ÖNCE kaydedilir; aksi halde bağlantı kurulduğu
        // an gelen olaylar kaybolurdu.
        const subscribe = (hub) => {
            hub.on('ReceiveNotification', (notification) => {
                const active = activeConversation.current;
                if (active && notification.relatedUrl === `/messages/${active}`) {
                    setNotifications(prev => [{ ...notification, isRead: true }, ...prev]);
                    api.post(`/api/v1/notification/${notification.id}/mark-as-read`).catch(() => {});
                    return;
                }
                showSnackbar(notification.message, 'info');
                setNotifications(prev => [notification, ...prev]);
            });

            hub.on('ReceiveMessage', (message) => {
                messageListeners.current.forEach(listener => listener(message));
            });

            hub.on('MessagesRead', (receipt) => {
                readListeners.current.forEach(listener => listener(receipt));
            });

            // Bağlantı koptuğu sürede gelenler kaçmış olabilir: yeniden kurulunca
            // bildirimler ve açık ekranlar (sohbet) sunucudan tazelenir.
            hub.onreconnected(() => {
                fetchNotifications();
                reconnectListeners.current.forEach(listener => listener());
            });
        };

        // SignalR kütüphanesi yalnızca giriş yapılınca yüklenir (M13); giriş
        // ekranının paketine girmez.
        import('@microsoft/signalr')
            .then((signalR) => {
                if (cancelled) return;
                connection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_URL}/notificationHub`, {
                        accessTokenFactory: () => localStorage.getItem('token')
                    })
                    .withAutomaticReconnect()
                    .build();
                subscribe(connection);
                return connection.start().then(() => { if (!cancelled) fetchNotifications(); });
            })
            .catch(e => { if (!cancelled) console.error('SignalR bağlantı hatası: ', e); });

        // Her durumda durdurulur. Önceden yalnızca "Connected" iken durduruluyordu:
        // bağlanırken bileşen kaldırılırsa (React Strict Mode, kullanıcı değişimi)
        // eski bağlantı yaşamaya devam ediyor ve her olay iki kez geliyordu.
        return () => {
            cancelled = true;
            connection?.stop();
        };
    }, [user, fetchNotifications, showSnackbar]);

    const markAllAsRead = async () => {
        try {
            await api.post('/api/v1/notification/mark-all-as-read');
            setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
        } catch (error) {
            console.error('Tüm bildirimler okundu olarak işaretlenemedi:', error);
        }
    };

    // Dinleyici ekler; dönen fonksiyon kaldırır (useEffect temizliğinde çağrılır).
    const subscribeToMessages = useCallback((listener) => {
        messageListeners.current.add(listener);
        return () => messageListeners.current.delete(listener);
    }, []);

    const subscribeToReads = useCallback((listener) => {
        readListeners.current.add(listener);
        return () => readListeners.current.delete(listener);
    }, []);

    const subscribeToReconnect = useCallback((listener) => {
        reconnectListeners.current.add(listener);
        return () => reconnectListeners.current.delete(listener);
    }, []);

    // Sohbet ekranı açılınca/kapanınca çağrılır. Açılırken o sohbetin okunmamış
    // bildirimleri de okundu işaretlenir.
    const setActiveConversation = useCallback((otherUserId) => {
        activeConversation.current = otherUserId;
        if (!otherUserId) return;
        notificationsRef.current
            .filter(n => !n.isRead && n.relatedUrl === `/messages/${otherUserId}`)
            .forEach(n => markAsRead(n.id));
    }, [markAsRead]);

    const value = {
        notifications,
        unreadCount: notifications.filter(n => !n.isRead).length,
        markAsRead,
        markAllAsRead,
        subscribeToMessages,
        subscribeToReads,
        subscribeToReconnect,
        setActiveConversation,
    };

    return (
        <NotificationContext.Provider value={value}>
            {children}
        </NotificationContext.Provider>
    );
};
