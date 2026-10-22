import React, { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from './AuthContext';
import api, { API_URL } from '../api/axiosConfig';
import { useSnackbar } from './SnackbarProvider';
import { useTranslation } from 'react-i18next';

const NotificationContext = createContext(null);

export const useNotifications = () => {
    return useContext(NotificationContext);
};

// The app has a SINGLE SignalR connection and it lives here. It carries:
//   ReceiveNotification → notification list
//   ReceiveMessage      → live chat (listened to via subscribeToMessages)
//   MessagesRead        → "Seen" receipts (listened to via subscribeToReads)
export const NotificationProvider = ({ children }) => {
    const [notifications, setNotifications] = useState([]);
    // The last load failed: the notification menu offers "Try again".
    const [loadError, setLoadError] = useState(false);
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar();
    const { i18n } = useTranslation();
    const language = i18n.language;

    const messageListeners = useRef(new Set());
    const reconnectListeners = useRef(new Set());
    const readListeners = useRef(new Set());
    // The other user of the conversation currently open: message notifications
    // from them are not shown but marked as read right away (the message is
    // already on screen).
    const activeConversation = useRef(null);
    const notificationsRef = useRef(notifications);
    notificationsRef.current = notifications;

    const fetchNotifications = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get('/api/v1/notification');
            setNotifications(response.data);
            setLoadError(false);
        } catch (error) {
            setLoadError(true);
            console.error('Could not load notifications:', error);
        }
    }, [user]);

    const markAsRead = useCallback(async (notificationId) => {
        setNotifications(prev => prev.map(n => (n.id === notificationId ? { ...n, isRead: true } : n)));
        try {
            await api.post(`/api/v1/notification/${notificationId}/mark-as-read`);
        } catch (error) {
            console.error('Could not mark the notification as read:', error);
        }
    }, []);

    useEffect(() => {
        if (!user) return undefined;

        let cancelled = false;
        let connection = null;

        // Handlers are registered BEFORE start; otherwise events arriving the
        // moment the connection opens would be lost.
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

            // Events may have been missed while disconnected: after reconnecting,
            // notifications and open screens (chat) are refetched from the server.
            hub.onreconnected(() => {
                fetchNotifications();
                reconnectListeners.current.forEach(listener => listener());
            });
        };

        // The SignalR library is only loaded after login (M13), so it stays out
        // of the login screen's bundle.
        // The hub gets the UI language as ?lang=; a language change opens a new
        // connection. Push texts are rendered in the user's stored language.
        import('@microsoft/signalr')
            .then((signalR) => {
                if (cancelled) return;
                connection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_URL}/notificationHub?lang=${encodeURIComponent(language)}`, {
                        accessTokenFactory: () => localStorage.getItem('token')
                    })
                    .withAutomaticReconnect()
                    .build();
                subscribe(connection);
                return connection.start().then(() => { if (!cancelled) fetchNotifications(); });
            })
            .catch(e => { if (!cancelled) console.error('SignalR connection error: ', e); });

        // Always stopped. It used to be stopped only while "Connected": if the
        // component unmounted mid-connect (React Strict Mode, user switch) the
        // old connection stayed alive and every event arrived twice.
        return () => {
            cancelled = true;
            connection?.stop();
        };
    }, [user, language, fetchNotifications, showSnackbar]);

    const markAllAsRead = async () => {
        try {
            await api.post('/api/v1/notification/mark-all-as-read');
            setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
        } catch (error) {
            console.error('Could not mark all notifications as read:', error);
        }
    };

    // Adds a listener; the returned function removes it (call it in useEffect cleanup).
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

    // Called when a chat screen opens/closes. On open, that conversation's
    // unread notifications are marked as read too.
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
        notificationsLoadError: loadError,
        reloadNotifications: fetchNotifications,
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
