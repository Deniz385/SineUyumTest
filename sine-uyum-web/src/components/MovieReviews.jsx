import React, { useCallback, useEffect, useState } from 'react';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { Alert, Box, Button, Checkbox, CircularProgress, FormControlLabel, Stack, TextField, Typography } from '@mui/material';
import { ReviewCard } from './ReviewCard';

const PAGE_SIZE = 20;

// Film sayfasındaki incelemeler: kendi incelemesini yaz/düzenle/sil ve
// başkalarının incelemeleri (en yeniden eskiye).
export const MovieReviews = ({ movieId, refreshKey }) => {
    const { showSnackbar } = useSnackbar();
    const [data, setData] = useState(null);
    const [reviews, setReviews] = useState([]);
    const [page, setPage] = useState(1);
    const [draft, setDraft] = useState({ text: '', containsSpoilers: false });
    const [isSaving, setIsSaving] = useState(false);
    const [isEditing, setIsEditing] = useState(false);

    const load = useCallback(async (nextPage = 1) => {
        try {
            const res = await api.get(`/api/v1/reviews/movie/${movieId}`, { params: { page: nextPage, pageSize: PAGE_SIZE } });
            setData(res.data);
            setReviews((prev) => (nextPage === 1 ? res.data.reviews : [...prev, ...res.data.reviews]));
            setPage(nextPage);
            if (nextPage === 1) {
                setDraft({ text: res.data.mine?.text ?? '', containsSpoilers: res.data.mine?.containsSpoilers ?? false });
                setIsEditing(!res.data.mine);
            }
        } catch {
            setData((prev) => prev ?? { reviews: [], totalCount: 0 });
        }
    }, [movieId]);

    useEffect(() => { load(1); }, [load, refreshKey]);

    const save = async (e) => {
        e.preventDefault();
        setIsSaving(true);
        try {
            const res = await api.put(`/api/v1/reviews/movie/${movieId}`, draft);
            setData((d) => ({ ...d, mine: res.data }));
            setIsEditing(false);
            showSnackbar('İncelemen kaydedildi.', 'success');
        } catch (error) {
            showSnackbar(error.response?.data?.title || 'İnceleme kaydedilemedi.', 'error');
        } finally {
            setIsSaving(false);
        }
    };

    const remove = async () => {
        try {
            await api.delete(`/api/v1/reviews/movie/${movieId}`);
            setData((d) => ({ ...d, mine: null }));
            setDraft({ text: '', containsSpoilers: false });
            setIsEditing(true);
        } catch {
            showSnackbar('İnceleme silinemedi.', 'error');
        }
    };

    if (!data) return <CircularProgress sx={{ my: 3 }} />;

    return (
        <Box sx={{ mt: 5, textAlign: 'left' }} data-testid="movie-reviews">
            <Typography variant="h5" sx={{ fontWeight: 'bold', mb: 2 }}>
                İncelemeler {data.totalCount > 0 && `(${data.totalCount})`}
            </Typography>

            {data.myRating == null ? (
                <Alert severity="info" sx={{ mb: 2 }}>İnceleme yazmak için önce filmi puanla.</Alert>
            ) : isEditing ? (
                <Box component="form" onSubmit={save} sx={{ mb: 3 }}>
                    <TextField
                        label={`İncelemen (puanın: ${data.myRating}/10)`}
                        multiline minRows={3} fullWidth
                        value={draft.text}
                        inputProps={{ maxLength: 2000 }}
                        onChange={(e) => setDraft((d) => ({ ...d, text: e.target.value }))}
                    />
                    <Stack direction="row" alignItems="center" spacing={1} sx={{ mt: 1 }}>
                        <FormControlLabel
                            control={<Checkbox checked={draft.containsSpoilers} onChange={(e) => setDraft((d) => ({ ...d, containsSpoilers: e.target.checked }))} />}
                            label="Spoiler içeriyor"
                        />
                        <Box sx={{ flexGrow: 1 }} />
                        {data.mine && <Button onClick={() => setIsEditing(false)}>Vazgeç</Button>}
                        <Button type="submit" variant="contained" disabled={isSaving || !draft.text.trim()}>
                            {data.mine ? 'Güncelle' : 'Paylaş'}
                        </Button>
                    </Stack>
                </Box>
            ) : data.mine ? (
                <Box sx={{ mb: 3 }}>
                    <ReviewCard review={data.mine} />
                    <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                        <Button size="small" onClick={() => setIsEditing(true)}>Düzenle</Button>
                        <Button size="small" color="error" onClick={remove}>Sil</Button>
                    </Stack>
                </Box>
            ) : null}

            <Stack spacing={2}>
                {reviews.map((r) => <ReviewCard key={r.id} review={r} />)}
            </Stack>
            {reviews.length === 0 && (
                <Typography color="text.secondary">Henüz başka inceleme yok. İlk yazan sen ol.</Typography>
            )}
            {reviews.length < data.totalCount && (
                <Button sx={{ mt: 2 }} onClick={() => load(page + 1)}>Daha fazla</Button>
            )}
        </Box>
    );
};
