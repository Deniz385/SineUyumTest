import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { Box, Button, Card, CardContent, Stack, Typography } from '@mui/material';
import DynamicFeedIcon from '@mui/icons-material/DynamicFeed';
import { AuthorLine, ReviewCard } from '../components/ReviewCard';
import { TasteTwinCard } from '../components/TasteTwinCard';
import { YearInReviewBanner } from '../components/YearInReviewBanner';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';

const POSTER = 'https://image.tmdb.org/t/p/w92';

const RatedItem = ({ item }) => {
    const { t } = useTranslation();
    return (
        <Card variant="outlined">
            <CardContent>
                <AuthorLine author={item.author} rating={item.rating} compatibility={item.authorCompatibility} when={item.activityAt}>
                    <Typography variant="body2" color="text.secondary">{t('feed.ratedMovie')}</Typography>
                </AuthorLine>
                <Box component={Link} to={`/movie/${item.movie.id}`}
                    sx={{ display: 'flex', gap: 1.5, alignItems: 'center', mt: 1.5, color: 'inherit', textDecoration: 'none' }}>
                    {item.movie.posterPath && <img src={`${POSTER}${item.movie.posterPath}`} alt="" style={{ width: 46, borderRadius: 4 }} />}
                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{item.movie.title}</Typography>
                </Box>
            </CardContent>
        </Card>
    );
};

// Ratings and reviews from the people you follow (and your own).
export const FeedPage = () => {
    const { t } = useTranslation();
    const [items, setItems] = useState(null);
    const [cursor, setCursor] = useState(null);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [error, setError] = useState(false);

    const load = useCallback(async (from) => {
        try {
            const res = await api.get('/api/v1/feed', { params: from ? { cursor: from } : {} });
            setItems((prev) => (from ? [...(prev ?? []), ...res.data.items] : res.data.items));
            setCursor(res.data.nextCursor);
            setError(false);
        } catch {
            setError(true);
            setItems((prev) => prev ?? []);
        }
    }, []);

    useEffect(() => { load(null); }, [load]);

    const loadMore = async () => {
        setIsLoadingMore(true);
        await load(cursor);
        setIsLoadingMore(false);
    };

    const retry = () => {
        setError(false);
        setItems(null);
        load(null);
    };

    if (items === null) return <LoadingState />;

    return (
        <Box className="page-container" sx={{ maxWidth: 720, width: '100%' }}>
            <Typography variant="h4" component="h1" gutterBottom>{t('feed.title')}</Typography>
            <YearInReviewBanner />
            <TasteTwinCard />
            {items.length === 0 && error ? (
                <ErrorState message={t('feed.loadError')} onRetry={retry} />
            ) : items.length === 0 ? (
                <EmptyState
                    icon={DynamicFeedIcon}
                    title={t('feed.emptyTitle')}
                    description={t('feed.emptyBody')}
                    action={<Button component={Link} to="/home" variant="contained">{t('feed.browseMovies')}</Button>}
                />
            ) : (
                <Stack spacing={2}>
                    {items.map((item) => item.review
                        ? <ReviewCard key={`r-${item.review.id}`} review={item.review} showMovie
                            headline={<Typography variant="body2" color="text.secondary">{t('feed.wroteReview')}</Typography>} />
                        : <RatedItem key={`p-${item.author.id}-${item.movie.id}`} item={item} />)}
                </Stack>
            )}
            {/* A failed "Load more" keeps what is already shown. */}
            {error && items.length > 0 && !isLoadingMore && (
                <ErrorState compact message={t('feed.loadError')} onRetry={cursor ? loadMore : retry} sx={{ mt: 2 }} />
            )}
            {cursor && (!error || isLoadingMore) && (
                <Button sx={{ mt: 2 }} onClick={loadMore} disabled={isLoadingMore}>
                    {isLoadingMore ? t('common.loadingEllipsis') : t('common.loadMore')}
                </Button>
            )}
        </Box>
    );
};
