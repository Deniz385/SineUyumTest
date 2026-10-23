import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from '@mui/material';
import { RatingInput } from './ui/RatingInput';
import { PosterImage } from './ui/PosterImage';

/**
 * Quick "rate this film" dialog: poster, title and the 10-point RatingInput
 * (focused, so arrow keys work at once). `movie` takes TMDb (`poster_path`)
 * or catalog (`posterPath`) shape; `initialRating` prefills it.
 */
export const RatingModal = ({ movie, open, onClose, onRate, initialRating = 0, saving = false }) => {
  const { t } = useTranslation();
  const [rating, setRating] = useState(initialRating || 0);
  const [prevOpen, setPrevOpen] = useState(open);

  // Each opening starts from the current rating.
  if (open !== prevOpen) {
    setPrevOpen(open);
    if (open) setRating(initialRating || 0);
  }

  const handleRate = () => {
    if (rating > 0) {
      onRate(movie, rating);
      onClose();
    }
  };

  const posterPath = movie?.poster_path ?? movie?.posterPath;

  return (
    <Dialog open={open} onClose={onClose} aria-labelledby="rating-dialog-title" maxWidth="xs" fullWidth>
      <DialogTitle id="rating-dialog-title" sx={{ pr: 3 }}>
        {t('rating.title', { title: movie?.title })}
      </DialogTitle>
      <DialogContent>
        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
          <Box sx={{ width: 72, flexShrink: 0 }}>
            <PosterImage path={posterPath} title={movie?.title} width={72} radius="sm" decorative />
          </Box>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{t('rating.hint')}</Typography>
            <RatingInput value={rating} onChange={setRating} autoFocus={open} size="medium" />
          </Box>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} color="inherit">{t('common.cancel')}</Button>
        <Button onClick={handleRate} variant="contained" disabled={rating === 0 || saving} data-testid="rating-save">
          {t('rating.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
