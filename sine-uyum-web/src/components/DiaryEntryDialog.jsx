import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Rating, Switch, TextField, Typography } from '@mui/material';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { EARLIEST_WATCHED_ON, todayIsoDay } from './diaryDates';

const DIARY_NOTE_MAX = 500;

// The form lives in its own component: the Dialog unmounts its content when
// closed, so every opening starts from fresh state.
const DiaryEntryForm = ({ movie, entry, initialRating, onClose, onSaved }) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const isEdit = Boolean(entry);
    const today = todayIsoDay();
    const [watchedOn, setWatchedOn] = useState(entry?.watchedOn ?? today);
    const [rewatch, setRewatch] = useState(entry?.rewatch ?? false);
    const [note, setNote] = useState(entry?.note ?? '');
    const [rating, setRating] = useState(initialRating ?? null);
    const [isSaving, setIsSaving] = useState(false);

    // "yyyy-MM-dd" strings compare correctly as text.
    const dateError = !watchedOn
        ? t('diary.dateRequired')
        : watchedOn > today
            ? t('diary.dateInFuture')
            : watchedOn < EARLIEST_WATCHED_ON
                ? t('diary.dateTooEarly')
                : '';

    const save = async (e) => {
        e.preventDefault();
        if (dateError) return;
        setIsSaving(true);
        const trimmedNote = note.trim() || null;
        try {
            let res;
            if (isEdit) {
                res = await api.put(`/api/v1/diary/${entry.id}`, { watchedOn, rewatch, note: trimmedNote });
            } else {
                // The rating is only sent when it changed, so logging a movie
                // does not rewrite an unchanged rating.
                const ratingChanged = rating != null && rating !== initialRating;
                res = await api.post('/api/v1/diary', {
                    movieId: movie.id,
                    watchedOn,
                    rewatch,
                    note: trimmedNote,
                    ...(ratingChanged ? { rating } : {}),
                });
            }
            showSnackbar(isEdit ? t('diary.updated') : t('diary.logged', { title: movie?.title ?? res.data.movie?.title }), 'success');
            onSaved?.(res.data);
            onClose();
        } catch (err) {
            showSnackbar(err.response?.data?.title || t('diary.saveFailed'), 'error');
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <Box component="form" onSubmit={save} noValidate data-testid="diary-entry-form">
            <DialogTitle>
                {isEdit ? t('diary.editTitle', { title: entry.movie.title }) : t('diary.logTitle', { title: movie.title })}
            </DialogTitle>
            <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <TextField
                    type="date"
                    label={t('diary.watchedOn')}
                    value={watchedOn}
                    onChange={(e) => setWatchedOn(e.target.value)}
                    error={Boolean(dateError)}
                    helperText={dateError || ' '}
                    required
                    fullWidth
                    sx={{ mt: 1 }}
                    slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: today, min: EARLIEST_WATCHED_ON } }}
                />
                <FormControlLabel
                    control={<Switch checked={rewatch} onChange={(e) => setRewatch(e.target.checked)} />}
                    label={t('diary.rewatch')}
                />
                <TextField
                    label={t('diary.note')}
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                    multiline
                    minRows={3}
                    fullWidth
                    helperText={t('diary.noteCounter', { length: note.length, max: DIARY_NOTE_MAX })}
                    slotProps={{ htmlInput: { maxLength: DIARY_NOTE_MAX }, formHelperText: { sx: { textAlign: 'right' } } }}
                />
                {!isEdit && (
                    <Box>
                        <Typography component="legend" variant="body2" color="text.secondary">{t('diary.ratingOptional')}</Typography>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                            <Rating
                                name="diary-rating"
                                value={rating}
                                max={10}
                                onChange={(_, value) => setRating(value)}
                            />
                            <Typography variant="body2">{rating ? t('diary.ratingValue', { rating }) : t('diary.noRating')}</Typography>
                        </Box>
                    </Box>
                )}
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose}>{t('common.cancel')}</Button>
                <Button type="submit" variant="contained" disabled={isSaving || Boolean(dateError)}>
                    {isSaving ? t('common.processing') : isEdit ? t('common.save') : t('diary.logIt')}
                </Button>
            </DialogActions>
        </Box>
    );
};

// Log a movie to the diary (movie + initialRating), or edit an entry (entry;
// the rating is not part of an entry, so it is not shown then).
export const DiaryEntryDialog = ({ open, onClose, movie, entry, initialRating, onSaved }) => (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
        <DiaryEntryForm movie={movie} entry={entry} initialRating={initialRating} onClose={onClose} onSaved={onSaved} />
    </Dialog>
);
