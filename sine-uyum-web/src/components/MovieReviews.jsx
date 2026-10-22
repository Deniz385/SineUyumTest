import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { Alert, Box, Button, Checkbox, CircularProgress, FormControlLabel, Stack, TextField, Typography } from '@mui/material';
import { ReviewCard } from './ReviewCard';

const PAGE_SIZE = 20;

// Reviews on the movie page: write/edit/delete your own review, plus
// everyone else's (newest first).
export const MovieReviews = ({ movieId, refreshKey, onForYou }) => {
    const { t } = useTranslation();
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
            if (nextPage === 1) onForYou?.(res.data.forYou ?? null);
            setReviews((prev) => (nextPage === 1 ? res.data.reviews : [...prev, ...res.data.reviews]));
            setPage(nextPage);
            if (nextPage === 1) {
                setDraft({ text: res.data.mine?.text ?? '', containsSpoilers: res.data.mine?.containsSpoilers ?? false });
                setIsEditing(!res.data.mine);
            }
        } catch {
            setData((prev) => prev ?? { reviews: [], totalCount: 0 });
        }
    }, [movieId, onForYou]);

    useEffect(() => { load(1); }, [load, refreshKey]);

    const save = async (e) => {
        e.preventDefault();
        setIsSaving(true);
        try {
            const res = await api.put(`/api/v1/reviews/movie/${movieId}`, draft);
            setData((d) => ({ ...d, mine: res.data }));
            setIsEditing(false);
            showSnackbar(t('reviews.saved'), 'success');
        } catch (error) {
            showSnackbar(error.response?.data?.title || t('reviews.saveFailed'), 'error');
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
            showSnackbar(t('reviews.deleteFailed'), 'error');
        }
    };

    if (!data) return <CircularProgress sx={{ my: 3 }} />;

    return (
        <Box sx={{ mt: 5, textAlign: 'left' }} data-testid="movie-reviews">
            <Typography variant="h5" sx={{ fontWeight: 'bold', mb: 2 }}>
                {data.totalCount > 0 ? t('reviews.titleWithCount', { count: data.totalCount }) : t('reviews.title')}
            </Typography>

            {data.myRating == null ? (
                <Alert severity="info" sx={{ mb: 2 }}>{t('reviews.rateFirst')}</Alert>
            ) : isEditing ? (
                <Box component="form" onSubmit={save} sx={{ mb: 3 }}>
                    <TextField
                        label={t('reviews.yourReview', { rating: data.myRating })}
                        multiline minRows={3} fullWidth
                        value={draft.text}
                        inputProps={{ maxLength: 2000 }}
                        onChange={(e) => setDraft((d) => ({ ...d, text: e.target.value }))}
                    />
                    <Stack direction="row" alignItems="center" spacing={1} sx={{ mt: 1 }}>
                        <FormControlLabel
                            control={<Checkbox checked={draft.containsSpoilers} onChange={(e) => setDraft((d) => ({ ...d, containsSpoilers: e.target.checked }))} />}
                            label={t('reviews.containsSpoilers')}
                        />
                        <Box sx={{ flexGrow: 1 }} />
                        {data.mine && <Button onClick={() => setIsEditing(false)}>{t('reviews.discard')}</Button>}
                        <Button type="submit" variant="contained" disabled={isSaving || !draft.text.trim()}>
                            {data.mine ? t('common.update') : t('reviews.post')}
                        </Button>
                    </Stack>
                </Box>
            ) : data.mine ? (
                <Box sx={{ mb: 3 }}>
                    <ReviewCard review={data.mine} />
                    <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                        <Button size="small" onClick={() => setIsEditing(true)}>{t('common.edit')}</Button>
                        <Button size="small" color="error" onClick={remove}>{t('common.delete')}</Button>
                    </Stack>
                </Box>
            ) : null}

            <Stack spacing={2}>
                {reviews.map((r) => <ReviewCard key={r.id} review={r} />)}
            </Stack>
            {reviews.length === 0 && (
                <Typography color="text.secondary">{t('reviews.empty')}</Typography>
            )}
            {reviews.length < data.totalCount && (
                <Button sx={{ mt: 2 }} onClick={() => load(page + 1)}>{t('common.loadMore')}</Button>
            )}
        </Box>
    );
};
