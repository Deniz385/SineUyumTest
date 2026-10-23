import React, { useEffect, useState, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
    Box, Typography, Chip, Button, Dialog, DialogTitle, DialogContent, List, ListItem, ListItemButton, ListItemText, Divider,
    Tooltip, Skeleton, Avatar,
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import StarRoundedIcon from '@mui/icons-material/StarRounded';
import EditCalendarRoundedIcon from '@mui/icons-material/EditCalendarRounded';
import PlaylistAddRoundedIcon from '@mui/icons-material/PlaylistAddRounded';
import IosShareRoundedIcon from '@mui/icons-material/IosShareRounded';
import PersonRoundedIcon from '@mui/icons-material/PersonRounded';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { RatingModal } from '../components/RatingModal';
import { MovieReviews } from '../components/MovieReviews';
import { DiaryEntryDialog } from '../components/DiaryEntryDialog';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { useSnackbar } from '../context/SnackbarProvider';
import { Page } from '../components/ui/Page';
import { PosterImage } from '../components/ui/PosterImage';
import { SectionHeader } from '../components/ui/MovieRow';
import { tmdbImage } from '../components/ui/posterUtils';
import { srOnly } from '../components/ui/a11y';
import { apiErrorMessage } from '../components/ui/apiError';
import { backdropScrim, layout } from '../theme';

const formatScore = (value, language) => value.toLocaleString(language, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

// One score: a big number out of 10 with its label ("For you", "TMDb").
const ScoreTile = ({ value, label, hint, testId, tooltip }) => {
    const tile = (
        <Box data-testid={testId} tabIndex={tooltip ? 0 : undefined} sx={{ minWidth: 92, px: 2, py: 1.25, borderRadius: 1.5, bgcolor: 'surface.raised', border: 1, borderColor: 'divider' }}>
            <Typography component="p" aria-hidden="true" sx={{ typography: 'number', fontSize: '1.75rem', color: 'text.primary' }}>
                {value}<Box component="span" sx={{ fontSize: '0.875rem', color: 'text.secondary', ml: 0.25 }}>/10</Box>
            </Typography>
            <Typography variant="caption" component="p" sx={{ color: 'text.secondary', mt: 0.5 }} aria-hidden="true">{label}</Typography>
            <Box component="span" sx={srOnly}>{hint}</Box>
        </Box>
    );
    return tooltip ? <Tooltip title={tooltip}>{tile}</Tooltip> : tile;
};

const CastRow = ({ cast }) => {
    const { t } = useTranslation();
    if (!cast?.length) return null;
    return (
        <Box component="section" sx={{ mt: 5 }}>
            <SectionHeader id="cast-heading" title={t('movie.cast')} />
            <Box role="region" aria-labelledby="cast-heading" tabIndex={0} className="no-scrollbar" sx={{ overflowX: 'auto', scrollSnapType: 'x mandatory', pb: 1, mx: -0.5, px: 0.5, pt: 0.5, borderRadius: 1.5 }}>
                <Box component="ul" sx={{ display: 'flex', gap: 2, listStyle: 'none', m: 0, p: 0 }}>
                    {cast.map((actor) => (
                        <Box component="li" key={actor.cast_id ?? actor.name} sx={{ width: 96, flexShrink: 0, scrollSnapAlign: 'start' }}>
                            <Avatar
                                src={tmdbImage(actor.profile_path, 'w185') ?? undefined}
                                alt=""
                                variant="rounded"
                                sx={{ width: 96, height: 120, borderRadius: 1.5, bgcolor: 'surface.raised', color: 'text.muted', border: 1, borderColor: 'divider' }}
                                imgProps={{ loading: 'lazy' }}
                            >
                                <PersonRoundedIcon sx={{ fontSize: 40 }} />
                            </Avatar>
                            <Typography variant="body2" sx={{ fontWeight: 600, mt: 1, color: 'text.primary' }}>{actor.name}</Typography>
                            {actor.character && <Typography variant="body2" color="text.secondary">{actor.character}</Typography>}
                        </Box>
                    ))}
                </Box>
            </Box>
        </Box>
    );
};

const HeroSkeleton = () => (
    <Box role="status" aria-busy="true" data-testid="movie-skeleton">
        <Skeleton variant="rectangular" sx={{ width: '100%', height: { xs: 260, md: 420 } }} />
        <Page sx={{ mt: { xs: -10, md: -16 }, position: 'relative' }}>
            <Box sx={{ display: 'flex', gap: 3, alignItems: 'flex-end' }}>
                <Skeleton variant="rounded" sx={{ width: { xs: 110, md: 200 }, height: { xs: 165, md: 300 }, borderRadius: 1.5, flexShrink: 0 }} />
                <Box sx={{ flex: 1 }}>
                    <Skeleton variant="text" sx={{ fontSize: '2.5rem', width: '60%' }} />
                    <Skeleton variant="text" sx={{ width: '35%' }} />
                </Box>
            </Box>
        </Page>
    </Box>
);

export const MovieDetailPage = () => {
    const { t, i18n } = useTranslation();
    const theme = useTheme();
    const { movieId } = useParams();
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar();
    const [movie, setMovie] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [isRatingModalOpen, setIsRatingModalOpen] = useState(false);
    const [isWatchlistModalOpen, setIsWatchlistModalOpen] = useState(false);
    // null while loading; listsError when they could not be loaded.
    const [userLists, setUserLists] = useState(null);
    const [listsError, setListsError] = useState(false);
    const [reviewsKey, setReviewsKey] = useState(0);
    // "For you" score: the rating from people who share your taste (arrives with the reviews).
    const [forYou, setForYou] = useState(null);
    // Your current rating (arrives with the reviews); prefills the dialogs.
    const [myRating, setMyRating] = useState(null);
    const [isDiaryOpen, setIsDiaryOpen] = useState(false);

    const fetchMovieDetails = useCallback(async () => {
        if (!user) return;
        setIsLoading(true);
        setError('');
        try {
            const response = await api.get(`/api/v1/movies/${movieId}`);
            setMovie(response.data);
        } catch {
            setError(t('movie.loadError'));
        } finally {
            setIsLoading(false);
        }
    }, [movieId, user, t]);

    useEffect(() => {
        fetchMovieDetails();
    }, [fetchMovieDetails]);

    const loadUserLists = async () => {
        setUserLists(null);
        setListsError(false);
        try {
            const response = await api.get(`/api/v1/watchlist`);
            // Older API responses wrapped arrays in $values.
            setUserLists(response.data.$values || response.data);
        } catch {
            setListsError(true);
        }
    };

    const handleOpenWatchlistModal = () => {
        setIsWatchlistModalOpen(true);
        loadUserLists();
    };

    const handleAddMovieToList = async (listId) => {
        setIsWatchlistModalOpen(false);
        const movieData = { movieId: movie.id, title: movie.title, posterPath: movie.poster_path };
        try {
            await api.post(`/api/v1/watchlist/${listId}/movies`, movieData);
            showSnackbar(t('movie.addedToList'), 'success');
        } catch (err) {
            showSnackbar(apiErrorMessage(err, t('movie.addToListFailed')), 'error');
        }
    };

    const handleRateMovie = async (movieToRate, rating) => {
        try {
            // A missing movie is added to the catalog by the server.
            await api.post(`/api/v1/ratings`, { movieId: movieToRate.id, rating });
            setMyRating(rating);
            showSnackbar(t('movie.rated', { title: movieToRate.title, rating }), 'success');
            // Now rated: refresh the reviews (opens the review form, updates "For you").
            setReviewsKey((prev) => prev + 1);
        } catch {
            showSnackbar(t('movie.rateFailed'), 'error');
        }
    };

    // Logging with a new rating also changes your rating: refresh the reviews
    // (they carry your rating) so the review form and the dialog prefill follow.
    const handleDiarySaved = (entry) => {
        if (entry.rating !== myRating) setReviewsKey((prev) => prev + 1);
    };

    const share = async () => {
        const url = window.location.href;
        try {
            if (navigator.share) {
                await navigator.share({ title: movie.title, url });
                return;
            }
            await navigator.clipboard.writeText(url);
            showSnackbar(t('movie.linkCopied'), 'success');
        } catch (err) {
            if (err?.name !== 'AbortError') showSnackbar(t('movie.shareFailed'), 'error');
        }
    };

    if (isLoading) return <HeroSkeleton />;
    if (error) return <ErrorState message={error} onRetry={fetchMovieDetails} />;
    if (!movie) return null;

    const year = movie.release_date ? movie.release_date.slice(0, 4) : null;
    const tmdbScore = movie.vote_average ? formatScore(movie.vote_average, i18n.language) : null;
    const backdrop = tmdbImage(movie.backdrop_path, 'w1280');
    const bg = theme.palette.tokens.bg;

    return (
        <>
            {/* Full-bleed backdrop hero; the scrim keeps text above 4.5:1. */}
            <Box component="section" aria-labelledby="movie-title" sx={{ position: 'relative' }} data-testid="movie-hero">
                <Box
                    aria-hidden="true"
                    sx={{
                        position: 'absolute', inset: 0, height: { xs: 300, md: 480 }, overflow: 'hidden',
                        backgroundColor: 'surface.raised',
                        backgroundImage: backdrop ? `url(${backdrop})` : undefined,
                        backgroundSize: 'cover', backgroundPosition: 'center 25%',
                        '&::after': { content: '""', position: 'absolute', inset: 0, background: backdropScrim(bg) },
                    }}
                />
                <Page sx={{ position: 'relative', pt: { xs: 18, md: 30 }, pb: { xs: 2, md: 3 } }}>
                    <Box sx={{ display: 'flex', gap: { xs: 2, md: 4 }, alignItems: { xs: 'flex-end', md: 'flex-end' } }}>
                        <Box sx={{ width: { xs: 112, sm: 150, md: 220 }, flexShrink: 0, boxShadow: '0 16px 40px rgba(0,0,0,.45)', borderRadius: 1.5 }}>
                            <PosterImage path={movie.poster_path} title={movie.title} width={220} eager />
                        </Box>
                        <Box sx={{ minWidth: 0, flex: 1, pb: { md: 1 } }}>
                            <Typography id="movie-title" variant="display" component="h1" sx={{ fontSize: { xs: '1.75rem', sm: '2.25rem', md: '2.75rem' }, lineHeight: 1.1, overflowWrap: 'anywhere' }}>
                                {movie.title}
                            </Typography>
                            <Typography variant="body1" color="text.secondary" sx={{ mt: 1 }} className="tabular-nums">
                                {[year, movie.runtime ? t('movie.runtime', { count: movie.runtime }) : null].filter(Boolean).join(' · ')}
                            </Typography>
                            {movie.genres?.length > 0 && (
                                <Box component="ul" aria-label={t('movie.genres')} sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, listStyle: 'none', p: 0, m: 0, mt: 1.5 }}>
                                    {movie.genres.map((genre) => <li key={genre.id}><Chip label={genre.name} size="small" /></li>)}
                                </Box>
                            )}
                        </Box>
                    </Box>
                </Page>
            </Box>

            <Page sx={{ pt: { xs: 1, md: 1 } }}>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 2, mb: 3 }}>
                    <Box sx={{ display: 'flex', gap: 1.5 }} data-testid="movie-scores">
                        {forYou && (
                            <ScoreTile
                                testId="for-you-score"
                                value={formatScore(forYou.score, i18n.language)}
                                label={t('movie.forYou')}
                                hint={t('movie.forYouSr', { score: formatScore(forYou.score, i18n.language) })}
                                tooltip={t('movie.forYouTooltip', {
                                    count: forYou.basedOn,
                                    matches: forYou.topMatches.map((m) => t('movie.forYouMatch', { userName: m.author.userName, compatibility: m.compatibility, rating: m.rating })).join(', '),
                                })}
                            />
                        )}
                        {tmdbScore && (
                            <ScoreTile testId="tmdb-score" value={tmdbScore} label="TMDb" hint={t('movie.tmdbSr', { score: tmdbScore })} />
                        )}
                    </Box>
                    <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }} data-testid="movie-actions">
                        <Button variant="contained" startIcon={<StarRoundedIcon />} onClick={() => setIsRatingModalOpen(true)} className="tabular-nums">
                            {myRating ? t('movie.yourRating', { rating: myRating }) : t('movie.rateThis')}
                        </Button>
                        <Button variant="tonal" startIcon={<EditCalendarRoundedIcon />} onClick={() => setIsDiaryOpen(true)}>{t('diary.addToDiary')}</Button>
                        <Button variant="tonal" startIcon={<PlaylistAddRoundedIcon />} onClick={handleOpenWatchlistModal}>{t('movie.addToList')}</Button>
                        <Button variant="tonal" startIcon={<IosShareRoundedIcon />} onClick={share}>{t('movie.share')}</Button>
                    </Box>
                </Box>

                <Box sx={{ maxWidth: layout.readingWidth }}>
                    {movie.tagline && <Typography variant="h3" component="p" sx={{ fontStyle: 'italic', color: 'text.secondary', mb: 1 }}>“{movie.tagline}”</Typography>}
                    {movie.overview && <Typography variant="body1" sx={{ fontSize: '1rem', lineHeight: 1.6 }}>{movie.overview}</Typography>}
                    {movie.release_date && (
                        <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
                            {t('movie.releaseDateLabel')} {new Date(movie.release_date).toLocaleDateString(i18n.language, { day: 'numeric', month: 'long', year: 'numeric' })}
                        </Typography>
                    )}
                </Box>

                <CastRow cast={movie.credits?.cast?.slice(0, 12)} />

                <Box sx={{ maxWidth: layout.readingWidth }}>
                    <MovieReviews movieId={movie.id} refreshKey={reviewsKey} onForYou={setForYou} onMyRating={setMyRating} />
                </Box>
            </Page>

            <RatingModal movie={movie} open={isRatingModalOpen} initialRating={myRating ?? 0} onClose={() => setIsRatingModalOpen(false)} onRate={handleRateMovie} />
            <DiaryEntryDialog
                open={isDiaryOpen}
                onClose={() => setIsDiaryOpen(false)}
                movie={movie}
                initialRating={myRating}
                onSaved={handleDiarySaved}
            />

            <Dialog open={isWatchlistModalOpen} onClose={() => setIsWatchlistModalOpen(false)} maxWidth="xs" fullWidth aria-labelledby="which-list-title">
                <DialogTitle id="which-list-title">{t('movie.whichList')}</DialogTitle>
                <DialogContent sx={{ px: 1.5 }}>
                    <List>
                        {listsError ? (
                            <ErrorState compact message={t('movie.listsLoadFailed')} onRetry={loadUserLists} />
                        ) : userLists === null ? (
                            <LoadingState compact />
                        ) : userLists.length > 0 ? userLists.map((list) => (
                            <ListItem key={list.id} disablePadding>
                                <ListItemButton onClick={() => handleAddMovieToList(list.id)} sx={{ borderRadius: 1 }}>
                                    <ListItemText primary={list.name} secondary={t('watchlist.movieCount', { count: list.itemCount ?? 0 })} />
                                </ListItemButton>
                            </ListItem>
                        )) : (
                            <EmptyState compact icon={PlaylistAddRoundedIcon} title={t('movie.noLists')} />
                        )}
                        <Divider sx={{ my: 1 }} />
                        <ListItem disablePadding>
                            <ListItemButton component={Link} to="/watchlist" sx={{ borderRadius: 1 }}>
                                <ListItemText primary={t('movie.manageLists')} />
                            </ListItemButton>
                        </ListItem>
                    </List>
                </DialogContent>
            </Dialog>
        </>
    );
};
