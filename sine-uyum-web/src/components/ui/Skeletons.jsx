import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Skeleton } from '@mui/material';
import { POSTER_CARD_WIDTHS } from './posterUtils';

// Loading placeholders for every list, grid and feed (no bare spinners in
// content areas). The wrappers announce "Loading" once; the shapes are hidden.
const Busy = ({ children, sx, testId = 'skeleton' }) => {
    const { t } = useTranslation();
    return (
        <Box role="status" aria-label={t('common.loading')} aria-busy="true" data-testid={testId} sx={sx}>
            <Box aria-hidden="true" sx={{ display: 'contents' }}>{children}</Box>
        </Box>
    );
};

export const PosterCardSkeleton = ({ size = 'M', fluid = false, meta = true }) => (
    <Box sx={{ width: fluid ? '100%' : POSTER_CARD_WIDTHS[size], flexShrink: 0 }}>
        <Skeleton variant="rounded" sx={{ width: '100%', height: 'auto', aspectRatio: '2 / 3', borderRadius: size === 'S' ? 1 : 1.5 }} />
        <Skeleton variant="text" sx={{ mt: 1, width: '85%', fontSize: '0.875rem' }} />
        {meta && <Skeleton variant="text" sx={{ width: '40%', fontSize: '0.8125rem' }} />}
    </Box>
);

export const MovieRowSkeleton = ({ count = 8, size = 'M', title = true }) => (
    <Busy testId="movie-row-skeleton" sx={{ mb: 4 }}>
        {title && <Skeleton variant="text" sx={{ width: 180, fontSize: '1.375rem', mb: 1.5 }} />}
        <Box sx={{ display: 'flex', gap: 2, overflow: 'hidden' }}>
            {Array.from({ length: count }, (_, i) => <PosterCardSkeleton key={i} size={size} />)}
        </Box>
    </Busy>
);

export const PosterGridSkeleton = ({ count = 12, min = 140 }) => (
    <Busy testId="poster-grid-skeleton" sx={{ display: 'grid', gridTemplateColumns: `repeat(auto-fill, minmax(${min}px, 1fr))`, gap: 2 }}>
        {Array.from({ length: count }, (_, i) => <PosterCardSkeleton key={i} fluid />)}
    </Busy>
);

export const FeedItemSkeleton = () => (
    <Box sx={{ display: 'flex', gap: 2, p: 2, borderRadius: 1.5, bgcolor: 'background.paper', border: 1, borderColor: 'divider' }}>
        <Skeleton variant="rounded" sx={{ width: 72, height: 108, flexShrink: 0, borderRadius: 1 }} />
        <Box sx={{ flex: 1, minWidth: 0 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Skeleton variant="circular" width={32} height={32} />
                <Skeleton variant="text" sx={{ width: '40%', fontSize: '0.9375rem' }} />
            </Box>
            <Skeleton variant="text" sx={{ mt: 1.5, width: '60%', fontSize: '1rem' }} />
            <Skeleton variant="text" sx={{ width: '90%' }} />
            <Skeleton variant="text" sx={{ width: '75%' }} />
        </Box>
    </Box>
);

export const FeedSkeleton = ({ count = 4 }) => (
    <Busy testId="feed-skeleton" sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {Array.from({ length: count }, (_, i) => <FeedItemSkeleton key={i} />)}
    </Busy>
);

export const UserRowSkeleton = () => (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, py: 1.5 }}>
        <Skeleton variant="circular" width={48} height={48} />
        <Box sx={{ flex: 1 }}>
            <Skeleton variant="text" sx={{ width: '35%', fontSize: '1rem' }} />
            <Skeleton variant="text" sx={{ width: '55%' }} />
        </Box>
        <Skeleton variant="rounded" sx={{ width: 96, height: 36, borderRadius: 999 }} />
    </Box>
);

export const UserListSkeleton = ({ count = 5 }) => (
    <Busy testId="user-list-skeleton">
        {Array.from({ length: count }, (_, i) => <UserRowSkeleton key={i} />)}
    </Busy>
);

export const ListCardSkeleton = () => (
    <Box sx={{ borderRadius: 1.5, overflow: 'hidden', bgcolor: 'background.paper', border: 1, borderColor: 'divider' }}>
        <Skeleton variant="rectangular" sx={{ width: '100%', height: 'auto', aspectRatio: '1 / 1' }} />
        <Box sx={{ p: 2 }}>
            <Skeleton variant="text" sx={{ width: '70%', fontSize: '1rem' }} />
            <Skeleton variant="text" sx={{ width: '45%' }} />
        </Box>
    </Box>
);

export const ListGridSkeleton = ({ count = 8 }) => (
    <Busy testId="list-grid-skeleton" sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: 2.5 }}>
        {Array.from({ length: count }, (_, i) => <ListCardSkeleton key={i} />)}
    </Busy>
);

// A generic card: a title line and a few lines of text.
export const CardSkeleton = ({ lines = 3, height }) => (
    <Busy testId="card-skeleton">
        <Box sx={{ p: 2.5, borderRadius: 1.5, bgcolor: 'background.paper', border: 1, borderColor: 'divider', minHeight: height }}>
            <Skeleton variant="text" sx={{ width: '45%', fontSize: '1.25rem' }} />
            {Array.from({ length: lines }, (_, i) => <Skeleton key={i} variant="text" sx={{ width: `${90 - i * 12}%` }} />)}
        </Box>
    </Busy>
);
