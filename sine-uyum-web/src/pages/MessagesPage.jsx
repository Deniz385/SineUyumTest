import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { useNotifications } from '../context/NotificationContext';
import { Box, Typography, CircularProgress, List, ListItem, ListItemText, ListItemAvatar, Avatar, Divider } from '@mui/material';

export const MessagesPage = () => {
    const { user } = useAuth();
    const [conversations, setConversations] = useState([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const { subscribeToMessages, subscribeToReconnect } = useNotifications();

    const fetchConversations = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get(`/api/v1/messages`);
            setConversations(response.data);
            setError('');
        } catch (err) {
            setError('Konuşmalar yüklenirken bir hata oluştu.');
            console.error(err);
        } finally {
            setIsLoading(false);
        }
    }, [user]);

    useEffect(() => {
        fetchConversations();
    }, [fetchConversations]);

    // Yeni mesaj gelince (ya da bağlantı yeniden kurulunca) liste sunucudan
    // tazelenir: sıra, son mesaj ve okunma durumu tek kaynaktan gelsin.
    useEffect(() => {
        const unsubscribeMessages = subscribeToMessages(() => fetchConversations());
        const unsubscribeReconnect = subscribeToReconnect(fetchConversations);
        return () => {
            unsubscribeMessages();
            unsubscribeReconnect();
        };
    }, [subscribeToMessages, subscribeToReconnect, fetchConversations]);

    if (isLoading) return <CircularProgress />;
    if (error) return <Typography color="error">{error}</Typography>;

    return (
        <Box className="page-container" sx={{ maxWidth: '800px' }}>
            <Typography variant="h4" component="h1" gutterBottom>
                Mesajlar
            </Typography>
            <List sx={{ width: '100%', bgcolor: 'background.paper' }}>
                {conversations.length === 0 ? (
                    <ListItem>
                        <ListItemText primary="Hiç konuşmanız yok." />
                    </ListItem>
                ) : (
                    conversations.map((convo, index) => (
                        <React.Fragment key={convo.otherUserId}>
                            <ListItem 
                                alignItems="flex-start" 
                                component={Link} 
                                to={`/messages/${convo.otherUserId}`}
                                sx={{ textDecoration: 'none', color: 'inherit' }}
                            >
                                <ListItemAvatar>
                                    <Avatar src={mediaUrl(convo.otherUserProfileImageUrl)}>
                                        {convo.otherUserUsername?.charAt(0).toUpperCase()}
                                    </Avatar>
                                </ListItemAvatar>
                                <ListItemText
                                    primary={convo.otherUserUsername}
                                    secondary={
                                        <Typography
                                            sx={{ display: 'inline' }}
                                            component="span"
                                            variant="body2"
                                            color="text.primary"
                                        >
                                            {convo.lastMessageContent.length > 100
                                                ? `${convo.lastMessageContent.substring(0, 100)}…`
                                                : convo.lastMessageContent}
                                        </Typography>
                                    }
                                />
                            </ListItem>
                            {index < conversations.length - 1 && <Divider variant="inset" component="li" />}
                        </React.Fragment>
                    ))
                )}
            </List>
        </Box>
    );
};
