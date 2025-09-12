import React, { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, TextField, IconButton, Modal, Card, CardMedia, List, ListItem, ListItemButton, ListItemText, Divider } from '@mui/material';
import SendIcon from '@mui/icons-material/Send';
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutline';
import PlaylistAddIcon from '@mui/icons-material/PlaylistAdd';
import { MovieSearchBar } from '../components/MovieSearchBar';

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

const modalStyle = {
  position: 'absolute',
  top: '20%',
  left: '50%',
  transform: 'translateX(-50%)',
  width: { xs: '90%', sm: 400 },
  bgcolor: 'background.paper',
  p: 2,
  boxShadow: 24,
  borderRadius: 2
};

const MovieRecommendationCard = ({ movie }) => (
    <Card sx={{ display: 'flex', alignItems: 'center', p: 1, textDecoration: 'none', backgroundColor: 'rgba(255,255,255,0.1)' }} component={Link} to={`/movie/${movie.id}`}>
        <CardMedia
            component="img"
            sx={{ width: 60, height: 90, borderRadius: 1 }}
            image={movie.posterPath ? `${IMAGE_BASE_URL}${movie.posterPath}` : '/vite.svg'}
            alt={movie.title}
        />
        <Box sx={{ ml: 1.5 }}>
            <Typography variant="body2" sx={{ opacity: 0.8 }}>Film Önerisi:</Typography>
            <Typography fontWeight="bold">{movie.title}</Typography>
        </Box>
    </Card>
);

const WatchlistShareCard = ({ watchlist }) => (
    <Card sx={{ display: 'flex', alignItems: 'center', p: 1, textDecoration: 'none', backgroundColor: 'rgba(255,255,255,0.1)' }} component={Link} to={`/watchlist/${watchlist.id}`}>
        <PlaylistAddIcon sx={{ fontSize: 40, mx: 1.5 }} />
        <Box sx={{ ml: 1.5 }}>
            <Typography variant="body2" sx={{ opacity: 0.8 }}>İzleme Listesi:</Typography>
            <Typography fontWeight="bold">{watchlist.name}</Typography>
            <Typography variant="caption" sx={{ opacity: 0.7 }}>@{watchlist.ownerUsername}</Typography>
        </Box>
    </Card>
);

