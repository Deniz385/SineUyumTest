import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { useNotifications } from '../context/NotificationContext';
import { Box, Typography, List, ListItem, ListItemText, ListItemAvatar, Avatar, Divider } from '@mui/material';
import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutline';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';

export const MessagesPage = () => {
    const { t } = useTranslation();
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
            setError(t('messages.loadError'));
            console.error(err);
        } finally {
            setIsLoading(false);
        }
    }, [user, t]);

    useEffect(() => {
        fetchConversations();
    }, [fetchConversations]);

    // When a new message arrives (or the connection is re-established) the list
    // is refetched, so order, last message and read state come from one source.
    useEffect(() => {
        const unsubscribeMessages = subscribeToMessages(() => fetchConversations());
        const unsubscribeReconnect = subscribeToReconnect(fetchConversations);
        return () => {
            unsubscribeMessages();
            unsubscribeReconnect();
        };
    }, [subscribeToMessages, subscribeToReconnect, fetchConversations]);

    const retry = () => {
        setError('');
        setIsLoading(true);
        fetchConversations();
    };

    if (isLoading) return <LoadingState />;
    if (error) return <ErrorState message={error} onRetry={retry} />;

    return (
        <Box className="page-container" sx={{ maxWidth: '800px' }}>
            <Typography variant="h4" component="h1" gutterBottom>
                {t('messages.title')}
            </Typography>
            {conversations.length === 0 ? (
                <EmptyState icon={ChatBubbleOutlineIcon} title={t('messages.empty')} description={t('messages.emptyHint')} />
            ) : (
                <List sx={{ width: '100%', bgcolor: 'background.paper' }}>
                    {conversations.map((convo, index) => (
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
                    ))}
                </List>
            )}
        </Box>
    );
};
