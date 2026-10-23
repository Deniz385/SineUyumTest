import React, { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { useNotifications } from '../context/NotificationContext';
import { useSnackbar } from '../context/SnackbarProvider';
import { Box, Typography, TextField, IconButton, Dialog, DialogTitle, DialogContent, Card, List, ListItem, ListItemButton, ListItemText, Tooltip } from '@mui/material';
import SendIcon from '@mui/icons-material/SendRounded';
import MovieRoundedIcon from '@mui/icons-material/MovieRounded';
import PlaylistAddIcon from '@mui/icons-material/PlaylistAddRounded';
import PlaylistPlayRoundedIcon from '@mui/icons-material/PlaylistPlayRounded';
import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutlineRounded';
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded';
import { MovieSearchBar } from '../components/MovieSearchBar';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { Page } from '../components/ui/Page';
import { PosterImage } from '../components/ui/PosterImage';
import { UserAvatar } from '../components/ui/UserRow';
import { FeedSkeleton, UserListSkeleton } from '../components/ui/Skeletons';
import { apiErrorMessage } from '../components/ui/apiError';
import { prefersReducedMotion } from '../components/ui/a11y';

const MovieRecommendationCard = ({ movie }) => {
    const { t } = useTranslation();
    return (
        <Card sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1, pr: 2, textDecoration: 'none', bgcolor: 'surface.raised', '&:hover': { bgcolor: 'surface.overlay' } }} component={Link} to={`/movie/${movie.id}`}>
            <Box sx={{ width: 48, flexShrink: 0 }}>
                <PosterImage path={movie.posterPath} title={movie.title} width={48} radius="sm" decorative />
            </Box>
            <Box sx={{ minWidth: 0 }}>
                <Typography variant="body2" color="text.secondary">{t('conversation.movieSuggestion')}</Typography>
                <Typography variant="title" component="p" sx={{ color: 'text.primary' }}>{movie.title}</Typography>
            </Box>
        </Card>
    );
};

const WatchlistShareCard = ({ watchlist }) => {
    const { t } = useTranslation();
    return (
        <Card sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1, pr: 2, textDecoration: 'none', bgcolor: 'surface.raised', '&:hover': { bgcolor: 'surface.overlay' } }} component={Link} to={`/watchlist/${watchlist.id}`}>
            <Box aria-hidden="true" sx={{ width: 48, height: 48, borderRadius: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'surface.overlay', color: 'primary.main', flexShrink: 0 }}>
                <PlaylistPlayRoundedIcon />
            </Box>
            <Box sx={{ minWidth: 0 }}>
                <Typography variant="body2" color="text.secondary">{t('conversation.watchlist')}</Typography>
                <Typography variant="title" component="p" sx={{ color: 'text.primary' }}>{watchlist.name}</Typography>
                <Typography variant="body2" color="text.secondary">@{watchlist.ownerUsername}</Typography>
            </Box>
        </Card>
    );
};

