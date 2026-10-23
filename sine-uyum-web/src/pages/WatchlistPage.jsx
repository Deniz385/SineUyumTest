import React, { useEffect, useState, useCallback } from 'react';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { useSnackbar } from '../context/SnackbarProvider';
import { Box, Typography, Grid, Card, Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Chip, Stack } from '@mui/material';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import AddIcon from '@mui/icons-material/AddRounded';
import GroupIcon from '@mui/icons-material/GroupRounded';
import PlaylistAddIcon from '@mui/icons-material/PlaylistAddRounded';
import PlaylistPlayIcon from '@mui/icons-material/PlaylistPlayRounded';
import PublicIcon from '@mui/icons-material/PublicRounded';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { VisibilityControl } from '../components/VisibilityControl';
import { SavedListsSection } from '../components/SavedListsSection';
import { VISIBILITY, isPublic } from '../components/publicLists';
import { Page, PageHeader } from '../components/ui/Page';
import { CardSkeleton } from '../components/ui/Skeletons';
import { UserAvatar } from '../components/ui/UserRow';
import { apiErrorMessage } from '../components/ui/apiError';

const formatDate = (iso, language) => new Date(iso).toLocaleDateString(language, { day: 'numeric', month: 'short', year: 'numeric' });

// A pending invitation to someone else's shared list.
const InvitationCard = ({ invitation, busy, onAccept, onDecline }) => {
    const { t, i18n } = useTranslation();
    const ownerName = invitation.owner?.userName;
    return (
        <Card>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap', p: 2 }}>
                <UserAvatar user={invitation.owner} size={44} />
                <Box sx={{ flex: '1 1 200px', minWidth: 0 }}>
                    <Typography variant="title" component="h3" noWrap>{invitation.name}</Typography>
                    <Typography variant="body2" color="text.secondary">
                        {t('watchlist.invitations.invitedBy', { userName: ownerName })}
                        {' · '}{t('watchlist.movieCount', { count: invitation.itemCount })}
                        {' · '}{formatDate(invitation.invitedAt, i18n.language)}
                    </Typography>
                </Box>
                <Stack direction="row" spacing={1}>
                    <Button variant="contained" size="small" disabled={busy} onClick={() => onAccept(invitation)}>
                        {t('watchlist.invitations.accept')}
                    </Button>
                    <Button variant="tonal" size="small" disabled={busy} onClick={() => onDecline(invitation)}>
                        {t('watchlist.invitations.decline')}
                    </Button>
                </Stack>
            </Box>
        </Card>
    );
};

