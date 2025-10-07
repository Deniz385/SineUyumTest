import React, { useEffect, useState, useCallback } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, Paper, Avatar, AvatarGroup, Alert, Grid, Card, CardContent, CardMedia, CardActions, Button, Chip } from '@mui/material';
import EventIcon from '@mui/icons-material/Event';
import HowToVoteIcon from '@mui/icons-material/HowToVote';
import api from '../api/axiosConfig';

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w500';

const AvailableEvent = ({ event, onJoin }) => (
    <Paper elevation={3} sx={{ p: 4, mt: 4, textAlign: 'center' }}>
        <Typography variant="h5" component="h2" gutterBottom>Sıradaki Etkinlik</Typography>
        <Typography variant="h6">{event.locationName}</Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'text.secondary', my: 2 }}>
            <EventIcon sx={{ mr: 1 }} />
            <Typography variant="body1">
                {new Date(event.eventDate).toLocaleDateString('tr-TR', { weekday: 'long', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
            </Typography>
        </Box>
        <Button variant="contained" size="large" onClick={() => onJoin(event.id)} sx={{ mt: 2 }}>
            Bu Haftaki Etkinliğe Katıl!
        </Button>
    </Paper>
);

const PendingEvent = ({ event }) => (
    <Alert severity="info" sx={{ mt: 4, p: 3 }}>
        <Typography variant="h6">Eşleştirme Bekleniyor</Typography>
        <Typography>Harika bir film deneyimi için eşleştirmeni yapıyoruz! **{event.locationName}** etkinliğine başarıyla katıldın. Eşleşmeler tamamlandığında bu sayfada masanı görebileceksin.</Typography>
    </Alert>
);

const MatchedEvent = ({ data, onVote }) => {
    const { user } = useAuth();
    
    const group = data.group;
    const event = data.event;
    const votes = data.votes || [];
    const suggestedMovies = data.suggestedMovies || [];
    const groupMembers = group.members || [];

    const getVoteCount = (movieId) => votes.filter(v => v.movieId === movieId).length;
    const currentUserVoteId = votes.find(v => v.userId === user.id)?.movieId;

    return (
        <>
            <Paper elevation={3} sx={{ p: 3, mt: 4, mb: 4 }}>
                <Typography variant="h5" component="h2">{event.locationName}</Typography>
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'text.secondary', my: 2 }}>
                    <EventIcon sx={{ mr: 1 }} />
                    <Typography variant="h6">{new Date(event.eventDate).toLocaleDateString('tr-TR', { weekday: 'long', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' })}</Typography>
                </Box>
                <Typography variant="h6" component="h3" sx={{ mt: 4, mb: 2 }}>Masanız</Typography>
                <AvatarGroup max={10} sx={{ justifyContent: 'center', mb: 2 }}>
                    {groupMembers.map(member => (
                        <Avatar key={member.id} component={Link} to={`/profile/${member.id}`} alt={member.userName} src={member.profileImageUrl} sx={{ width: 64, height: 64, border: '2px solid white' }} title={member.userName} />
                    ))}
                </AvatarGroup>
            </Paper>

            <Typography variant="h5" component="h2" sx={{ mb: 3 }}>Film Oylaması</Typography>
            <Grid container spacing={3} justifyContent="center">
                {suggestedMovies.map(movie => (
                    <Grid item xs={12} sm={6} md={4} key={movie.id}>
                        <Card sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <CardMedia component="img" height="350" image={movie.posterPath ? `${IMAGE_BASE_URL}${movie.posterPath}` : '/vite.svg'} alt={movie.title} />
                            <CardContent sx={{ flexGrow: 1 }}><Typography gutterBottom variant="h6" component="div">{movie.title}</Typography></CardContent>
                            <CardActions sx={{ justifyContent: 'space-between', p: 2 }}>
                                <Button size="small" variant={currentUserVoteId === movie.id ? "contained" : "outlined"} onClick={() => onVote(movie.id)} startIcon={<HowToVoteIcon />}>
                                    {currentUserVoteId === movie.id ? "Oyum Bu" : "Oyla"}
                                </Button>
                                <Chip label={`${getVoteCount(movie.id)} Oy`} variant="outlined" />
                            </CardActions>
                        </Card>
                    </Grid>
                ))}
            </Grid>
        </>
    );
};


export const MyEventPage = () => {
    const { user } = useAuth();
    const [statusData, setStatusData] = useState(null);
    const [isLoading, setIsLoading] = useState(true);

    const fetchEventStatus = useCallback(async () => {
        if (!user || !user.isSubscribed) {
            setIsLoading(false);
            return;
        }
        setIsLoading(true);
        try {
            const response = await api.get('/api/event/my-status');
            setStatusData(response.data);
        } catch (err) {
            if (err.response?.status !== 401) {
                setStatusData({ status: 'ERROR' });
            }
        } finally {
            setIsLoading(false);
        }
    }, [user]);

    useEffect(() => {
        fetchEventStatus();
    }, [fetchEventStatus]);

    const handleJoinEvent = async (eventId) => {
        try {
            await api.post(`/api/event/${eventId}/join`);
            fetchEventStatus();
        } catch (error) {
            alert(error.response?.data?.message || "Etkinliğe katılırken bir hata oluştu.");
        }
    };
    
    const handleVote = async (movieId) => {
        try {
            await api.post('/api/event/vote', { 
                groupId: statusData.group.id, 
                movieId: movieId 
            });
            fetchEventStatus();
        } catch (err) {
            alert("Oyunuz kaydedilirken bir sorun oluştu.");
        }
    };
    
    if (!user) {
        return (
            <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '80vh' }}>
                <CircularProgress />
            </Box>
        );
    }
    
    if (!user.isSubscribed) {
        return <Navigate to="/subscription" replace />;
    }

    const renderContent = () => {
        if (isLoading) {
            return (
                <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '50vh' }}>
                    <CircularProgress />
                </Box>
            );
        }
        if (!statusData) {
            return <Alert severity="warning" sx={{ mt: 4 }}>Etkinlik durumu alınamadı. Lütfen sayfayı yenileyin.</Alert>;
        }

        switch (statusData.status) {
            case 'AVAILABLE':
                return <AvailableEvent event={statusData.event} onJoin={handleJoinEvent} />;
            case 'PENDING':
                return <PendingEvent event={statusData.event} />;
            case 'MATCHED':
                return <MatchedEvent data={statusData} onVote={handleVote} />;
            case 'NONE':
                return <Alert severity="info" sx={{ mt: 4 }}>Katılabileceğin yeni bir etkinlik bulunmuyor. Lütfen daha sonra tekrar kontrol et.</Alert>;
            default:
                return <Alert severity="error" sx={{ mt: 4 }}>Etkinlik bilgileri yüklenirken bir hata oluştu.</Alert>;
        }
    };

    return (
        <Box className="page-container">
            <Typography variant="h4" component="h1" gutterBottom>Etkinliğim</Typography>
            {renderContent()}
        </Box>
    );
};