export const ConversationPage = () => {
    const { t, i18n } = useTranslation();
    const { otherUserId } = useParams();
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar();
    const { subscribeToMessages, subscribeToReads, subscribeToReconnect, setActiveConversation } = useNotifications();
    const [messages, setMessages] = useState([]);
    const [otherUser, setOtherUser] = useState(null);
    // Can be opened with a prefilled draft (?draft=), e.g. from "Ask them" on a profile.
    const [searchParams] = useSearchParams();
    const [newMessage, setNewMessage] = useState(() => searchParams.get('draft') ?? '');
    const [isLoading, setIsLoading] = useState(true);
    const [loadError, setLoadError] = useState(false);
    const [isMovieModalOpen, setIsMovieModalOpen] = useState(false);
    const [isWatchlistModalOpen, setIsWatchlistModalOpen] = useState(false);
    // null while loading; listsError when they could not be loaded.
    const [userLists, setUserLists] = useState(null);
    const [listsError, setListsError] = useState(false);
    const messagesEndRef = useRef(null);

    const scrollToBottom = () => {
        messagesEndRef.current?.scrollIntoView({ behavior: prefersReducedMotion() ? "auto" : "smooth", block: "end" });
    };

    const fetchThread = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get(`/api/v1/messages/thread/${otherUserId}`);
            const messageData = response.data || [];
            setMessages(messageData);

            if (messageData.length > 0) {
                const firstMessage = messageData[0];
                setOtherUser(firstMessage.senderId === user.id ? 
                    { id: firstMessage.recipientId, userName: firstMessage.recipientUsername } :
                    { id: firstMessage.senderId, userName: firstMessage.senderUsername }
                );
            } else {
                const profileResponse = await api.get(`/api/v1/profile/${otherUserId}`);
                setOtherUser(profileResponse.data);
            }
            setLoadError(false);
        } catch (err) {
            setLoadError(true);
            console.error("Could not load messages:", err);
        } finally {
            setIsLoading(false);
        }
    }, [otherUserId, user]);

    useEffect(() => {
        fetchThread();
    }, [fetchThread]);

    // The same message can arrive both in the send response and as a live event
    // (it is pushed to the sender too), so messages are de-duplicated by id.
    const appendMessage = useCallback((message) => {
        setMessages(prev => (prev.some(m => m.id === message.id) ? prev : [...prev, message]));
    }, []);

    const markThreadAsRead = useCallback(() => {
        api.post(`/api/v1/messages/thread/${otherUserId}/mark-as-read`).catch(() => {});
    }, [otherUserId]);

    // Live chat (M11): messages belonging to this conversation are appended
    // instantly; ones from the other user count as read right away since the
    // screen is open. After a reconnect the thread is refetched to catch
    // anything missed while disconnected.
    useEffect(() => {
        if (!user) return undefined;
        setActiveConversation(otherUserId);
        markThreadAsRead();

        const belongsHere = (m) =>
            (m.senderId === otherUserId && m.recipientId === user.id) ||
            (m.senderId === user.id && m.recipientId === otherUserId);

        const unsubscribeMessages = subscribeToMessages((message) => {
            if (!belongsHere(message)) return;
            appendMessage(message);
            if (message.senderId === otherUserId) markThreadAsRead();
        });
        // The other user has read the thread (M10): my messages up to that point are "Seen".
        const unsubscribeReads = subscribeToReads((receipt) => {
            if (receipt.readerId !== otherUserId || receipt.otherUserId !== user.id) return;
            setMessages(prev => prev.map(m => (
                m.senderId === user.id && !m.dateRead && m.id <= receipt.lastReadMessageId
                    ? { ...m, dateRead: receipt.readAt }
                    : m
            )));
        });
        const unsubscribeReconnect = subscribeToReconnect(fetchThread);

        return () => {
            unsubscribeMessages();
            unsubscribeReads();
            unsubscribeReconnect();
            setActiveConversation(null);
        };
    }, [user, otherUserId, subscribeToMessages, subscribeToReads, subscribeToReconnect, setActiveConversation,
        appendMessage, markThreadAsRead, fetchThread]);

    useEffect(scrollToBottom, [messages]);

    // "Seen" appears only under my last read message, not under every one.
    const lastSeenId = messages.reduce(
        (max, m) => (m.senderId === user?.id && m.dateRead && m.id > max ? m.id : max), 0);

    const handleSendMessage = async ({ content, movieId, watchlistId }) => {
        if (!content && !movieId && !watchlistId) return;
        try {
            const response = await api.post(`/api/v1/messages`, { recipientId: otherUserId, content, movieId, watchlistId });
            setNewMessage('');
            // The response is the created message itself; no need to refetch the thread.
            appendMessage(response.data);
        } catch (err) {
            // On a privacy/block rejection (403) the server's reason is shown.
            showSnackbar(apiErrorMessage(err, t('conversation.sendFailed')), 'error');
        }
    };
    
    const handleMovieSelectAndSend = async (movie) => {
        setIsMovieModalOpen(false);
        try {
            await api.post(`/api/v1/ratings/addmovie`, { id: movie.id, title: movie.title, posterPath: movie.poster_path });
        } catch (addMovieError) {
            if (addMovieError.response && addMovieError.response.status !== 400) {
               console.error("Could not add the movie:", addMovieError);
               return;
            }
        }
        handleSendMessage({ movieId: movie.id });
    };

    const loadUserLists = async () => {
        setUserLists(null);
        setListsError(false);
        try {
            const response = await api.get(`/api/v1/watchlist`);
            setUserLists(response.data || []);
        } catch (err) {
            setListsError(true);
            console.error("Could not load lists:", err);
        }
    };

    const handleOpenWatchlistModal = () => {
        setIsWatchlistModalOpen(true);
        loadUserLists();
    };

    const handleListSelectAndSend = (listId) => {
        setIsWatchlistModalOpen(false);
        handleSendMessage({ watchlistId: listId });
    };

    const handleTextSubmit = (e) => {
        e.preventDefault();
        if (!newMessage.trim()) return;
        handleSendMessage({ content: newMessage.trim() });
    };

    const retry = () => {
        setLoadError(false);
        setIsLoading(true);
        fetchThread();
    };

    if (isLoading) return <Page width="reading"><UserListSkeleton count={1} /><FeedSkeleton count={2} /></Page>;
    if (loadError && messages.length === 0) return <Page width="reading"><ErrorState message={t('conversation.loadError')} onRetry={retry} /></Page>;

    const time = (iso) => new Date(iso).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' });

    return (
        <>
            <Box
                data-testid="conversation"
                sx={{
                    display: 'flex', flexDirection: 'column', width: '100%', maxWidth: 680 + 48, mx: 'auto', px: { xs: 0, sm: 3 },
                    height: { xs: 'calc(100dvh - 64px - 64px - env(safe-area-inset-bottom))', md: 'calc(100dvh - 64px)' },
                }}
            >
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, px: { xs: 1, sm: 0 }, py: 1.5, borderBottom: 1, borderColor: 'divider' }}>
                    <Tooltip title={t('conversation.back')}>
                        <IconButton component={Link} to="/messages" aria-label={t('conversation.back')}><ArrowBackRoundedIcon /></IconButton>
                    </Tooltip>
                    <Box component={Link} to={`/profile/${otherUserId}`} sx={{ display: 'flex', alignItems: 'center', gap: 1.25, color: 'text.primary', minWidth: 0, borderRadius: 1 }}>
                        <UserAvatar user={{ userName: otherUser?.userName, profileImageUrl: otherUser?.profileImageUrl }} size={40} />
                        <Typography variant="h3" component="h1" noWrap>{otherUser?.userName ? `@${otherUser.userName}` : t('conversation.fallbackTitle')}</Typography>
                    </Box>
                </Box>
                <Box role="log" aria-live="polite" aria-label={t('conversation.history')} sx={{ flexGrow: 1, overflowY: 'auto', px: { xs: 2, sm: 0 }, py: 2, display: 'flex', flexDirection: 'column', gap: 1.25 }}>
                    {messages.length === 0 && (
                        <EmptyState compact icon={ChatBubbleOutlineIcon} title={t('conversation.empty')} sx={{ my: 'auto' }} />
                    )}
                    {messages.map(msg => {
                        const mine = msg.senderId === user.id;
                        const rich = msg.movie || msg.watchlist;
                        return (
                            <Box key={msg.id} data-testid="message" sx={{ alignSelf: mine ? 'flex-end' : 'flex-start', maxWidth: { xs: '85%', sm: '75%' } }}>
                                <Box sx={{
                                    bgcolor: rich ? 'transparent' : mine ? 'primary.main' : 'surface.raised',
                                    color: mine && !rich ? 'primary.contrastText' : 'text.primary',
                                    px: rich ? 0 : 1.75,
                                    py: rich ? 0 : 1,
                                    borderRadius: '18px',
                                    [mine ? 'borderBottomRightRadius' : 'borderBottomLeftRadius']: rich ? '18px' : '6px',
                                    border: mine || rich ? 0 : 1,
                                    borderColor: 'divider',
                                }}>
                                    {msg.movie ? <MovieRecommendationCard movie={msg.movie} /> :
                                     msg.watchlist ? <WatchlistShareCard watchlist={msg.watchlist} /> :
                                     <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', color: 'inherit' }}>{msg.content}</Typography>
                                    }
                                </Box>
                                <Typography variant="body2" sx={{ display: 'block', textAlign: mine ? 'right' : 'left', color: 'text.secondary', mt: 0.5, fontSize: '0.75rem' }}>
                                    {time(msg.messageSent)}
                                    {msg.id === lastSeenId && ` · ${t('conversation.seen', { time: time(msg.dateRead) })}`}
                                </Typography>
                            </Box>
                        );
                    })}
                    <div ref={messagesEndRef} />
                </Box>
                <Box component="form" onSubmit={handleTextSubmit} sx={{ px: { xs: 1, sm: 0 }, py: 1.5, borderTop: 1, borderColor: 'divider', display: 'flex', gap: 0.5, alignItems: 'center' }}>
                    <Tooltip title={t('conversation.suggestMovie')}>
                        <IconButton onClick={() => setIsMovieModalOpen(true)} aria-label={t('conversation.suggestMovie')} sx={{ color: 'text.secondary' }}>
                            <MovieRoundedIcon />
                        </IconButton>
                    </Tooltip>
                    <Tooltip title={t('conversation.shareList')}>
                        <IconButton onClick={handleOpenWatchlistModal} aria-label={t('conversation.shareList')} sx={{ color: 'text.secondary' }}>
                            <PlaylistAddIcon />
                        </IconButton>
                    </Tooltip>
                    <TextField
                        fullWidth
                        size="small"
                        variant="outlined"
                        placeholder={t('conversation.placeholder')}
                        value={newMessage}
                        onChange={(e) => setNewMessage(e.target.value)}
                        slotProps={{ htmlInput: { 'aria-label': t('conversation.placeholder') } }}
                        sx={{ '& .MuiOutlinedInput-root': { borderRadius: 999, pl: 1 } }}
                    />
                    <Tooltip title={t('common.send')}>
                        <span>
                            <IconButton type="submit" aria-label={t('common.send')} disabled={!newMessage.trim()} sx={{ bgcolor: 'primary.main', color: 'primary.contrastText', ml: 0.5, '&:hover': { bgcolor: 'primary.main', filter: 'brightness(1.08)' }, '&.Mui-disabled': { bgcolor: 'action.disabledBackground' } }}>
                                <SendIcon fontSize="small" />
                            </IconButton>
                        </span>
                    </Tooltip>
                </Box>
            </Box>

            <Dialog open={isMovieModalOpen} onClose={() => setIsMovieModalOpen(false)} maxWidth="xs" fullWidth aria-labelledby="suggest-movie-title"
                slotProps={{ paper: { sx: { alignSelf: 'flex-start', mt: '15vh' } } }}>
                <DialogTitle id="suggest-movie-title">{t('conversation.suggestMovie')}</DialogTitle>
                <DialogContent sx={{ pt: 1 }}>
                    <MovieSearchBar onMovieSelect={handleMovieSelectAndSend} autoFocus />
                </DialogContent>
            </Dialog>

            <Dialog open={isWatchlistModalOpen} onClose={() => setIsWatchlistModalOpen(false)} maxWidth="xs" fullWidth aria-labelledby="share-list-title">
                <DialogTitle id="share-list-title">{t('conversation.whichList')}</DialogTitle>
                <DialogContent sx={{ px: 1.5 }}>
                    <List>
                        {listsError ? (
                            <ErrorState compact message={t('conversation.listsLoadFailed')} onRetry={loadUserLists} />
                        ) : userLists === null ? (
                            <LoadingState compact />
                        ) : userLists.length > 0 ? userLists.map((list) => (
                            <ListItem key={list.id} disablePadding>
                                <ListItemButton onClick={() => handleListSelectAndSend(list.id)} sx={{ borderRadius: 1 }}>
                                    <ListItemText primary={list.name} secondary={t('watchlist.movieCount', { count: list.itemCount })} />
                                </ListItemButton>
                            </ListItem>
                        )) : (
                            <EmptyState compact icon={PlaylistAddIcon} title={t('conversation.noListsToShare')} />
                        )}
                    </List>
                </DialogContent>
            </Dialog>
        </>
    );
};
