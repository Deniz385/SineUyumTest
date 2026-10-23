import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, IconButton, LinearProgress, MenuItem, Paper, Stack, TextField, Tooltip, Typography } from '@mui/material';
import EditIcon from '@mui/icons-material/EditRounded';
import DeleteIcon from '@mui/icons-material/DeleteOutlineRounded';
import ReplayIcon from '@mui/icons-material/ReplayRounded';
import LockIcon from '@mui/icons-material/Lock';
import EditCalendarIcon from '@mui/icons-material/EditCalendar';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { ConfirmationDialog } from './ConfirmationDialog';
import { DiaryEntryDialog } from './DiaryEntryDialog';
import { monthNames, parseIsoDay } from './diaryDates';
import { CardSkeleton } from './ui/Skeletons';
import { PosterImage } from './ui/PosterImage';
import { RatingStars } from './ui/RatingInput';
import { EmptyState } from './states/EmptyState';
import { ErrorState } from './states/ErrorState';

const PAGE_SIZE = 20;
const CHART_HEIGHT = 72;

// Entries arrive newest first, so a month's entries are always adjacent.
const groupByMonth = (entries) => {
    const groups = [];
    for (const entry of entries) {
        const month = Number(entry.watchedOn.slice(5, 7));
        if (groups.at(-1)?.month !== month) groups.push({ month, entries: [] });
        groups.at(-1).entries.push(entry);
    }
    return groups;
};

const MonthChart = ({ year, months, language }) => {
    const { t } = useTranslation();
    const shortNames = monthNames(language, 'short');
    const narrowNames = monthNames(language, 'narrow');
    const longFormat = new Intl.DateTimeFormat(language, { month: 'long' });
    const max = Math.max(1, ...months.map((m) => m.count));

    return (
        <Box
            role="img"
            aria-label={t('diary.chartLabel', { year })}
            data-testid="diary-chart"
            sx={{ display: 'grid', gridTemplateColumns: 'repeat(12, 1fr)', gap: { xs: 0.5, sm: 1 }, alignItems: 'end', my: 2 }}
        >
            {months.map(({ month, count }) => (
                <Tooltip key={month} title={t('diary.monthCount', { count, month: longFormat.format(new Date(year, month - 1, 1)) })}>
                    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', minWidth: 0 }}>
                        <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.2, visibility: count ? 'visible' : 'hidden' }}>{count}</Typography>
                        <Box sx={{ height: CHART_HEIGHT, width: '100%', display: 'flex', alignItems: 'flex-end', justifyContent: 'center' }}>
                            <Box
                                data-testid={`diary-bar-${month}`}
                                sx={{
                                    width: '70%',
                                    maxWidth: 28,
                                    height: count ? `${Math.max(6, (count / max) * 100)}%` : 2,
                                    bgcolor: count ? 'primary.main' : 'divider',
                                    borderRadius: '4px 4px 0 0',
                                }}
                            />
                        </Box>
                        {/* Phones get one-letter names; the tooltip has the full one. */}
                        <Typography variant="caption" sx={{ mt: 0.5, whiteSpace: 'nowrap', display: { xs: 'none', sm: 'block' } }}>{shortNames[month - 1]}</Typography>
                        <Typography variant="caption" aria-hidden="true" sx={{ mt: 0.5, display: { xs: 'block', sm: 'none' } }}>{narrowNames[month - 1]}</Typography>
                    </Box>
                </Tooltip>
            ))}
        </Box>
    );
};