export const ConversationPage = () => {
    const { otherUserId } = useParams();
    const { user } = useAuth();
    const [messages, setMessages] = useState([]);
    const [otherUser, setOtherUser] = useState(null);
    const [newMessage, setNewMessage] = useState('');
    const [isLoading, setIsLoading] = useState(true);
    const [isMovieModalOpen, setIsMovieModalOpen] = useState(false);
    const [isWatchlistModalOpen, setIsWatchlistModalOpen] = useState(false);
    const [userLists, setUserLists] = useState([]);
    const messagesEndRef = useRef(null);

    const scrollToBottom = () => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    };

    const fetchThread = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get(`/api/messages/thread/${otherUserId}`);
            // DÜZELTME: Gelen verinin içindeki $values dizisini alıyoruz.
            const messageData = response.data.$values || response.data || [];
            setMessages(messageData);

            if (messageData.length > 0) {
                const firstMessage = messageData[0];
                setOtherUser(firstMessage.senderId === user.id ? 
                    { id: firstMessage.recipientId, userName: firstMessage.recipientUsername } :
                    { id: firstMessage.senderId, userName: firstMessage.senderUsername }
                );
            } else {
                const profileResponse = await api.get(`/api/profile/${otherUserId}`);
                setOtherUser(profileResponse.data);
            }
        } catch (err) {
            console.error("Mesajlar yüklenemedi:", err);
        } finally {
            setIsLoading(false);
        }
    }, [otherUserId, user]);

    useEffect(() => {
        fetchThread();
    }, [fetchThread]);

    useEffect(scrollToBottom, [messages]);

    const handleSendMessage = async ({ content, movieId, watchlistId }) => {
        if (!content && !movieId && !watchlistId) return;
        try {
            await api.post(`/api/messages`, { recipientId: otherUserId, content, movieId, watchlistId });
            setNewMessage('');
            fetchThread(); 
        } catch (err) {
            console.error("Mesaj gönderilemedi:", err);
        }
    };
    
    const handleMovieSelectAndSend = async (movie) => {
        setIsMovieModalOpen(false);
        try {
            await api.post(`/api/ratings/addmovie`, { id: movie.id, title: movie.title, posterPath: movie.poster_path });
        } catch (addMovieError) {
            if (addMovieError.response && addMovieError.response.status !== 400) {
               console.error("Film eklenirken bir hata oluştu:", addMovieError);
               return;
            }
        }
        handleSendMessage({ movieId: movie.id });
    };

    const handleOpenWatchlistModal = async () => {
        setIsWatchlistModalOpen(true);
        try {
            const response = await api.get(`/api/watchlist`);
            // DÜZELTME: Gelen verinin içindeki $values dizisini alıyoruz.
            setUserLists(response.data.$values || response.data || []);
        } catch (err) {
            console.error("Listeler yüklenemedi:", err);
        }
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

    if (isLoading) return <CircularProgress />;

    return (
        <>
            <Box sx={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 120px)', width: '100%', maxWidth: '800px', margin: 'auto' }}>
                <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider' }}>
                    <Typography variant="h6" component={Link} to={`/profile/${otherUserId}`} sx={{textDecoration: 'none', color: 'inherit'}}>
                        {otherUser?.userName || 'Sohbet'}
                    </Typography>
                </Box>
                <Box sx={{ flexGrow: 1, overflowY: 'auto', p: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
                    {messages.map(msg => (
                        <Box key={msg.id} sx={{ alignSelf: msg.senderId === user.id ? 'flex-end' : 'flex-start', maxWidth: '70%' }}>
                            <Box sx={{
                                bgcolor: msg.senderId === user.id ? 'primary.main' : 'grey.300',
                                color: msg.senderId === user.id ? 'primary.contrastText' : 'text.primary',
                                p: (msg.movie || msg.watchlist) ? 0.5 : 1.5,
                                borderRadius: 2
                            }}>
                                {msg.movie ? <MovieRecommendationCard movie={msg.movie} /> :
                                 msg.watchlist ? <WatchlistShareCard watchlist={msg.watchlist} /> :
                                 <Typography variant="body1">{msg.content}</Typography>
                                }
                            </Box>
                            <Typography variant="caption" sx={{ display: 'block', textAlign: msg.senderId === user.id ? 'right' : 'left', color: 'text.secondary', mt: 0.5 }}>
                                {new Date(msg.messageSent).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
                            </Typography>
                        </Box>
                    ))}
                    <div ref={messagesEndRef} />
                </Box>
                <Box component="form" onSubmit={handleTextSubmit} sx={{ p: 2, borderTop: 1, borderColor: 'divider', display: 'flex', gap: 1 }}>
                    <IconButton color="primary" onClick={() => setIsMovieModalOpen(true)} title="Film Öner">
                        <AddCircleOutlineIcon />
                    </IconButton>
                    <IconButton color="primary" onClick={handleOpenWatchlistModal} title="Liste Paylaş">
                        <PlaylistAddIcon />
                    </IconButton>
                    <TextField fullWidth variant="outlined" placeholder="Mesaj yaz..." value={newMessage} onChange={(e) => setNewMessage(e.target.value)} />
                    <IconButton type="submit" color="primary"><SendIcon /></IconButton>
                </Box>
            </Box>

            <Modal open={isMovieModalOpen} onClose={() => setIsMovieModalOpen(false)}>
                <Box sx={modalStyle}>
                    <Typography variant="h6" sx={{ mb: 2 }}>Film Öner</Typography>
                    <MovieSearchBar onMovieSelect={handleMovieSelectAndSend} />
                </Box>
            </Modal>

            <Modal open={isWatchlistModalOpen} onClose={() => setIsWatchlistModalOpen(false)}>
                <Box sx={modalStyle}>
                    <Typography variant="h6" component="h2">Hangi Listeyi Paylaşacaksın?</Typography>
                    <List sx={{ mt: 2 }}>
                        {userLists.length > 0 ? userLists.map((list) => (
                            <ListItem key={list.id} disablePadding>
                                <ListItemButton onClick={() => handleListSelectAndSend(list.id)}>
                                    <ListItemText primary={list.name} secondary={`${list.itemCount} film`} />
                                </ListItemButton>
                            </ListItem>
                        )) : (<Typography>Henüz paylaşacak listeniz yok.</Typography>)}
                    </List>
                </Box>
            </Modal>
        </>
    );
};