export const WatchlistPage = () => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar();
    const [watchlists, setWatchlists] = useState([]);
    const [invitations, setInvitations] = useState([]);
    const [busyInvitationId, setBusyInvitationId] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [open, setOpen] = useState(false);
    const [newListName, setNewListName] = useState('');
    const [newListDesc, setNewListDesc] = useState('');
    const [newListVisibility, setNewListVisibility] = useState(VISIBILITY.private);

    const fetchWatchlists = useCallback(async ({ silent = false } = {}) => {
        if (!user) return;
        if (!silent) {
            setIsLoading(true);
            setError('');
        }
        try {
            const response = await api.get(`/api/v1/watchlist`);
            setWatchlists(response.data);
        } catch {
            if (!silent) setError(t('watchlist.loadError'));
        } finally {
            if (!silent) setIsLoading(false);
        }
    }, [user, t]);

    // Invitations are secondary: if they fail to load, the lists still show.
    const fetchInvitations = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get(`/api/v1/watchlist/invitations`);
            setInvitations(response.data || []);
        } catch {
            setInvitations([]);
        }
    }, [user]);

    useEffect(() => {
        fetchWatchlists();
        fetchInvitations();
    }, [fetchWatchlists, fetchInvitations]);

    const handleCreateList = async (e) => {
        e.preventDefault();
        if (!newListName.trim()) return;

        try {
            await api.post(`/api/v1/watchlist`, { name: newListName, description: newListDesc, visibility: newListVisibility });
            setOpen(false);
            setNewListName('');
            setNewListDesc('');
            setNewListVisibility(VISIBILITY.private);
            fetchWatchlists();
        } catch (err) {
            // The lists are still fine, so this is a message, not an error page.
            showSnackbar(apiErrorMessage(err, t('watchlist.createFailed')), 'error');
        }
    };

    const handleAccept = async (invitation) => {
        setBusyInvitationId(invitation.listId);
        try {
            await api.post(`/api/v1/watchlist/${invitation.listId}/accept`);
            setInvitations((current) => current.filter((i) => i.listId !== invitation.listId));
            showSnackbar(t('watchlist.invitations.accepted', { name: invitation.name }), 'success');
            // The list now shows up among mine, with fresh member counts.
            await fetchWatchlists({ silent: true });
        } catch (err) {
            showSnackbar(apiErrorMessage(err, t('watchlist.invitations.acceptFailed')), 'error');
        } finally {
            setBusyInvitationId(null);
        }
    };

    // Declining is the invitee removing their own (pending) membership.
    const handleDecline = async (invitation) => {
        setBusyInvitationId(invitation.listId);
        try {
            await api.delete(`/api/v1/watchlist/${invitation.listId}/members/${user.id}`);
            setInvitations((current) => current.filter((i) => i.listId !== invitation.listId));
            showSnackbar(t('watchlist.invitations.declined'), 'info');
        } catch (err) {
            showSnackbar(apiErrorMessage(err, t('watchlist.invitations.declineFailed')), 'error');
        } finally {
            setBusyInvitationId(null);
        }
    };

    if (isLoading) return <Page><PageHeader title={t('watchlist.title')} /><CardSkeleton lines={2} /></Page>;
    if (error) return <Page><ErrorState message={error} onRetry={() => fetchWatchlists()} /></Page>;

    return (
        <>
            <Page>
                <PageHeader
                    title={t('watchlist.title')}
                    actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>{t('watchlist.create')}</Button>}
                />

                {invitations.length > 0 && (
                    <Box component="section" sx={{ mb: 4 }} aria-labelledby="watchlist-invitations-title">
                        <Typography id="watchlist-invitations-title" variant="h2" component="h2" sx={{ mb: 1.5 }}>
                            {t('watchlist.invitations.title', { count: invitations.length })}
                        </Typography>
                        <Stack spacing={1.5}>
                            {invitations.map((invitation) => (
                                <InvitationCard
                                    key={invitation.listId}
                                    invitation={invitation}
                                    busy={busyInvitationId === invitation.listId}
                                    onAccept={handleAccept}
                                    onDecline={handleDecline}
                                />
                            ))}
                        </Stack>
                    </Box>
                )}

                {watchlists.length === 0 ? (
                    <EmptyState
                        icon={PlaylistAddIcon}
                        title={t('watchlist.emptyTitle')}
                        description={t('watchlist.emptyBody')}
                    />
                ) : (
                    <Grid container spacing={2}>
                        {watchlists.map(list => (
                            <Grid size={{ xs: 12, sm: 6, md: 4 }} key={list.id}>
                                <Card component={Link} to={`/watchlist/${list.id}`} sx={{ textDecoration: 'none', height: '100%', display: 'flex', gap: 2, p: 2, alignItems: 'flex-start', transition: (theme) => `background-color ${theme.motion.fast}ms`, '&:hover': { bgcolor: 'surface.raised' } }}>
                                    <Box aria-hidden="true" sx={{ width: 56, height: 56, borderRadius: 1.5, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'surface.raised', color: 'primary.main', border: 1, borderColor: 'divider' }}>
                                        {list.isShared ? <GroupIcon /> : <PlaylistPlayIcon />}
                                    </Box>
                                    <Box sx={{ minWidth: 0, flex: 1 }}>
                                        <Typography variant="title" component="h3" sx={{ color: 'text.primary', overflowWrap: 'anywhere' }}>{list.name}</Typography>
                                        {!list.isOwner && list.ownerUsername && (
                                            <Typography variant="body2" color="text.secondary">
                                                {t('watchlist.ownedBy', { userName: list.ownerUsername })}
                                            </Typography>
                                        )}
                                        {list.description && <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>{list.description}</Typography>}
                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mt: 1 }}>
                                            <Typography variant="body2" color="text.secondary" className="tabular-nums">{t('watchlist.movieCount', { count: list.itemCount })}</Typography>
                                            {isPublic(list) && (
                                                <Chip size="small" variant="outlined" icon={<PublicIcon />} label={t('watchlist.visibility.publicChip')} data-testid="public-chip" />
                                            )}
                                            {list.isShared && (
                                                <Chip size="small" variant="outlined" icon={<GroupIcon />} label={t('watchlist.sharedChip', { count: list.memberCount })} />
                                            )}
                                        </Box>
                                    </Box>
                                </Card>
                            </Grid>
                        ))}
                    </Grid>
                )}

                <SavedListsSection />
            </Page>

            <Dialog open={open} onClose={() => setOpen(false)} maxWidth="xs" fullWidth aria-labelledby="create-list-title">
                <Box component="form" onSubmit={handleCreateList}>
                    <DialogTitle id="create-list-title">{t('watchlist.create')}</DialogTitle>
                    <DialogContent>
                        <TextField autoFocus required margin="dense" label={t('watchlist.name')} fullWidth variant="outlined" value={newListName} onChange={(e) => setNewListName(e.target.value)} />
                        <TextField margin="dense" label={t('watchlist.description')} fullWidth multiline rows={3} variant="outlined" value={newListDesc} onChange={(e) => setNewListDesc(e.target.value)} />
                        <VisibilityControl value={newListVisibility} onChange={setNewListVisibility} />
                    </DialogContent>
                    <DialogActions>
                        <Button color="inherit" onClick={() => setOpen(false)}>{t('common.cancel')}</Button>
                        <Button type="submit" variant="contained">{t('common.create')}</Button>
                    </DialogActions>
                </Box>
            </Dialog>
        </>
    );
};
