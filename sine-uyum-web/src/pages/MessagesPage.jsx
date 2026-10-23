import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { useNotifications } from '../context/NotificationContext';
import { Box, Card, Typography } from '@mui/material';
import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutlineRounded';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { Page, PageHeader } from '../components/ui/Page';
import { UserAvatar } from '../components/ui/UserRow';
import { UserListSkeleton } from '../components/ui/Skeletons';
import { timeSince } from '../components/navigation';
import { srOnly } from '../components/ui/a11y';

export const MessagesPage = () => {
    const { t, i18n } = useTranslation();
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

    return (
        <Page width="reading">
            <PageHeader title={t('messages.title')} />
            {isLoading ? (
                <UserListSkeleton count={5} />
            ) : error ? (
                <ErrorState message={error} onRetry={retry} />
            ) : conversations.length === 0 ? (
                <EmptyState icon={ChatBubbleOutlineIcon} title={t('messages.empty')} description={t('messages.emptyHint')} />
            ) : (
                <Card component="ul" data-testid="conversations" sx={{ listStyle: 'none', m: 0, p: 0.75 }}>
                    {conversations.map((convo) => {
                        const unread = convo.isRead === false;
                        return (
                            <li key={convo.otherUserId}>
                                <Box
                                    component={Link}
                                    to={`/messages/${convo.otherUserId}`}
                                    sx={{ display: 'flex', gap: 1.5, alignItems: 'center', p: 1.25, borderRadius: 1.5, color: 'inherit', '&:hover': { bgcolor: 'action.hover' } }}
                                >
                                    <UserAvatar user={{ userName: convo.otherUserUsername, profileImageUrl: convo.otherUserProfileImageUrl }} size={48} />
                                    <Box sx={{ flex: 1, minWidth: 0 }}>
                                        <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
                                            <Typography variant="subtitle1" component="span" noWrap sx={{ flex: 1, minWidth: 0 }}>@{convo.otherUserUsername}</Typography>
                                            {convo.lastMessageSent && (
                                                <Typography variant="body2" component="span" color="text.secondary" sx={{ flexShrink: 0 }}>
                                                    {timeSince(convo.lastMessageSent, i18n.language, t('notifications.justNow'))}
                                                </Typography>
                                            )}
                                        </Box>
                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                            <Typography variant="body1" noWrap sx={{ flex: 1, minWidth: 0, color: unread ? 'text.primary' : 'text.secondary', fontWeight: unread ? 600 : 400 }}>
                                                {convo.lastMessageContent}
                                            </Typography>
                                            {unread && (
                                                <>
                                                    <Box aria-hidden="true" sx={{ width: 10, height: 10, borderRadius: '50%', bgcolor: 'primary.main', flexShrink: 0 }} />
                                                    <Box component="span" sx={srOnly}>{t('messages.unread')}</Box>
                                                </>
                                            )}
                                        </Box>
                                    </Box>
                                </Box>
                            </li>
                        );
                    })}
                </Card>
            )}
        </Page>
    );
};
