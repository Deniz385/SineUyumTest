import React, { useEffect, useState, useCallback } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { Trans, useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Paper, Avatar, AvatarGroup, Alert, Grid, Card, CardContent, CardMedia, CardActions, Button, Chip } from '@mui/material';
import EventIcon from '@mui/icons-material/Event';
import HowToVoteIcon from '@mui/icons-material/HowToVote';
import EventBusyIcon from '@mui/icons-material/EventBusy';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import api, { mediaUrl } from '../api/axiosConfig';

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w500';

const EVENT_DATE_FORMAT = { weekday: 'long', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' };

const AvailableEvent = ({ event, onJoin }) => {
    const { t, i18n } = useTranslation();
    return (
        <Paper elevation={3} sx={{ p: 4, mt: 4, textAlign: 'center' }}>
            <Typography variant="h5" component="h2" gutterBottom>{t('event.next')}</Typography>
            <Typography variant="h6">{event.locationName}</Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'text.secondary', my: 2 }}>
                <EventIcon sx={{ mr: 1 }} />
                <Typography variant="body1">
                    {new Date(event.eventDate).toLocaleDateString(i18n.language, EVENT_DATE_FORMAT)}
                </Typography>
            </Box>
            <Button variant="contained" size="large" onClick={() => onJoin(event.id)} sx={{ mt: 2 }}>
                {t('event.join')}
            </Button>
        </Paper>
    );
};

const PendingEvent = ({ event }) => {
    const { t } = useTranslation();
    return (
        <Alert severity="info" sx={{ mt: 4, p: 3 }}>
            <Typography variant="h6">{t('event.pendingTitle')}</Typography>
            <Typography>
                <Trans i18nKey="event.pendingBody" values={{ location: event.locationName }} components={{ strong: <strong /> }} />
            </Typography>
        </Alert>
    );
};

const MatchedEvent = ({ data, onVote }) => {
    const { t, i18n } = useTranslation();
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
                    <Typography variant="h6">{new Date(event.eventDate).toLocaleDateString(i18n.language, EVENT_DATE_FORMAT)}</Typography>
                </Box>
                <Typography variant="h6" component="h3" sx={{ mt: 4, mb: 2 }}>{t('event.yourTable')}</Typography>
                <AvatarGroup max={10} sx={{ justifyContent: 'center', mb: 2 }}>
                    {groupMembers.map(member => (
                        <Avatar key={member.id} component={Link} to={`/profile/${member.id}`} alt={member.userName} src={mediaUrl(member.profileImageUrl)} sx={{ width: 64, height: 64, border: '2px solid white' }} title={member.userName} />
                    ))}
                </AvatarGroup>
            </Paper>

            <Typography variant="h5" component="h2" sx={{ mb: 3 }}>{t('event.movieVote')}</Typography>
            <Grid container spacing={3} justifyContent="center">
                {suggestedMovies.map(movie => (
                    <Grid size={{ xs: 12, sm: 6, md: 4 }} key={movie.id}>
                        <Card sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <CardMedia component="img" height="350" image={movie.posterPath ? `${IMAGE_BASE_URL}${movie.posterPath}` : '/vite.svg'} alt={movie.title} />
                            <CardContent sx={{ flexGrow: 1 }}><Typography gutterBottom variant="h6" component="div">{movie.title}</Typography></CardContent>
                            <CardActions sx={{ justifyContent: 'space-between', p: 2 }}>
                                <Button size="small" variant={currentUserVoteId === movie.id ? "contained" : "outlined"} onClick={() => onVote(movie.id)} startIcon={<HowToVoteIcon />}>
                                    {currentUserVoteId === movie.id ? t('event.myVote') : t('event.vote')}
                                </Button>
                                <Chip label={t('event.voteCount', { count: getVoteCount(movie.id) })} variant="outlined" />
                            </CardActions>
                        </Card>
                    </Grid>
                ))}
            </Grid>
        </>
    );
};


export const MyEventPage = () => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const navigate = useNavigate();
    const [statusData, setStatusData] = useState(null);
    const [isLoading, setIsLoading] = useState(true);

    const fetchEventStatus = useCallback(async () => {
        if (!user || !user.isSubscribed) {
            setIsLoading(false);
            return;
        }
        setIsLoading(true);
        try {
            const response = await api.get('/api/v1/event/my-status');
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

    // The server enforces the paid feature itself (402). If the subscription
    // has ended, the user is sent to the subscription page.
    const handleActionError = (error, fallback) => {
        if (error.response?.status === 402) {
            navigate('/subscription');
            return;
        }
        const data = error.response?.data;
        alert(data?.message || data?.title || fallback);
    };

    const handleJoinEvent = async (eventId) => {
        try {
            await api.post(`/api/v1/event/${eventId}/join`);
            fetchEventStatus();
        } catch (error) {
            handleActionError(error, t('event.joinFailed'));
        }
    };
    
    const handleVote = async (movieId) => {
        try {
            await api.post('/api/v1/event/vote', { 
                groupId: statusData.group.id, 
                movieId: movieId 
            });
            fetchEventStatus();
        } catch (error) {
            handleActionError(error, t('event.voteFailed'));
        }
    };
    
    if (!user) {
        return <LoadingState />;
    }
    
    if (!user.isSubscribed) {
        return <Navigate to="/subscription" replace />;
    }

    const renderContent = () => {
        if (isLoading) {
            return <LoadingState />;
        }
        if (!statusData) {
            return <ErrorState message={t('event.statusUnavailable')} onRetry={fetchEventStatus} />;
        }

        switch (statusData.status) {
            case 'AVAILABLE':
                return <AvailableEvent event={statusData.event} onJoin={handleJoinEvent} />;
            case 'PENDING':
                return <PendingEvent event={statusData.event} />;
            case 'MATCHED':
                return <MatchedEvent data={statusData} onVote={handleVote} />;
            case 'NONE':
                return <EmptyState icon={EventBusyIcon} title={t('event.none')} />;
            default:
                return <ErrorState message={t('event.loadError')} onRetry={fetchEventStatus} />;
        }
    };

    return (
        <Box className="page-container">
            <Typography variant="h4" component="h1" gutterBottom>{t('event.title')}</Typography>
            {renderContent()}
        </Box>
    );
};