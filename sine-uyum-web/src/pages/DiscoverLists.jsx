import React, { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, IconButton, InputAdornment, Tab, Tabs, TextField, Typography } from '@mui/material';
import SearchIcon from '@mui/icons-material/SearchRounded';
import ClearIcon from '@mui/icons-material/CloseRounded';
import SearchOffIcon from '@mui/icons-material/SearchOffRounded';
import WhatshotIcon from '@mui/icons-material/WhatshotRounded';
import PlaylistAddIcon from '@mui/icons-material/PlaylistAddRounded';
import FavoriteBorderIcon from '@mui/icons-material/FavoriteBorderRounded';
import { ListGridSkeleton } from '../components/ui/Skeletons';
import { segmentedTabsSx } from '../components/ui/segmentedTabs';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { ListCardGrid } from '../components/ListCard';
import { DISCOVER_SORTS, usePagedLists, useListReactions } from '../components/publicLists';

const SEARCH_DEBOUNCE_MS = 400;

// Public lists by others: this week's trending, all-time popular, newly
// published, and lists by people with a similar taste (Discover › Lists).
// The sort and the search live in the URL, so going back keeps them.
export const DiscoverLists = () => {
    const { t } = useTranslation();
    const [searchParams, setSearchParams] = useSearchParams();
    const sortParam = searchParams.get('sort');
    const sort = DISCOVER_SORTS.includes(sortParam) ? sortParam : 'trending';
    const query = (searchParams.get('q') || '').trim();
    const [searchInput, setSearchInput] = useState(searchParams.get('q') || '');

    // Debounced: the URL (and so the request) follows the box after a pause.
    useEffect(() => {
        const next = searchInput.trim();
        if (next === query) return undefined;
        const timer = setTimeout(() => {
            setSearchParams((current) => {
                const params = new URLSearchParams(current);
                if (next) params.set('q', next); else params.delete('q');
                return params;
            }, { replace: true });
        }, SEARCH_DEBOUNCE_MS);
        return () => clearTimeout(timer);
    }, [searchInput, query, setSearchParams]);

    const params = useMemo(() => (query ? { sort, q: query } : { sort }), [sort, query]);
    const { items, status, hasMore, isLoadingMore, loadMore, updateList, reload } = usePagedLists('/api/v1/watchlist/discover', params);
    const toggle = useListReactions(updateList);

    const changeSort = (event, next) => {
        setSearchParams((current) => {
            const params = new URLSearchParams(current);
            if (next === 'trending') params.delete('sort'); else params.set('sort', next);
            return params;
        }, { replace: true });
    };

    const clearSearch = () => {
        setSearchInput('');
        setSearchParams((current) => {
            const params = new URLSearchParams(current);
            params.delete('q');
            return params;
        }, { replace: true });
    };

    const renderEmpty = () => {
        if (query) {
            return (
                <EmptyState
                    icon={SearchOffIcon}
                    title={t('lists.discover.noResults', { query })}
                    description={t('lists.discover.noResultsHint')}
                    action={<Button variant="tonal" onClick={clearSearch}>{t('lists.discover.clearSearch')}</Button>}
                />
            );
        }
        if (sort === 'forYou') {
            return (
                <EmptyState
                    icon={FavoriteBorderIcon}
                    title={t('lists.discover.empty.forYouTitle')}
                    description={t('lists.discover.empty.forYouBody')}
                    action={<Button variant="contained" component={Link} to="/discover">{t('lists.discover.empty.forYouAction')}</Button>}
                />
            );
        }
        if (sort === 'trending') {
            return (
                <EmptyState
                    icon={WhatshotIcon}
                    title={t('lists.discover.empty.trendingTitle')}
                    description={t('lists.discover.empty.trendingBody')}
                    action={<Button variant="tonal" onClick={() => changeSort(null, 'popular')}>{t('lists.discover.empty.trendingAction')}</Button>}
                />
            );
        }
        return (
            <EmptyState
                icon={PlaylistAddIcon}
                title={t('lists.discover.empty.noneTitle')}
                description={t('lists.discover.empty.noneBody')}
                action={<Button variant="contained" component={Link} to="/watchlist">{t('lists.discover.empty.noneAction')}</Button>}
            />
        );
    };

    return (
        <Box data-testid="discover-lists">
            <Typography variant="body1" color="text.secondary" sx={{ mb: 2 }}>{t('lists.discover.subtitle')}</Typography>

            <Box sx={{ display: 'flex', alignItems: { xs: 'stretch', md: 'center' }, flexDirection: { xs: 'column', md: 'row' }, gap: 2, mb: 1 }}>
                <Tabs
                    value={sort}
                    onChange={changeSort}
                    variant="scrollable"
                    scrollButtons="auto"
                    allowScrollButtonsMobile
                    aria-label={t('lists.discover.tabsLabel')}
                    sx={[segmentedTabsSx, { flex: '1 1 auto', minWidth: 0 }]}
                >
                    {DISCOVER_SORTS.map((value) => (
                        <Tab key={value} value={value} label={t(`lists.discover.tabs.${value}`)} />
                    ))}
                </Tabs>
                <TextField
                    size="small"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    placeholder={t('lists.discover.searchPlaceholder')}
                    sx={{ flex: { md: '0 1 320px' }, width: { xs: '100%', md: 'auto' } }}
                    slotProps={{
                        htmlInput: { 'aria-label': t('lists.discover.searchLabel'), type: 'search', enterKeyHint: 'search' },
                        input: {
                            startAdornment: (
                                <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment>
                            ),
                            endAdornment: searchInput ? (
                                <InputAdornment position="end">
                                    <IconButton size="small" edge="end" onClick={clearSearch} aria-label={t('lists.discover.clearSearch')}>
                                        <ClearIcon fontSize="small" />
                                    </IconButton>
                                </InputAdornment>
                            ) : null,
                        },
                    }}
                />
            </Box>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }} data-testid="sort-hint">
                {t(`lists.discover.hints.${sort}`)}
            </Typography>

            {status === 'loading' ? (
                <ListGridSkeleton />
            ) : status === 'error' ? (
                <ErrorState message={t('lists.discover.loadError')} onRetry={reload} />
            ) : items.length === 0 ? (
                renderEmpty()
            ) : (
                <>
                    <ListCardGrid lists={items} onToggle={toggle} />
                    {hasMore && (
                        <Button variant="tonal" onClick={loadMore} disabled={isLoadingMore} sx={{ mt: 3, display: 'flex', mx: 'auto' }}>
                            {isLoadingMore ? t('common.loadingEllipsis') : t('common.loadMore')}
                        </Button>
                    )}
                </>
            )}
        </Box>
    );
};
