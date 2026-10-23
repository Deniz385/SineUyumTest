import React from 'react';
import { Box, Dialog, DialogContent, DialogTitle, IconButton, Tooltip } from '@mui/material';
import { useTranslation } from 'react-i18next';
import MovieFilterIcon from '@mui/icons-material/MovieFilterRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import { EmptyState } from './states/EmptyState';
import { PosterGridSkeleton } from './ui/Skeletons';
import { PosterCard } from './ui/PosterCard';
import { fromTmdb } from './ui/posterUtils';

// Films both of you would likely enjoy (from the compatibility API).
export const RecommendationModal = ({ open, onClose, recommendations, isLoading, targetUserName }) => {
  const { t } = useTranslation();
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth aria-labelledby="recs-title">
      <DialogTitle id="recs-title" sx={{ pr: 7 }}>{t('recommendations.title', { userName: targetUserName })}</DialogTitle>
      <Tooltip title={t('common.close')}>
        <IconButton onClick={onClose} aria-label={t('common.close')} sx={{ position: 'absolute', right: 12, top: 12 }}><CloseRoundedIcon /></IconButton>
      </Tooltip>
      <DialogContent>
        {isLoading ? (
          <PosterGridSkeleton count={8} min={120} />
        ) : recommendations.length === 0 ? (
          <EmptyState compact icon={MovieFilterIcon} title={t('recommendations.emptyTitle')} description={t('recommendations.empty')} />
        ) : (
          <Box component="ul" sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))', gap: 2 }} onClick={(e) => { if (e.target.closest('a')) onClose(); }}>
            {recommendations.map((movie) => <li key={movie.id}><PosterCard movie={fromTmdb(movie)} fluid /></li>)}
          </Box>
        )}
      </DialogContent>
    </Dialog>
  );
};
