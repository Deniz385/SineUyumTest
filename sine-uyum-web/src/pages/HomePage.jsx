import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Card, Typography } from '@mui/material';
import GroupAddRoundedIcon from '@mui/icons-material/GroupAddRounded';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { AuthorLine, ReviewCard } from '../components/ReviewCard';
import { TasteTwinCard } from '../components/TasteTwinCard';
import { YearInReviewBanner } from '../components/YearInReviewBanner';
import { ErrorState } from '../components/states/ErrorState';
import { Page, PageHeader } from '../components/ui/Page';
import { MovieRow, SectionHeader } from '../components/ui/MovieRow';
import { PosterCard } from '../components/ui/PosterCard';
import { FeedSkeleton } from '../components/ui/Skeletons';
import { PosterImage } from '../components/ui/PosterImage';
import { savePosterWall } from '../components/ui/posterWallCache';
import { useMovieCollections } from './discoverData';
import { layout } from '../theme';

// Fewer items than this and movie rows fill the page under the feed.
const THIN_FEED = 3;

const greetingKey = (hour) => (hour >= 5 && hour < 12 ? 'morning' : hour >= 12 && hour < 18 ? 'afternoon' : 'evening');

// "@ayse rated Past Lives ★★★★½ 9": poster-led, like a review card.
const RatedItem = ({ item }) => {
    const { t } = useTranslation();
    return (
        <Card component="article" data-testid="feed-rated" sx={{ p: 2, display: 'flex', gap: 2, alignItems: 'flex-start' }}>
            <Box component={Link} to={`/movie/${item.movie.id}`} tabIndex={-1} aria-hidden="true" sx={{ width: { xs: 64, sm: 84 }, flexShrink: 0, borderRadius: 1 }}>
                <PosterImage path={item.movie.posterPath} title={item.movie.title} width={84} radius="sm" decorative />
            </Box>
            <Box sx={{ flex: 1, minWidth: 0 }}>
                <AuthorLine author={item.author} rating={item.rating} compatibility={item.authorCompatibility} when={item.activityAt} relative>
                    <Typography variant="body2" color="text.secondary" component="span">{t('feed.ratedMovie')}</Typography>
                </AuthorLine>
                <Typography component={Link} to={`/movie/${item.movie.id}`} variant="title" sx={{ display: 'block', mt: 1, color: 'text.primary', '&:hover': { textDecoration: 'underline' } }}>
                    {item.movie.title}
                </Typography>
            </Box>
        </Card>
    );
};

// Movie rows for a thin feed (instead of an empty state).
const DiscoverRows = () => {
    const { t } = useTranslation();
    const { status, rows } = useMovieCollections();
    useEffect(() => {
        if (status === 'ready') savePosterWall(Object.values(rows).flat().map((m) => m.posterPath));
    }, [status, rows]);
    if (status === 'error') return null;
    const loading = status === 'loading';
    return (
        <Box data-testid="home-rows" sx={{ mt: 5 }}>
            <MovieRow title={t('discover.movies.popular')} movies={rows.popular} loading={loading} seeAllTo="/discover?section=popular" testId="row-popular" />
            <MovieRow title={t('discover.movies.nowPlaying')} movies={rows.now_playing} loading={loading} seeAllTo="/discover?section=now_playing" testId="row-now_playing" />
            <MovieRow title={t('discover.movies.topRated')} movies={rows.top_rated} loading={loading} seeAllTo="/discover?section=top_rated" testId="row-top_rated" />
        </Box>
    );
};

// Desktop side column: a few popular films next to a busy feed.
const SidePopular = () => {
    const { t } = useTranslation();
    const { status, rows } = useMovieCollections(['popular']);
    if (status !== 'ready' || !rows.popular?.length) return null;
    return (
        <Card component="section" aria-labelledby="side-popular" sx={{ p: 2, display: { xs: 'none', lg: 'block' } }}>
            <SectionHeader id="side-popular" title={t('discover.movies.popular')} variant="h3" component="h2" seeAllTo="/discover" sx={{ mb: 1 }} />
            <Box component="ul" sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 1.5 }}>
                {rows.popular.slice(0, 6).map((m) => <li key={m.id}><PosterCard movie={m} size="S" fluid /></li>)}
            </Box>
        </Card>
    );
};

