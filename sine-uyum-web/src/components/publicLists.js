// Shared logic for public lists: paged card lists (discover, a profile's
// public lists, saved lists) and optimistic like/save toggles.
import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';

export const VISIBILITY = { private: 'Private', public: 'Public' };

export const isPublic = (list) => list?.visibility === VISIBILITY.public;

export const DISCOVER_SORTS = ['trending', 'popular', 'new', 'forYou'];

const PAGE_SIZE = 20;

// Field names per reaction: the card's flag and count, and the flag in the
// POST/DELETE response.
const REACTIONS = {
    like: { flag: 'likedByMe', count: 'likeCount', result: 'liked' },
    save: { flag: 'savedByMe', count: 'saveCount', result: 'saved' },
};

/**
 * Pages of list cards from `url` (`{ items, page, hasMore }`), reloaded when
 * `url` or `params` change. `enabled: false` skips loading (e.g. a private
 * profile). `status`: 'loading' | 'ready' | 'error'; `errorStatus` is the
 * HTTP status of a failed first page (403/404 hide a profile's section).
 */
export const usePagedLists = (url, params = {}, { enabled = true } = {}) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [state, setState] = useState({ items: [], page: 0, hasMore: false, status: 'loading', errorStatus: null });
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [reloadKey, setReloadKey] = useState(0);
    // Only the latest request may update the state (tab and search changes).
    const requestId = useRef(0);
    const paramsKey = JSON.stringify(params);

    useEffect(() => {
        if (!enabled) return undefined;
        // A newer first page also makes pending "load more" results stale.
        requestId.current++;
        let cancelled = false;
        setState({ items: [], page: 0, hasMore: false, status: 'loading', errorStatus: null });
        setIsLoadingMore(false);
        api.get(url, { params: { ...JSON.parse(paramsKey), page: 1, pageSize: PAGE_SIZE } })
            .then(({ data }) => {
                if (cancelled) return;
                setState({ items: data?.items || [], page: 1, hasMore: Boolean(data?.hasMore), status: 'ready', errorStatus: null });
            })
            .catch((err) => {
                if (cancelled) return;
                setState({ items: [], page: 0, hasMore: false, status: 'error', errorStatus: err.response?.status ?? null });
            });
        return () => {
            cancelled = true;
        };
    }, [url, paramsKey, enabled, reloadKey]);

    const loadMore = useCallback(async () => {
        if (isLoadingMore || !state.hasMore) return;
        const id = requestId.current;
        const nextPage = state.page + 1;
        setIsLoadingMore(true);
        try {
            const { data } = await api.get(url, { params: { ...JSON.parse(paramsKey), page: nextPage, pageSize: PAGE_SIZE } });
            if (id !== requestId.current) return;
            setState((current) => {
                // A card can move between pages while browsing; keep one of each.
                const known = new Set(current.items.map((l) => l.id));
                const fresh = (data?.items || []).filter((l) => !known.has(l.id));
                return { ...current, items: [...current.items, ...fresh], page: nextPage, hasMore: Boolean(data?.hasMore) };
            });
        } catch (err) {
            if (id === requestId.current) showSnackbar(err.response?.data?.title || t('lists.loadMoreFailed'), 'error');
        } finally {
            if (id === requestId.current) setIsLoadingMore(false);
        }
    }, [isLoadingMore, state.hasMore, state.page, url, paramsKey, showSnackbar, t]);

    const updateList = useCallback((listId, update) => {
        setState((current) => ({ ...current, items: current.items.map((l) => (l.id === listId ? update(l) : l)) }));
    }, []);

    const removeList = useCallback((listId) => {
        setState((current) => ({ ...current, items: current.items.filter((l) => l.id !== listId) }));
    }, []);

    const reload = useCallback(() => setReloadKey((k) => k + 1), []);

    return { ...state, isLoadingMore, loadMore, updateList, removeList, reload };
};

/**
 * Like/save toggles with optimistic updates: the change shows at once, the
 * server's counts replace it, and a failure rolls it back with the server's
 * message. `updateList(listId, fn)` applies a change to the list object.
 * `onUnsaved(list)` runs after a successful unsave (saved lists drop the card).
 * Returns `toggle(list, 'like' | 'save')`.
 */
export const useListReactions = (updateList, { onUnsaved } = {}) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    // One request per list and reaction at a time; extra clicks are ignored.
    const pending = useRef(new Set());

    return useCallback(async (list, kind) => {
        const r = REACTIONS[kind];
        const key = `${list.id}:${kind}`;
        if (!r || pending.current.has(key)) return;
        pending.current.add(key);

        const before = { [r.flag]: Boolean(list[r.flag]), [r.count]: list[r.count] ?? 0 };
        const on = !before[r.flag];
        updateList(list.id, (l) => ({ ...l, [r.flag]: on, [r.count]: Math.max(0, (l[r.count] ?? 0) + (on ? 1 : -1)) }));

        try {
            const url = `/api/v1/watchlist/${list.id}/${kind}`;
            const { data } = on ? await api.post(url) : await api.delete(url);
            updateList(list.id, (l) => ({
                ...l,
                [r.flag]: typeof data?.[r.result] === 'boolean' ? data[r.result] : on,
                [r.count]: typeof data?.[r.count] === 'number' ? data[r.count] : l[r.count],
            }));
            if (kind === 'save') {
                if (on) showSnackbar(t('lists.savedToast'), 'success');
                else onUnsaved?.(list);
            }
        } catch (err) {
            updateList(list.id, (l) => ({ ...l, ...before }));
            const fallback = kind === 'like' ? t('lists.likeFailed') : t('lists.saveFailed');
            showSnackbar(err.response?.data?.title || fallback, 'error');
        } finally {
            pending.current.delete(key);
        }
    }, [updateList, onUnsaved, showSnackbar, t]);
};
