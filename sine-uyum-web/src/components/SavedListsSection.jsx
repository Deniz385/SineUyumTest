import React, { useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Typography } from '@mui/material';
import BookmarkBorderIcon from '@mui/icons-material/BookmarkBorder';
import ExploreIcon from '@mui/icons-material/Explore';
import { ListGridSkeleton } from './ui/Skeletons';
import { EmptyState } from './states/EmptyState';
import { ErrorState } from './states/ErrorState';
import { ListCardGrid } from './ListCard';
import { usePagedLists, useListReactions } from './publicLists';
import { useSnackbar } from '../context/SnackbarProvider';

// Others' public lists I saved (on the watchlists page). Unsaving one drops its card.
export const SavedListsSection = () => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const { items, status, hasMore, isLoadingMore, loadMore, updateList, removeList, reload } = usePagedLists('/api/v1/watchlist/saved');
    const onUnsaved = useCallback((list) => {
        removeList(list.id);
        showSnackbar(t('lists.saved.removed', { name: list.name }), 'info');
    }, [removeList, showSnackbar, t]);
    const toggle = useListReactions(updateList, { onUnsaved });

    return (
        <Box component="section" sx={{ mt: 6 }} aria-labelledby="saved-lists-title" data-testid="saved-lists">
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 2, flexWrap: 'wrap', mb: 2 }}>
                <Typography id="saved-lists-title" variant="h2" component="h2">{t('lists.saved.title')}</Typography>
                <Button component={Link} to="/discover?tab=lists" startIcon={<ExploreIcon />}>{t('lists.saved.discover')}</Button>
            </Box>
            {status === 'loading' ? (
                <ListGridSkeleton count={4} />
            ) : status === 'error' ? (
                <ErrorState compact message={t('lists.saved.loadError')} onRetry={reload} />
            ) : items.length === 0 ? (
                <EmptyState compact icon={BookmarkBorderIcon} title={t('lists.saved.emptyTitle')} description={t('lists.saved.emptyBody')} />
            ) : (
                <>
                    <ListCardGrid lists={items} onToggle={toggle} columns={{ xs: 12, sm: 6, md: 4 }} />
                    {hasMore && (
                        <Button variant="outlined" onClick={loadMore} disabled={isLoadingMore} sx={{ mt: 3 }}>
                            {isLoadingMore ? t('common.loadingEllipsis') : t('common.loadMore')}
                        </Button>
                    )}
                </>
            )}
        </Box>
    );
};