const DiaryEntryRow = ({ entry, language, isMine, onEdit, onDelete }) => {
    const { t } = useTranslation();
    const day = parseIsoDay(entry.watchedOn);
    const { title } = entry.movie;

    return (
        <Paper variant="outlined" data-testid="diary-entry" sx={{ display: 'flex', gap: { xs: 1, sm: 2 }, p: 1.25, alignItems: 'flex-start', bgcolor: 'background.paper' }}>
            <Box sx={{ width: 40, flexShrink: 0, textAlign: 'center', pt: 0.5 }}>
                <Typography component="p" sx={{ typography: 'number', fontSize: '1.375rem' }}>{day.getDate()}</Typography>
                <Typography variant="caption" color="text.secondary">{day.toLocaleDateString(language, { weekday: 'short' })}</Typography>
            </Box>
            <Box component={Link} to={`/movie/${entry.movie.id}`} sx={{ flexShrink: 0, width: 46, borderRadius: 1 }}>
                <PosterImage path={entry.movie.posterPath} title={t('profile.posterAlt', { title })} width={46} radius="sm" />
            </Box>
            <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                    <Typography component={Link} to={`/movie/${entry.movie.id}`} sx={{ fontWeight: 600, color: 'text.primary', textDecoration: 'none', '&:hover': { textDecoration: 'underline' } }}>
                        {title}
                    </Typography>
                    {entry.rating != null && (
                        <RatingStars value={entry.rating} size="small" showNumber />
                    )}
                    {entry.rewatch && (
                        <Tooltip title={t('diary.rewatch')}>
                            <ReplayIcon fontSize="small" sx={{ color: 'text.secondary' }} aria-label={t('diary.rewatch')} data-testid="diary-rewatch" />
                        </Tooltip>
                    )}
                </Box>
                {entry.note && (
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{entry.note}</Typography>
                )}
            </Box>
            {isMine && (
                <Box sx={{ display: 'flex', flexShrink: 0 }}>
                    <IconButton size="small" onClick={() => onEdit(entry)} aria-label={t('diary.editEntry', { title })}><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" onClick={() => onDelete(entry)} aria-label={t('diary.deleteEntry', { title })}><DeleteIcon fontSize="small" /></IconButton>
                </Box>
            )}
        </Paper>
    );
};

