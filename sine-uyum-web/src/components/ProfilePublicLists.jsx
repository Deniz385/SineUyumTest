import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Typography } from '@mui/material';
import PublicIcon from '@mui/icons-material/Public';
import { LoadingState } from './states/LoadingState';
import { EmptyState } from './states/EmptyState';
import { ErrorState } from './states/ErrorState';
import { ListCardGrid } from './ListCard';
import { usePagedLists, useListReactions } from './publicLists';

// A profile's public lists. Hidden when the profile's details are not
// visible to me (403/404) and, on someone else's profile, when there are none.
export const ProfilePublicLists = ({ userId, isMine, enabled = true }) => {
    const { t } = useTranslation();
    const { items, status, errorStatus, hasMore, isLoadingMore, loadMore, updateList, reload } =
        usePagedLists(`/api/v1/watchlist/user/${userId}`, {}, { enabled });
    const toggle = useListReactions(updateList);

    if (!enabled) return null;
    if (status === 'error' && (errorStatus === 403 || errorStatus === 404)) return null;
    if (status === 'ready' && items.length === 0 && !isMine) return null;

    return (
        <Box component="section" sx={{ my: 4, textAlign: 'left' }} aria-labelledby="profile-public-lists-title" data-testid="profile-public-lists">
            <Typography id="profile-public-lists-title" variant="h5" component="h3" gutterBottom sx={{ fontWeight: 'bold' }}>
                {t('lists.profile.title')}
            </Typography>
            {status === 'loading' ? (
                <LoadingState compact />
            ) : status === 'error' ? (
                <ErrorState compact message={t('lists.profile.loadError')} onRetry={reload} />
            ) : items.length === 0 ? (
                <EmptyState
                    compact
                    icon={PublicIcon}
                    title={t('lists.profile.emptyMineTitle')}
                    description={t('lists.profile.emptyMineBody')}
                    action={<Button variant="outlined" component={Link} to="/watchlist">{t('lists.profile.emptyMineAction')}</Button>}
                />
            ) : (
                <>
                    <ListCardGrid lists={items} onToggle={toggle} />
                    {hasMore && (
                        <Box sx={{ textAlign: 'center' }}>
                            <Button variant="outlined" onClick={loadMore} disabled={isLoadingMore} sx={{ mt: 3 }}>
                                {isLoadingMore ? t('common.loadingEllipsis') : t('common.loadMore')}
                            </Button>
                        </Box>
                    )}
                </>
            )}
        </Box>
    );
};
