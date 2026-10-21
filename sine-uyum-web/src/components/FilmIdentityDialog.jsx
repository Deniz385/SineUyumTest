import React, { useEffect, useState } from 'react';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, LinearProgress, Typography } from '@mui/material';
import ShareIcon from '@mui/icons-material/Share';
import { shareIdentityImage } from './identityImage';

const fmt = (n) => Number(n).toLocaleString('tr-TR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

// Film kimliği: puanlardan çıkan zevk profili ve unvan; görsel olarak paylaşılır.
export const FilmIdentityDialog = ({ open, onClose, userId, isMine }) => {
    const { showSnackbar } = useSnackbar();
    const [identity, setIdentity] = useState(null);
    const [error, setError] = useState('');
    const [isSharing, setIsSharing] = useState(false);

    useEffect(() => {
        if (!open) return;
        setIdentity(null);
        setError('');
        api.get(`/api/v1/profile/${userId}/identity`)
            .then((res) => setIdentity(res.data))
            .catch((e) => setError(e.response?.data?.title || 'Film kimliği yüklenemedi.'));
    }, [open, userId]);

    const share = async () => {
        setIsSharing(true);
        try {
            const result = await shareIdentityImage(identity, isMine);
            if (result === 'downloaded') showSnackbar('Görsel indirildi; istediğin yerde paylaşabilirsin.', 'success');
        } catch (e) {
            // Kullanıcı paylaşım menüsünü kapattıysa hata sayılmaz.
            if (e?.name !== 'AbortError') showSnackbar('Görsel oluşturulamadı.', 'error');
        } finally {
            setIsSharing(false);
        }
    };

    return (
        <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
            <DialogContent sx={{ p: 0 }}>
                {error && <Typography color="error" sx={{ p: 3 }}>{error}</Typography>}
                {!identity && !error && <Box sx={{ p: 4, textAlign: 'center' }}><CircularProgress /></Box>}
                {identity && (
                    <Box data-testid="film-identity" sx={{ p: 3, color: '#fff', background: 'linear-gradient(135deg, #1b1038, #0d2a4a)' }}>
                        <Typography variant="overline" sx={{ color: '#9fb3ff' }}>Film kimliği · @{identity.userName}</Typography>
                        <Typography variant="h4" sx={{ color: '#ffd54f', fontWeight: 'bold', mt: 1 }}>{identity.title}</Typography>
                        <Typography sx={{ color: '#e3e8ff', mb: 2 }}>{identity.titleDescription}</Typography>
                        <Typography sx={{ fontWeight: 600 }}>{identity.totalRatings} film · ortalama {fmt(identity.averageRating)}</Typography>
                        {identity.topGenres.map((g) => (
                            <Box key={g.name} sx={{ mt: 1.5 }}>
                                <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                                    <Typography variant="body2">{g.name}</Typography>
                                    <Typography variant="body2">%{g.share} · ort. {fmt(g.average)}</Typography>
                                </Box>
                                <LinearProgress variant="determinate" value={g.share} sx={{ height: 8, borderRadius: 4, bgcolor: 'rgba(255,255,255,0.15)' }} />
                            </Box>
                        ))}
                        {identity.contrarianTake && (
                            <Box sx={{ mt: 2 }}>
                                <Typography variant="overline" sx={{ color: '#9fb3ff' }}>{isMine ? 'En aykırı puanın' : 'En aykırı puanı'}</Typography>
                                <Typography>{identity.contrarianTake.title}: herkes {fmt(identity.contrarianTake.tmdbAverage)}, {isMine ? 'sen' : 'o'} {identity.contrarianTake.rating}</Typography>
                            </Box>
                        )}
                        {identity.favorite && (
                            <Box sx={{ mt: 1 }}>
                                <Typography variant="overline" sx={{ color: '#9fb3ff' }}>{isMine ? 'Favorin' : 'Favorisi'}</Typography>
                                <Typography>{identity.favorite.title} · {identity.favorite.rating}/10</Typography>
                            </Box>
                        )}
                    </Box>
                )}
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose}>Kapat</Button>
                <Button variant="contained" startIcon={<ShareIcon />} disabled={!identity || isSharing} onClick={share}>
                    Görsel olarak paylaş
                </Button>
            </DialogActions>
        </Dialog>
    );
};