/**
 * Home: a greeting, this week's taste twin and the yearly recap when there
 * is one, then the feed (ratings and reviews from the people you follow,
 * and your own). When the feed is thin, movie rows fill the page.
 */
export const HomePage = () => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const [items, setItems] = useState(null);
    const [cursor, setCursor] = useState(null);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [error, setError] = useState(false);
    const [greeting] = useState(() => greetingKey(new Date().getHours()));

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

    const thin = items !== null && !error && items.length < THIN_FEED && !cursor;

    return (
        <Page>
            <PageHeader
                title={t(`home.greeting.${greeting}`, { userName: user?.username })}
                subtitle={t('home.subtitle')}
            />
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', lg: `minmax(0, ${layout.readingWidth}px) minmax(280px, 1fr)` }, gap: { xs: 3, lg: 5 }, alignItems: 'start' }}>
            <Box sx={{ order: { xs: 1, lg: 2 }, display: 'flex', flexDirection: 'column', gap: 2, position: { lg: 'sticky' }, top: { lg: 88 }, '&:empty': { display: 'none' } }}>
                <TasteTwinCard />
                <YearInReviewBanner />
                {items !== null && !thin && <SidePopular />}
            </Box>

            <Box component="section" aria-labelledby="feed-heading" sx={{ order: { xs: 2, lg: 1 }, minWidth: 0 }}>
                <Typography id="feed-heading" variant="h2" component="h2" sx={{ mb: 2 }}>{t('feed.title')}</Typography>
                {items === null ? (
                    <FeedSkeleton />
                ) : items.length === 0 && error ? (
                    <ErrorState message={t('feed.loadError')} onRetry={retry} />
                ) : (
                    <>
                        {items.length === 0 && (
                            <Card data-testid="feed-hint" sx={{ p: 2.5, display: 'flex', gap: 2, alignItems: 'center', flexWrap: 'wrap' }}>
                                <Box aria-hidden="true" sx={{ width: 48, height: 48, borderRadius: '50%', bgcolor: 'surface.raised', color: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                                    <GroupAddRoundedIcon />
                                </Box>
                                <Box sx={{ flex: '1 1 240px', minWidth: 0 }}>
                                    <Typography variant="subtitle1" component="p">{t('feed.emptyTitle')}</Typography>
                                    <Typography variant="body1" color="text.secondary">{t('feed.thinBody')}</Typography>
                                </Box>
                                <Button component={Link} to="/discover?tab=people" variant="contained">{t('feed.findPeople')}</Button>
                            </Card>
                        )}
                        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }} data-testid="feed-list">
                            {items.map((item) => item.review
                                ? <ReviewCard key={`r-${item.review.id}`} review={item.review} showMovie relativeTime
                                    headline={<Typography variant="body2" color="text.secondary" component="span">{t('feed.wroteReview')}</Typography>} />
                                : <RatedItem key={`p-${item.author.id}-${item.movie.id}`} item={item} />)}
                        </Box>
                    </>
                )}
                {/* A failed "Load more" keeps what is already shown. */}
                {error && items?.length > 0 && !isLoadingMore && (
                    <ErrorState compact message={t('feed.loadError')} onRetry={cursor ? loadMore : retry} sx={{ mt: 2 }} />
                )}
                {cursor && (!error || isLoadingMore) && (
                    <Button variant="tonal" sx={{ mt: 3, display: 'flex', mx: 'auto' }} onClick={loadMore} disabled={isLoadingMore}>
                        {isLoadingMore ? t('common.loadingEllipsis') : t('common.loadMore')}
                    </Button>
                )}
            </Box>
            </Box>

            {thin && <DiscoverRows />}
        </Page>
    );
};
