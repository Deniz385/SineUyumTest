import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { Box, Button, Dialog, DialogActions, DialogContent, LinearProgress, Typography } from '@mui/material';
import ShareIcon from '@mui/icons-material/IosShareRounded';
import { gradients } from '../theme';
import { shareIdentityImage } from './identityImage';
import { LoadingState } from './states/LoadingState';
import { ErrorState } from './states/ErrorState';

// Film identity: the taste profile and title derived from ratings; shareable as an image.
export const FilmIdentityDialog = ({ open, onClose, userId, isMine }) => {
    const { t, i18n } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [identity, setIdentity] = useState(null);
    const [error, setError] = useState('');
    const [isSharing, setIsSharing] = useState(false);
    // Bumped by "Try again" to load once more.
    const [attempt, setAttempt] = useState(0);

    const fmt = (n) => Number(n).toLocaleString(i18n.language, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

    useEffect(() => {
        if (!open) return;
        setIdentity(null);
        setError('');
        api.get(`/api/v1/profile/${userId}/identity`)
            .then((res) => setIdentity(res.data))
            .catch((e) => setError(e.response?.data?.title || t('identity.loadFailed')));
    }, [open, userId, t, attempt]);

    const share = async () => {
        setIsSharing(true);
        try {
            const result = await shareIdentityImage(identity, isMine);
            if (result === 'downloaded') showSnackbar(t('identity.downloaded'), 'success');
        } catch (e) {
            // Closing the share sheet is not an error.
            if (e?.name !== 'AbortError') showSnackbar(t('identity.imageFailed'), 'error');
        } finally {
            setIsSharing(false);
        }
    };

    const take = identity?.contrarianTake;
    const takeValues = take && { title: take.title, average: fmt(take.tmdbAverage), rating: take.rating };

    return (
        <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
            <DialogContent sx={{ p: 0 }}>
                {error && <ErrorState compact message={error} onRetry={() => setAttempt((n) => n + 1)} sx={{ py: 3 }} />}
                {!identity && !error && <LoadingState compact sx={{ py: 4 }} />}
                {identity && (
                    <Box data-testid="film-identity" sx={{ p: 3, color: '#fff', background: 'radial-gradient(120% 80% at 0% 0%, #2A2210 0%, #14161C 55%, #0B0C10 100%)', borderBottom: '3px solid', borderImage: `${gradients.marquee} 1` }}>
                        <Typography variant="overline" sx={{ color: '#A9ADBA' }}>{t('identity.header', { userName: identity.userName })}</Typography>
                        <Typography variant="h1" component="h2" sx={{ color: '#F5B841', mt: 1 }}>{identity.title}</Typography>
                        <Typography sx={{ color: '#F2F2F5', mb: 2 }}>{identity.titleDescription}</Typography>
                        <Typography sx={{ fontWeight: 600 }}>{t('identity.summary', { count: identity.totalRatings, average: fmt(identity.averageRating) })}</Typography>
                        {identity.topGenres.map((g) => (
                            <Box key={g.name} sx={{ mt: 1.5 }}>
                                <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                                    <Typography variant="body2">{g.name}</Typography>
                                    <Typography variant="body2">{t('identity.genreStat', { share: g.share, average: fmt(g.average) })}</Typography>
                                </Box>
                                <LinearProgress variant="determinate" value={g.share} sx={{ height: 8, borderRadius: 4, bgcolor: 'rgba(255,255,255,0.12)', '& .MuiLinearProgress-bar': { bgcolor: '#F5B841' } }} />
                            </Box>
                        ))}
                        {take && (
                            <Box sx={{ mt: 2 }}>
                                <Typography variant="overline" sx={{ color: '#A9ADBA' }}>{isMine ? t('identity.contrarianMine') : t('identity.contrarianTheirs')}</Typography>
                                <Typography>{isMine ? t('identity.contrarianLineMine', takeValues) : t('identity.contrarianLineTheirs', takeValues)}</Typography>
                            </Box>
                        )}
                        {identity.favorite && (
                            <Box sx={{ mt: 1 }}>
                                <Typography variant="overline" sx={{ color: '#A9ADBA' }}>{isMine ? t('identity.favoriteMine') : t('identity.favoriteTheirs')}</Typography>
                                <Typography>{identity.favorite.title} · {identity.favorite.rating}/10</Typography>
                            </Box>
                        )}
                    </Box>
                )}
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose} color="inherit">{t('common.close')}</Button>
                <Button variant="contained" startIcon={<ShareIcon />} disabled={!identity || isSharing} onClick={share}>
                    {t('identity.share')}
                </Button>
            </DialogActions>
        </Dialog>
    );
};
