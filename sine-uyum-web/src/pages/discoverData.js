// Data hooks of the Discover page (also used by Home and the welcome flow).
import { useCallback, useEffect, useState } from 'react';
import api from '../api/axiosConfig';
import { fromTmdb } from '../components/ui/posterUtils';

export const DISCOVER_TABS = ['movies', 'lists', 'people'];

// The three TMDb collections of the Movies tab. Only top rated pages further.
export const MOVIE_SECTIONS = [
    { key: 'popular', url: '/api/v1/movies/popular', labelKey: 'discover.movies.popular' },
    { key: 'now_playing', url: '/api/v1/movies/now_playing', labelKey: 'discover.movies.nowPlaying' },
    { key: 'top_rated', url: '/api/v1/movies/top_rated', labelKey: 'discover.movies.topRated', pages: 5 },
];

export const dedupe = (movies) => Array.from(new Map(movies.map((m) => [m.id, m])).values());

/** Loads the three movie collections; a row that fails is left out. */
export const useMovieCollections = (keys = MOVIE_SECTIONS.map((s) => s.key)) => {
    const [state, setState] = useState({ status: 'loading', rows: {} });
    const keysKey = keys.join(',');

    const load = useCallback(async () => {
        setState({ status: 'loading', rows: {} });
        const sections = MOVIE_SECTIONS.filter((s) => keysKey.split(',').includes(s.key));
        const results = await Promise.allSettled(sections.map((s) => api.get(s.url)));
        const rows = {};
        results.forEach((r, i) => {
            if (r.status === 'fulfilled') rows[sections[i].key] = dedupe(r.value.data?.results || []).map(fromTmdb);
        });
        setState({ status: Object.keys(rows).length ? 'ready' : 'error', rows });
    }, [keysKey]);

    useEffect(() => { load(); }, [load]);
    return { ...state, reload: load };
};

// Suggested people (GET /api/v1/people/suggestions) and the people search.
export const usePeopleSuggestions = (limit = 20) => {
    const [state, setState] = useState({ status: 'loading', basis: null, items: [] });
    const load = useCallback(async () => {
        setState({ status: 'loading', basis: null, items: [] });
        try {
            const { data } = await api.get('/api/v1/people/suggestions', { params: { limit } });
            setState({ status: 'ready', basis: data?.basis ?? 'popular', items: data?.items || [] });
        } catch {
            setState({ status: 'error', basis: null, items: [] });
        }
    }, [limit]);
    useEffect(() => { load(); }, [load]);
    return { ...state, reload: load };
};

