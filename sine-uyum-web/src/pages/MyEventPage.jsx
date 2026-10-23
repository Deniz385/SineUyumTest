import React, { useEffect, useState, useCallback } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { Trans, useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { useSnackbar } from '../context/SnackbarProvider';
import { Box, Typography, Avatar, AvatarGroup, Card, Button, Tooltip } from '@mui/material';
import EventIcon from '@mui/icons-material/EventRounded';
import HowToVoteIcon from '@mui/icons-material/HowToVoteRounded';
import EventBusyIcon from '@mui/icons-material/EventBusyRounded';
import PlaceRoundedIcon from '@mui/icons-material/PlaceRounded';
import HourglassTopRoundedIcon from '@mui/icons-material/HourglassTopRounded';
import CheckRoundedIcon from '@mui/icons-material/CheckRounded';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { Page, PageHeader } from '../components/ui/Page';
import { CardSkeleton } from '../components/ui/Skeletons';
import { PosterCard } from '../components/ui/PosterCard';
import { apiErrorMessage } from '../components/ui/apiError';
import { gradients } from '../theme';
import api, { mediaUrl } from '../api/axiosConfig';

const EVENT_DATE_FORMAT = { weekday: 'long', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' };

// Where and when, in a card with the marquee edge.
const EventCard = ({ overline, event, children, testId }) => {
    const { i18n } = useTranslation();
    return (
        <Card component="section" data-testid={testId} sx={{ p: { xs: 2.5, md: 3.5 }, position: 'relative', overflow: 'hidden', '&::before': { content: '""', position: 'absolute', inset: '0 0 auto 0', height: 3, background: gradients.marquee } }}>
            {overline && <Typography variant="overline" component="p" color="text.secondary">{overline}</Typography>}
            <Typography variant="h1" component="h2" sx={{ mt: 0.5, display: 'flex', alignItems: 'center', gap: 1 }}>
                <PlaceRoundedIcon sx={{ color: 'primary.main' }} aria-hidden="true" />{event.locationName}
            </Typography>
            <Typography variant="body1" color="text.secondary" sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 1 }}>
                <EventIcon fontSize="small" aria-hidden="true" />
                {new Date(event.eventDate).toLocaleDateString(i18n.language, EVENT_DATE_FORMAT)}
            </Typography>
            {children}
        </Card>
    );
};

const AvailableEvent = ({ event, onJoin }) => {
    const { t } = useTranslation();
    return (
        <EventCard overline={t('event.next')} event={event} testId="event-available">
            <Button variant="contained" size="large" onClick={() => onJoin(event.id)} sx={{ mt: 3 }}>
                {t('event.join')}
            </Button>
        </EventCard>
    );
};

const PendingEvent = ({ event }) => {
    const { t } = useTranslation();
    return (
        <Card sx={{ p: 3, display: 'flex', gap: 2, alignItems: 'flex-start' }} data-testid="event-pending">
            <Box aria-hidden="true" sx={{ width: 48, height: 48, borderRadius: '50%', bgcolor: 'surface.raised', color: 'info.main', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                <HourglassTopRoundedIcon />
            </Box>
            <Box>
                <Typography variant="h2" component="h2">{t('event.pendingTitle')}</Typography>
                <Typography variant="body1" color="text.secondary" sx={{ mt: 0.75 }}>
                    <Trans i18nKey="event.pendingBody" values={{ location: event.locationName }} components={{ strong: <Box component="strong" sx={{ color: 'text.primary' }} /> }} />
                </Typography>
            </Box>
        </Card>
    );
};

const MatchedEvent = ({ data, onVote }) => {
    const { t } = useTranslation();
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
            <EventCard event={event} testId="event-matched">
                <Typography variant="h3" component="h3" sx={{ mt: 3, mb: 1.5 }}>{t('event.yourTable')}</Typography>
                <AvatarGroup max={10} sx={{ justifyContent: 'flex-end', flexDirection: 'row-reverse', '& .MuiAvatar-root': { borderColor: 'background.paper' } }}>
                    {groupMembers.map(member => (
                        <Tooltip key={member.id} title={`@${member.userName}`}>
                            <Avatar component={Link} to={`/profile/${member.id}`} alt={member.userName} src={mediaUrl(member.profileImageUrl)} sx={{ width: 52, height: 52 }}>
                                {member.userName?.charAt(0).toUpperCase()}
                            </Avatar>
                        </Tooltip>
                    ))}
                </AvatarGroup>
            </EventCard>

            <Typography variant="h2" component="h2" sx={{ mt: 5, mb: 2 }}>{t('event.movieVote')}</Typography>
            <Box component="ul" sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(auto-fill, minmax(170px, 1fr))' }, gap: 2.5 }}>
                {suggestedMovies.map(movie => {
                    const mine = currentUserVoteId === movie.id;
                    const count = getVoteCount(movie.id);
                    return (
                        <li key={movie.id}>
                            <PosterCard movie={{ id: movie.id, title: movie.title, posterPath: movie.posterPath }} fluid />
                            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1, mt: 1, flexWrap: 'wrap' }}>
                                <Button
                                    size="small"
                                    variant={mine ? 'contained' : 'tonal'}
                                    onClick={() => onVote(movie.id)}
                                    startIcon={mine ? <CheckRoundedIcon /> : <HowToVoteIcon />}
                                    aria-pressed={mine}
                                    aria-label={`${mine ? t('event.myVote') : t('event.vote')}: ${movie.title}`}
                                >
                                    {mine ? t('event.myVote') : t('event.vote')}
                                </Button>
                                <Typography variant="body2" color="text.secondary" className="tabular-nums">{t('event.voteCount', { count })}</Typography>
                            </Box>
                        </li>
                    );
                })}
            </Box>
        </>
    );
};


export const MyEventPage = () => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar();
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
        showSnackbar(apiErrorMessage(error, fallback), 'error');
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
        return <Page><CardSkeleton /></Page>;
    }

    if (!user.isSubscribed) {
        return <Navigate to="/subscription" replace />;
    }

    const renderContent = () => {
        if (isLoading) {
            return <CardSkeleton lines={3} height={180} />;
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
        <Page>
            <PageHeader title={t('event.title')} subtitle={t('event.subtitle')} />
            {renderContent()}
        </Page>
    );
};
