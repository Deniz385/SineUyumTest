import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, FormHelperText, Switch, TextField, Typography } from '@mui/material';
import { RatingInput } from './ui/RatingInput';
import { apiErrorMessage } from './ui/apiError';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { EARLIEST_WATCHED_ON, formatIsoDay, todayIsoDay } from './diaryDates';

const DIARY_NOTE_MAX = 500;

// The form lives in its own component: the Dialog unmounts its content when
// closed, so every opening starts from fresh state.
const DiaryEntryForm = ({ movie, entry, initialRating, onClose, onSaved }) => {
    const { t, i18n } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const isEdit = Boolean(entry);
    const today = todayIsoDay();
    const [watchedOn, setWatchedOn] = useState(entry?.watchedOn ?? today);
    const [rewatch, setRewatch] = useState(entry?.rewatch ?? false);
    const [note, setNote] = useState(entry?.note ?? '');
    const [rating, setRating] = useState(initialRating ?? null);
    const [isSaving, setIsSaving] = useState(false);
    // Day of my latest entry for this movie (create mode only).
    const [lastLoggedOn, setLastLoggedOn] = useState(null);
    // Once the switch is touched, a late lookup must not override the choice.
    const rewatchTouched = useRef(false);

    // Logging a movie that is already in my diary is most likely a rewatch:
    // "Rewatch" starts checked. The lookup is only a hint, so a failure is ignored.
    const movieId = movie?.id;
    useEffect(() => {
        if (isEdit || movieId == null) return undefined;
        let cancelled = false;
        api.get(`/api/v1/diary/movie/${movieId}`)
            .then((res) => {
                const latest = Array.isArray(res.data) ? res.data[0] : null;
                if (cancelled || !latest?.watchedOn) return;
                setLastLoggedOn(latest.watchedOn);
                if (!rewatchTouched.current) setRewatch(true);
            })
            .catch(() => {});
        return () => { cancelled = true; };
    }, [isEdit, movieId]);

    const changeRewatch = (checked) => {
        rewatchTouched.current = true;
        setRewatch(checked);
    };

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
            showSnackbar(apiErrorMessage(err, t('diary.saveFailed')), 'error');
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
                <Box>
                    <FormControlLabel
                        control={(
                            <Switch
                                checked={rewatch}
                                onChange={(e) => changeRewatch(e.target.checked)}
                                slotProps={{ input: { 'aria-describedby': lastLoggedOn ? 'diary-last-logged' : undefined } }}
                            />
                        )}
                        label={t('diary.rewatch')}
                    />
                    {lastLoggedOn && (
                        <FormHelperText id="diary-last-logged" data-testid="diary-last-logged" sx={{ mt: 0, mx: 0 }}>
                            {t('diary.lastLogged', { date: formatIsoDay(lastLoggedOn, i18n.language) })}
                        </FormHelperText>
                    )}
                </Box>
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
                        <Typography id="diary-rating-label" variant="body2" color="text.secondary" sx={{ mb: 0.75 }}>{t('diary.ratingOptional')}</Typography>
                        <RatingInput value={rating ?? 0} onChange={setRating} size="medium" label={t('diary.ratingOptional')} />
                    </Box>
                )}
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose} color="inherit">{t('common.cancel')}</Button>
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