// The film diary on a profile: a year's month-by-month chart and entries.
// Someone else's diary follows the profile privacy rules (403 private, 404 blocked).
export const ProfileDiary = ({ userId, isMine }) => {
    const { t, i18n } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const language = i18n.language;
    const [summary, setSummary] = useState(null);
    const [entries, setEntries] = useState([]);
    const [totalCount, setTotalCount] = useState(0);
    const [page, setPage] = useState(1);
    // { status, title } of a failed load; the message is chosen at render time.
    const [loadError, setLoadError] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [editTarget, setEditTarget] = useState(null);
    const [isEditOpen, setIsEditOpen] = useState(false);
    const [deleteTarget, setDeleteTarget] = useState(null);
    // Only the latest load may update the state (e.g. quick year switches).
    const loadId = useRef(0);

    const fetchPage = useCallback((year, pageNumber) =>
        api.get(`/api/v1/diary/user/${userId}`, { params: { year, page: pageNumber, pageSize: PAGE_SIZE } }).then((res) => res.data),
    [userId]);

    // Loads the summary and the first `pages` pages of a year. Without a year it
    // opens the current year, or the latest one with entries if this year has none.
    const load = useCallback(async (requestedYear, pages = 1) => {
        const id = ++loadId.current;
        setIsLoading(true);
        try {
            const summaryUrl = `/api/v1/diary/user/${userId}/summary`;
            let nextSummary = (await api.get(summaryUrl, { params: requestedYear ? { year: requestedYear } : {} })).data;
            const years = nextSummary.availableYears;
            if (requestedYear == null && years.length > 0 && !years.includes(nextSummary.year)) {
                nextSummary = (await api.get(summaryUrl, { params: { year: years[0] } })).data;
            }
            const results = await Promise.all(Array.from({ length: pages }, (_, i) => fetchPage(nextSummary.year, i + 1)));
            if (id !== loadId.current) return;
            const seen = new Set();
            setEntries(results.flatMap((r) => r.items).filter((e) => !seen.has(e.id) && seen.add(e.id)));
            setTotalCount(results.at(-1).totalCount);
            setSummary(nextSummary);
            setPage(pages);
            setLoadError(null);
        } catch (err) {
            if (id !== loadId.current) return;
            setLoadError({ status: err.response?.status, title: err.response?.data?.title });
        } finally {
            if (id === loadId.current) setIsLoading(false);
        }
    }, [userId, fetchPage]);

    useEffect(() => { load(null); }, [load]);

    const loadMore = async () => {
        setIsLoadingMore(true);
        try {
            const data = await fetchPage(summary.year, page + 1);
            setEntries((prev) => [...prev, ...data.items.filter((e) => !prev.some((p) => p.id === e.id))]);
            setTotalCount(data.totalCount);
            setPage(page + 1);
        } catch (err) {
            showSnackbar(err.response?.data?.title || t('diary.loadFailed'), 'error');
        } finally {
            setIsLoadingMore(false);
        }
    };

    // After a change the loaded pages are fetched again: an edited date can
    // move an entry to another month or year and the counts change.
    const reload = () => load(summary.year, page);

    const openEdit = (entry) => {
        setEditTarget(entry);
        setIsEditOpen(true);
    };

    const confirmDelete = async () => {
        const entry = deleteTarget;
        setDeleteTarget(null);
        try {
            await api.delete(`/api/v1/diary/${entry.id}`);
            showSnackbar(t('diary.deleted'), 'success');
            reload();
        } catch (err) {
            showSnackbar(err.response?.data?.title || t('diary.deleteFailed'), 'error');
        }
    };

    // A private (403) or unavailable (404) diary is not something to retry.
    const isRestricted = loadError?.status === 403 || loadError?.status === 404;
    const errorMessage = loadError && (loadError.title || (
        loadError.status === 403 ? t('diary.private')
            : loadError.status === 404 ? t('diary.unavailable')
                : t('diary.loadFailed')));

    const year = summary?.year;
    const yearOptions = summary ? [...new Set([...summary.availableYears, summary.year])].sort((a, b) => b - a) : [];
    const monthFormat = new Intl.DateTimeFormat(language, { month: 'long', year: 'numeric' });

    return (
        <Box component="section" data-testid="profile-diary" sx={{ my: 4, textAlign: 'left' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
                <Typography variant="h2" component="h2">{t('diary.title')}</Typography>
                {summary && !loadError && (
                    <TextField
                        select
                        size="small"
                        label={t('diary.year')}
                        value={year}
                        onChange={(e) => load(Number(e.target.value))}
                        sx={{ minWidth: 110 }}
                    >
                        {yearOptions.map((y) => <MenuItem key={y} value={y}>{y}</MenuItem>)}
                    </TextField>
                )}
            </Box>

            {loadError && isRestricted ? (
                <EmptyState compact icon={LockIcon} title={errorMessage} sx={{ mt: 1 }} />
            ) : loadError ? (
                <ErrorState compact message={errorMessage} onRetry={() => { setLoadError(null); load(summary?.year ?? null); }} sx={{ mt: 1 }} />
            ) : !summary ? (
                <CardSkeleton lines={4} />
            ) : (
                <Box sx={{ opacity: isLoading ? 0.6 : 1, transition: 'opacity 0.2s' }}>
                    <LinearProgress sx={{ mt: 1, visibility: isLoading ? 'visible' : 'hidden' }} />
                    <Typography sx={{ mt: 1 }} data-testid="diary-totals">
                        <strong>{t('diary.filmCount', { count: summary.totalCount })}</strong>
                        {' · '}
                        {t('diary.rewatchCount', { count: summary.rewatchCount })}
                    </Typography>
                    <MonthChart year={year} months={summary.months} language={language} />

                    {entries.length === 0 ? (
                        <EmptyState
                            compact
                            icon={EditCalendarIcon}
                            title={t('diary.empty', { year })}
                            description={isMine ? t('diary.emptyMineHint') : undefined}
                        />
                    ) : (
                        groupByMonth(entries).map((group) => (
                            <Box key={group.month} sx={{ mt: 3 }}>
                                <Typography variant="overline" component="h3" sx={{ display: "block", color: "text.secondary", mb: 1 }}>
                                    {monthFormat.format(new Date(year, group.month - 1, 1))}
                                </Typography>
                                <Stack spacing={1}>
                                    {group.entries.map((entry) => (
                                        <DiaryEntryRow
                                            key={entry.id}
                                            entry={entry}
                                            language={language}
                                            isMine={isMine}
                                            onEdit={openEdit}
                                            onDelete={setDeleteTarget}
                                        />
                                    ))}
                                </Stack>
                            </Box>
                        ))
                    )}

                    {entries.length < totalCount && (
                        <Button sx={{ mt: 2 }} onClick={loadMore} disabled={isLoadingMore}>
                            {isLoadingMore ? t('common.loadingEllipsis') : t('common.loadMore')}
                        </Button>
                    )}
                </Box>
            )}

            {isMine && (
                <>
                    <DiaryEntryDialog open={isEditOpen} onClose={() => setIsEditOpen(false)} entry={editTarget} onSaved={reload} />
                    <ConfirmationDialog
                        open={Boolean(deleteTarget)}
                        onClose={() => setDeleteTarget(null)}
                        onConfirm={confirmDelete}
                        title={t('diary.deleteConfirmTitle')}
                        description={deleteTarget ? t('diary.deleteConfirmBody', {
                            title: deleteTarget.movie.title,
                            date: parseIsoDay(deleteTarget.watchedOn).toLocaleDateString(language, { day: 'numeric', month: 'long', year: 'numeric' }),
                        }) : ''}
                    />
                </>
            )}
        </Box>
    );
};
