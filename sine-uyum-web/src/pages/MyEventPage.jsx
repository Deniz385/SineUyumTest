// sine-uyum-web/src/pages/MyEventPage.jsx
import React, { useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, Paper, Avatar, AvatarGroup, Alert, Grid, Card, CardMedia, CardActions, Button, Chip } from '@mui/material';
import EventIcon from '@mui/icons-material/Event';
import HowToVoteIcon from '@mui/icons-material/HowToVote';
import api from '../api/axiosConfig'; // Merkezi axios instance'ını kullanıyoruz

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w500';

export const MyEventPage = () => {
    const { user, token } = useAuth();
    const [eventData, setEventData] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');

    const fetchMyEvent = async () => {
        if (!user || !user.isSubscribed) {
            setIsLoading(false);
            return;
        }
        try {
            const response = await api.get('/api/event/my-event');
            setEventData(response.data);
        } catch (err) {
            if (err.response && err.response.status === 404) {
                setError("Harika bir film deneyimi için eşleştirmeni yapıyoruz! Lütfen daha sonra tekrar kontrol et.");
            } else {
                setError('Etkinlik bilgileri yüklenirken bir hata oluştu.');
            }
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        if (user) {
            fetchMyEvent();
        }
    }, [user, token]);

    const handleVote = async (movieId) => {
        try {
            await api.post('/api/event/vote', { 
                groupId: eventData.groupInfo.group.id, 
                movieId: movieId 
            });
            // Oylama sonrası en güncel veriyi anında görmek için listeyi yenile
            fetchMyEvent();
        } catch (err) {
            console.error("Oylama sırasında hata:", err);
            alert("Oyunuz kaydedilirken bir sorun oluştu.");
        }
    };

    if (!user || isLoading) {
        return (
            <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '80vh' }}>
                <CircularProgress />
            </Box>
        );
    }
    
    if (!user.isSubscribed) {
        return <Navigate to="/subscription" replace />;
    }

    // Oyları saymak için bir yardımcı fonksiyon
    const getVoteCount = (movieId) => {
        return eventData?.groupInfo.votes.filter(v => v.movieId === movieId).length || 0;
    };

    // Mevcut kullanıcının oyunu bulmak için
    const currentUserVoteId = eventData?.groupInfo.votes.find(v => v.userId === user.id)?.movieId;

    return (
        <Box className="page-container">
            <Typography variant="h4" component="h1" gutterBottom>
                Etkinliğim
            </Typography>

            {error && !eventData && (
                <Alert severity="info" sx={{ mt: 4 }}>{error}</Alert>
            )}

            {eventData && (
                <>
                    <Paper elevation={3} sx={{ p: 3, mt: 4, mb: 4 }}>
                        <Typography variant="h5" component="h2">{eventData.groupInfo.event.locationName}</Typography>
                        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'text.secondary', my: 2 }}>
                            <EventIcon sx={{ mr: 1 }} />
                            <Typography variant="h6">{new Date(eventData.groupInfo.event.eventDate).toLocaleDateString('tr-TR', { weekday: 'long', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' })}</Typography>
                        </Box>

                        <Typography variant="h6" component="h3" sx={{ mt: 4, mb: 2 }}>Masanız</Typography>
                        <AvatarGroup max={10} sx={{ justifyContent: 'center', mb: 2 }}>
                            {eventData.groupInfo.group.members.map(member => (
                                <Avatar key={member.id} component={Link} to={`/profile/${member.id}`} alt={member.userName} src={member.profileImageUrl} sx={{ width: 64, height: 64, border: '2px solid white' }} title={member.userName} />
                            ))}
                        </AvatarGroup>
                    </Paper>

                    <Typography variant="h5" component="h2" sx={{ mb: 3 }}>Film Oylaması</Typography>
                    <Grid container spacing={3} justifyContent="center">
                        {eventData.suggestedMovies.map(movie => (
                            <Grid item xs={12} sm={6} md={4} key={movie.id}>
                                <Card sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                                    <CardMedia
                                        component="img"
                                        height="350"
                                        image={movie.posterPath ? `${IMAGE_BASE_URL}${movie.posterPath}` : '/vite.svg'}
                                        alt={movie.title}
                                    />
                                    <CardContent sx={{ flexGrow: 1 }}>
                                        <Typography gutterBottom variant="h6" component="div">{movie.title}</Typography>
                                    </CardContent>
                                    <CardActions sx={{ justifyContent: 'space-between', p: 2 }}>
                                        <Button 
                                            size="small" 
                                            variant={currentUserVoteId === movie.id ? "contained" : "outlined"}
                                            onClick={() => handleVote(movie.id)}
                                            startIcon={<HowToVoteIcon />}
                                        >
                                            {currentUserVoteId === movie.id ? "Oyum Bu" : "Oyla"}
                                        </Button>
                                        <Chip 
                                            label={`${getVoteCount(movie.id)} Oy`} 
                                            variant="outlined" 
                                        />
                                    </CardActions>
                                </Card>
                            </Grid>
                        ))}
                    </Grid>
                </>
            )}
        </Box>
    );
};