import React, { useEffect, useState, useCallback, useMemo } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import {
    Box, Typography, IconButton, Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Avatar, AvatarGroup, Tooltip,
    Chip, FormControlLabel, Switch,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/DeleteOutlineRounded';
import EditIcon from '@mui/icons-material/EditRounded';
import GroupIcon from '@mui/icons-material/GroupRounded';
import LogoutIcon from '@mui/icons-material/LogoutRounded';
import CheckIcon from '@mui/icons-material/CheckRounded';
import MovieFilterIcon from '@mui/icons-material/MovieFilterRounded';
import TaskAltIcon from '@mui/icons-material/TaskAltRounded';
import PublicIcon from '@mui/icons-material/PublicRounded';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { WatchlistMembersDialog } from '../components/WatchlistMembersDialog';
import { useSnackbar } from '../context/SnackbarProvider';
import { VisibilityControl } from '../components/VisibilityControl';
import { ListReactions, OwnerLink, PosterCollage } from '../components/ListCard';
import { VISIBILITY, isPublic, useListReactions } from '../components/publicLists';
import { Page } from '../components/ui/Page';
import { PosterCard } from '../components/ui/PosterCard';
import { CardSkeleton, PosterGridSkeleton } from '../components/ui/Skeletons';
import { apiErrorMessage } from '../components/ui/apiError';

const initial = (name) => name?.charAt(0).toUpperCase();

export const WatchlistDetailPage = () => {
    const { t } = useTranslation();
    const { listId } = useParams();
    const { user } = useAuth();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar();
    const [watchlist, setWatchlist] = useState(null);
    // 'member': I own the list or am an accepted member (full detail).
    // 'public': someone else's list opened from a shared message, discovery
    // or a profile (read only; a public list can be liked and saved).
    const [access, setAccess] = useState('member');
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');

    const [isEditModalOpen, setIsEditModalOpen] = useState(false);
    const [editedListName, setEditedListName] = useState('');
    const [editedListDesc, setEditedListDesc] = useState('');
    const [editedVisibility, setEditedVisibility] = useState(VISIBILITY.private);
    const [isMembersOpen, setIsMembersOpen] = useState(false);
    const [onlyUnwatched, setOnlyUnwatched] = useState(false);

    const [dialog, setDialog] = useState({ isOpen: false, title: '', description: '', onConfirm: null });

    const fetchWatchlistDetails = useCallback(async ({ silent = false } = {}) => {
        if (!user) return;
        if (!silent) {
            setIsLoading(true);
            setError('');
        }
        try {
            let listData;
            try {
                listData = (await api.get(`/api/v1/watchlist/${listId}`)).data;
                setAccess('member');
            } catch (err) {
                // Not mine and not a member: fall back to the read-only public view.
                if (err.response?.status !== 404) throw err;
                listData = (await api.get(`/api/v1/watchlist/public/${listId}`)).data;
                setAccess('public');
            }
            setWatchlist(listData);
            setEditedListName(listData.name);
            setEditedListDesc(listData.description || '');
            setEditedVisibility(listData.visibility || VISIBILITY.private);
        } catch {
            if (!silent) setError(t('watchlist.notFound'));
        } finally {
            if (!silent) setIsLoading(false);
        }
    }, [listId, user, t]);

    useEffect(() => {
        fetchWatchlistDetails();
    }, [fetchWatchlistDetails]);

    const updateList = useCallback((id, update) => {
        setWatchlist((current) => (current && current.id === id ? update(current) : current));
    }, []);
    const toggleReaction = useListReactions(updateList);

    const isMemberView = access === 'member';
    const isOwner = isMemberView && Boolean(watchlist?.isOwner);
    const owner = isMemberView ? watchlist?.owner : null;
    const ownerName = owner?.userName || watchlist?.ownerUsername;
    const acceptedMembers = useMemo(
        () => (isMemberView ? (watchlist?.members || []).filter((m) => m.accepted) : []),
        [isMemberView, watchlist]
    );
    // Same rule as the list summaries: someone else's list, or one with accepted members.
    const isShared = isMemberView && (!isOwner || acceptedMembers.length > 0);
    // The list's people: owner first, then accepted members.
    const people = useMemo(
        () => (owner ? [owner, ...acceptedMembers.map((m) => m.user)] : []),
        [owner, acceptedMembers]
    );
    const avatarByName = useMemo(
        () => new Map(people.map((p) => [p.userName, p.profileImageUrl])),
        [people]
    );

    const handleRemoveMovie = (movieId) => {
        setDialog({
            isOpen: true,
            title: t('watchlist.removeMovieTitle'),
            description: t('watchlist.removeMovieBody'),
            onConfirm: () => {
                setDialog({ isOpen: false });
                performRemoveMovie(movieId);
            }
        });
    };

    const performRemoveMovie = async (movieId) => {
        try {
            await api.delete(`/api/v1/watchlist/${listId}/movies/${movieId}`);
            showSnackbar(t('watchlist.movieRemoved'), 'success');
            fetchWatchlistDetails({ silent: true });
        } catch {
            showSnackbar(t('watchlist.removeMovieFailed'), 'error');
        }
    };

    const handleDeleteList = () => {
        setDialog({
            isOpen: true,
            title: t('watchlist.deleteTitle'),
            description: t('watchlist.deleteBody', { name: watchlist.name }),
            onConfirm: () => {
                setDialog({ isOpen: false });
                performDeleteList();
            }
        });
    };

    const performDeleteList = async () => {
        try {
            await api.delete(`/api/v1/watchlist/${listId}`);
            showSnackbar(t('watchlist.deleted'), 'success');
            // Navigating after a short delay avoids the "aria-hidden" warning from the closing dialog.
            setTimeout(() => navigate('/watchlist'), 1500);
        } catch {
            showSnackbar(t('watchlist.deleteFailed'), 'error');
        }
    };

    const handleLeaveList = () => {
        setDialog({
            isOpen: true,
            title: t('watchlist.leave.title'),
            description: t('watchlist.leave.body', { name: watchlist.name }),
            onConfirm: () => {
                setDialog({ isOpen: false });
                performLeaveList();
            }
        });
    };

    // Leaving is a member removing their own membership.
    const performLeaveList = async () => {
        try {
            await api.delete(`/api/v1/watchlist/${listId}/members/${user.id}`);
            showSnackbar(t('watchlist.leave.done', { name: watchlist.name }), 'success');
            navigate('/watchlist');
        } catch (err) {
            showSnackbar(apiErrorMessage(err, t('watchlist.leave.failed')), 'error');
        }
    };

    const handleUpdateList = async (e) => {
        e.preventDefault();
        if (!editedListName.trim()) return;
        try {
            await api.put(`/api/v1/watchlist/${listId}`,
                { name: editedListName, description: editedListDesc, visibility: editedVisibility }
            );
            showSnackbar(t('watchlist.updated'), 'success');
            setIsEditModalOpen(false);
            fetchWatchlistDetails({ silent: true });
        } catch (err) {
            showSnackbar(apiErrorMessage(err, t('watchlist.updateFailed')), 'error');
        }
    };

    const handleMembersChange = (members) => {
        setWatchlist((current) => ({ ...current, members }));
    };

    if (isLoading) return <Page><CardSkeleton lines={3} /><Box sx={{ mt: 3 }}><PosterGridSkeleton count={8} /></Box></Page>;
    if (error) return <Page><ErrorState message={error} onRetry={() => fetchWatchlistDetails()} /></Page>;

    const items = watchlist?.items || [];
    const visibleItems = onlyUnwatched ? items.filter((item) => !item.watchedBy?.length) : items;
    const pendingCount = isOwner ? (watchlist?.members || []).filter((m) => !m.accepted).length : 0;
    const listIsPublic = isPublic(watchlist);
    // Others' public lists can be liked and saved; my own (canEdit) cannot.
    const canReact = !isMemberView && listIsPublic && !watchlist?.canEdit;
    const showCounts = isMemberView && (listIsPublic || watchlist?.likeCount > 0 || watchlist?.saveCount > 0);

    return (
        <>
            <Page>
                <Box component="header" sx={{ display: 'flex', gap: { xs: 2, md: 3 }, alignItems: { xs: 'flex-start', sm: 'flex-end' }, mb: 4, flexDirection: { xs: 'column', sm: 'row' } }}>
                    <Box sx={{ width: { xs: 120, sm: 168 }, flexShrink: 0 }}>
                        <PosterCollage posters={items.slice(0, 4).map((item) => item.posterPath)} />
                    </Box>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                        <Typography variant="overline" component="p" color="text.secondary">
                            {listIsPublic ? t('watchlist.visibility.publicChip') : t('watchlist.listLabel')}
                            {' · '}{t('watchlist.movieCount', { count: items.length })}
                        </Typography>
                        <Typography variant="h1" component="h1" sx={{ overflowWrap: 'anywhere' }}>{watchlist?.name}</Typography>
                        {watchlist?.description && <Typography variant="body1" color="text.secondary" sx={{ mt: 0.75, maxWidth: '65ch' }}>{watchlist.description}</Typography>}

                        {!isMemberView && watchlist?.owner?.id ? (
                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mt: 1.5, flexWrap: 'wrap' }} data-testid="public-view-header">
                                <OwnerLink owner={watchlist.owner} size={32} />
                                {canReact && <ListReactions list={watchlist} onToggle={toggleReaction} size="medium" />}
                            </Box>
                        ) : (
                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mt: 1.5, flexWrap: 'wrap' }}>
                                {people.length > 0 && (
                                    <AvatarGroup max={6} sx={{ '& .MuiAvatar-root': { width: 32, height: 32, fontSize: '0.875rem', borderColor: 'background.default' } }}>
                                        {people.map((person) => (
                                            <Tooltip key={person.id} title={`@${person.userName}`}>
                                                <Avatar src={mediaUrl(person.profileImageUrl)} alt={person.userName}>
                                                    {initial(person.userName)}
                                                </Avatar>
                                            </Tooltip>
                                        ))}
                                    </AvatarGroup>
                                )}
                                <Box>
                                    <Typography variant="body2" color="text.secondary" sx={{ display: 'block' }}>
                                        {t('watchlist.createdBy', { userName: ownerName })}
                                    </Typography>
                                    {isShared && (
                                        <Typography variant="body2" color="text.secondary" sx={{ display: 'block' }}>
                                            {t('watchlist.peopleCount', { count: people.length })}
                                        </Typography>
                                    )}
                                </Box>
                            </Box>
                        )}

                        {showCounts && (
                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mt: 1.5, flexWrap: 'wrap' }} data-testid="list-public-info">
                                {listIsPublic && (
                                    <Chip size="small" variant="outlined" icon={<PublicIcon />} label={t('watchlist.visibility.publicChip')} data-testid="public-chip" />
                                )}
                                <ListReactions list={watchlist} />
                            </Box>
                        )}

                        {isOwner && (
                            <Box sx={{ mt: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                                <Button variant="tonal" startIcon={<GroupIcon />} onClick={() => setIsMembersOpen(true)}>
                                    {pendingCount > 0
                                        ? t('watchlist.members.buttonWithPending', { count: pendingCount })
                                        : t('watchlist.members.button')}
                                </Button>
                                <Button variant="tonal" startIcon={<EditIcon />} onClick={() => setIsEditModalOpen(true)}>{t('watchlist.edit')}</Button>
                                <Button color="secondary" startIcon={<DeleteIcon />} onClick={handleDeleteList}>{t('watchlist.delete')}</Button>
                            </Box>
                        )}
                        {isMemberView && !isOwner && (
                            <Box sx={{ mt: 2 }}>
                                <Button variant="tonal" startIcon={<LogoutIcon />} onClick={handleLeaveList}>{t('watchlist.leave.button')}</Button>
                            </Box>
                        )}
                    </Box>
                </Box>

                {isMemberView && items.length > 0 && (
                    <FormControlLabel
                        sx={{ mb: 2 }}
                        control={<Switch checked={onlyUnwatched} onChange={(e) => setOnlyUnwatched(e.target.checked)} />}
                        label={isShared ? t('watchlist.filter.unwatchedByAll') : t('watchlist.filter.notWatchedYet')}
                    />
                )}

                {items.length === 0 ? (
                    <EmptyState
                        icon={MovieFilterIcon}
                        title={t('watchlist.noMovies')}
                        description={isMemberView ? t('watchlist.noMoviesHint') : undefined}
                        action={isMemberView ? <Button variant="tonal" component={Link} to="/discover">{t('feed.browseMovies')}</Button> : undefined}
                    />
                ) : visibleItems.length === 0 ? (
                    <EmptyState
                        compact
                        icon={TaskAltIcon}
                        title={isShared ? t('watchlist.filter.noneLeftShared') : t('watchlist.filter.noneLeftSolo')}
                    />
                ) : (
                    <Box component="ul" data-testid="list-items" sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(auto-fill, minmax(150px, 1fr))' }, gap: { xs: 2, sm: 2.5 } }}>
                        {visibleItems.map(item => (
                            <Box component="li" key={item.movieId} sx={{ position: 'relative', minWidth: 0 }}>
                                <PosterCard
                                    movie={{ id: item.movieId, title: item.title, posterPath: item.posterPath }}
                                    fluid
                                    meta={isShared && item.addedByUsername ? (
                                        <Typography variant="body2" component="span" color="text.secondary">{t('watchlist.addedBy', { userName: item.addedByUsername })}</Typography>
                                    ) : null}
                                />
                                {isMemberView && item.watchedBy?.length > 0 && (
                                    isShared ? (
                                        <Box sx={{ mt: 0.75, display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                                            <Typography variant="body2" color="text.secondary">{t('watchlist.watchedBy')}</Typography>
                                            {item.watchedBy.map((name) => (
                                                <Chip
                                                    key={name}
                                                    size="small"
                                                    variant="outlined"
                                                    avatar={<Avatar src={mediaUrl(avatarByName.get(name))} alt={name}>{initial(name)}</Avatar>}
                                                    label={`@${name}`}
                                                />
                                            ))}
                                        </Box>
                                    ) : (
                                        <Chip size="small" variant="outlined" icon={<CheckIcon />} label={t('watchlist.watched')} sx={{ mt: 0.75, '& .MuiChip-icon': { color: 'success.main' } }} />
                                    )
                                )}
                                {isMemberView && (
                                    <Tooltip title={t('watchlist.removeMovie')}>
                                        <IconButton
                                            onClick={() => handleRemoveMovie(item.movieId)}
                                            size="small"
                                            sx={{ position: 'absolute', top: 8, right: 8, color: '#F2F2F5', bgcolor: 'rgba(11,12,16,0.72)', backdropFilter: 'blur(4px)', '&:hover': { bgcolor: 'rgba(11,12,16,0.9)' } }}
                                            aria-label={`${t('watchlist.removeMovie')}: ${item.title}`}
                                        >
                                            <DeleteIcon fontSize="small" />
                                        </IconButton>
                                    </Tooltip>
                                )}
                            </Box>
                        ))}
                    </Box>
                )}
            </Page>

            <Dialog open={isEditModalOpen} onClose={() => setIsEditModalOpen(false)} maxWidth="xs" fullWidth aria-labelledby="edit-list-title">
                <Box component="form" onSubmit={handleUpdateList}>
                    <DialogTitle id="edit-list-title">{t('watchlist.edit')}</DialogTitle>
                    <DialogContent>
                        <TextField autoFocus required margin="dense" label={t('watchlist.name')} fullWidth variant="outlined" value={editedListName} onChange={(e) => setEditedListName(e.target.value)} />
                        <TextField margin="dense" label={t('watchlist.description')} fullWidth multiline rows={3} variant="outlined" value={editedListDesc} onChange={(e) => setEditedListDesc(e.target.value)} />
                        <VisibilityControl value={editedVisibility} onChange={setEditedVisibility} />
                    </DialogContent>
                    <DialogActions>
                        <Button color="inherit" onClick={() => setIsEditModalOpen(false)}>{t('common.cancel')}</Button>
                        <Button type="submit" variant="contained">{t('common.save')}</Button>
                    </DialogActions>
                </Box>
            </Dialog>
            {isOwner && (
                <WatchlistMembersDialog
                    open={isMembersOpen}
                    onClose={() => setIsMembersOpen(false)}
                    watchlist={watchlist}
                    onMembersChange={handleMembersChange}
                />
            )}
            <ConfirmationDialog
                open={dialog.isOpen}
                onClose={() => setDialog({ ...dialog, isOpen: false })}
                onConfirm={dialog.onConfirm}
                title={dialog.title}
                description={dialog.description}
            />
        </>
    );
};
