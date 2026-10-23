import React, { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Tab, Tabs, Typography } from '@mui/material';
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded';
import api from '../api/axiosConfig';
import { Page, PageHeader } from '../components/ui/Page';
import { MovieRow } from '../components/ui/MovieRow';
import { PosterCard } from '../components/ui/PosterCard';
import { fromTmdb } from '../components/ui/posterUtils';
import { MovieRowSkeleton, PosterGridSkeleton } from '../components/ui/Skeletons';
import { UserSearch } from '../components/UserSearch';
import { ErrorState } from '../components/states/ErrorState';
import { DiscoverLists } from './DiscoverLists';
import { DISCOVER_TABS, MOVIE_SECTIONS, dedupe, useMovieCollections, usePeopleSuggestions } from './discoverData';
import { PeopleList } from '../components/PeopleList';

const MoviesPanel = ({ section, onSection }) => {
    const { t } = useTranslation();
    const { status, rows, reload } = useMovieCollections();

    if (section) {
        return <MovieSectionGrid section={section} initial={rows[section.key]} onBack={() => onSection(null)} />;
    }
    if (status === 'loading') return (<><MovieRowSkeleton /><MovieRowSkeleton /><MovieRowSkeleton /></>);
    if (status === 'error') return <ErrorState message={t('home.loadError')} onRetry={reload} />;
    return MOVIE_SECTIONS.map((s) => (
        <MovieRow key={s.key} title={t(s.labelKey)} movies={rows[s.key]} onSeeAll={() => onSection(s.key)} testId={`row-${s.key}`} />
    ));
};

// "See all": the whole collection as a poster grid.
const MovieSectionGrid = ({ section, initial, onBack }) => {
    const { t } = useTranslation();
    const [movies, setMovies] = useState(initial ?? null);
    const [page, setPage] = useState(1);
    const [error, setError] = useState(false);
    const [loadingMore, setLoadingMore] = useState(false);

    const load = useCallback(async (nextPage) => {
        setError(false);
        try {
            const { data } = await api.get(section.url, nextPage > 1 ? { params: { page: nextPage } } : undefined);
            const fresh = (data?.results || []).map(fromTmdb);
            setMovies((prev) => dedupe([...(nextPage > 1 ? prev ?? [] : []), ...fresh]));
            setPage(nextPage);
        } catch {
            setError(true);
            setMovies((prev) => prev ?? []);
        }
    }, [section.url]);

    useEffect(() => {
        if (!initial) load(1);
        // Only on open; `initial` is a snapshot from the rows.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [load]);

    const more = async () => {
        setLoadingMore(true);
        await load(page + 1);
        setLoadingMore(false);
    };

    return (
        <Box component="section" aria-labelledby="discover-section-title">
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
                <Button startIcon={<ArrowBackRoundedIcon />} onClick={onBack} size="small" sx={{ color: 'text.secondary' }}>
                    {t('discover.movies.back')}
                </Button>
            </Box>
            <Typography id="discover-section-title" variant="h2" component="h2" sx={{ mb: 2 }}>{t(section.labelKey)}</Typography>
            {movies === null ? (
                <PosterGridSkeleton />
            ) : movies.length === 0 && error ? (
                <ErrorState message={t('home.loadError')} onRetry={() => load(1)} />
            ) : (
                <>
                    <Box
                        component="ul"
                        data-testid="poster-grid"
                        sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: { xs: 'repeat(3, minmax(0, 1fr))', sm: 'repeat(auto-fill, minmax(140px, 1fr))' }, gap: { xs: 1.5, sm: 2 } }}
                    >
                        {movies.map((m) => <li key={m.id}><PosterCard movie={m} fluid /></li>)}
                    </Box>
                    {section.pages && page < section.pages && (
                        <Button variant="tonal" onClick={more} disabled={loadingMore} sx={{ display: 'flex', mx: 'auto', mt: 3 }}>
                            {loadingMore ? t('common.loadingEllipsis') : t('common.loadMore')}
                        </Button>
                    )}
                </>
            )}
        </Box>
    );
};

const PeoplePanel = () => {
    const { t } = useTranslation();
    const { status, basis, items, reload } = usePeopleSuggestions();
    return (
        <Box sx={{ maxWidth: 720 }}>
            <UserSearch sx={{ mb: 3 }} />
            <Typography variant="h2" component="h2">{basis === 'taste' ? t('people.tasteTitle') : t('people.popularTitle')}</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1.5 }}>
                {basis === 'taste' ? t('people.tasteHint') : t('people.popularHint')}
            </Typography>
            <PeopleList status={status} items={items} reload={reload} />
        </Box>
    );
};

// Discover: movies (popular, now playing, top rated), public lists and
// people. The tab (and the lists' sort/search) live in the URL.
export const DiscoverPage = () => {
    const { t } = useTranslation();
    const [searchParams, setSearchParams] = useSearchParams();
    const tabParam = searchParams.get('tab');
    const tab = DISCOVER_TABS.includes(tabParam) ? tabParam : 'movies';
    const section = MOVIE_SECTIONS.find((s) => s.key === searchParams.get('section')) ?? null;

    const changeTab = (event, next) => {
        // Each tab keeps its own parameters; switching starts it fresh.
        setSearchParams(next === 'movies' ? {} : { tab: next }, { replace: true });
    };
    const openSection = (key) => {
        setSearchParams(key ? { section: key } : {});
        window.scrollTo({ top: 0 });
    };

    return (
        <Page>
            <PageHeader title={t('discover.title')} subtitle={t('discover.subtitle')} sx={{ mb: 1.5 }} />
            <Tabs value={tab} onChange={changeTab} aria-label={t('discover.tabsLabel')} sx={{ mb: 3, borderBottom: 1, borderColor: 'divider' }}>
                {DISCOVER_TABS.map((value) => (
                    <Tab key={value} value={value} label={t(`discover.tabs.${value}`)} id={`discover-tab-${value}`} aria-controls={`discover-panel-${value}`} />
                ))}
            </Tabs>
            <Box role="tabpanel" id={`discover-panel-${tab}`} aria-labelledby={`discover-tab-${tab}`}>
                {tab === 'movies' && <MoviesPanel section={section} onSection={openSection} />}
                {tab === 'lists' && <DiscoverLists />}
                {tab === 'people' && <PeoplePanel />}
            </Box>
        </Page>
    );
};
