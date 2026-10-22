import React, { useEffect, useState, useCallback } from 'react';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { useSnackbar } from '../context/SnackbarProvider';
import { Box, Typography, Grid, Card, CardContent, Button, Modal, TextField, Chip, Avatar, Stack } from '@mui/material';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import AddIcon from '@mui/icons-material/Add';
import GroupIcon from '@mui/icons-material/Group';
import PlaylistAddIcon from '@mui/icons-material/PlaylistAdd';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';

const modalStyle = {
  position: 'absolute',
  top: '50%',
  left: '50%',
  transform: 'translate(-50%, -50%)',
  width: 400,
  bgcolor: 'background.paper',
  border: '2px solid #000',
  boxShadow: 24,
  p: 4,
};

const formatDate = (iso, language) => new Date(iso).toLocaleDateString(language, { day: 'numeric', month: 'short', year: 'numeric' });

// A pending invitation to someone else's shared list.
const InvitationCard = ({ invitation, busy, onAccept, onDecline }) => {
    const { t, i18n } = useTranslation();
    const ownerName = invitation.owner?.userName;
    return (
        <Card variant="outlined">
            <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap', '&:last-child': { pb: 2 } }}>
                <Avatar src={mediaUrl(invitation.owner?.profileImageUrl)} alt={ownerName}>
                    {ownerName?.charAt(0).toUpperCase()}
                </Avatar>
                <Box sx={{ flex: '1 1 200px', minWidth: 0, textAlign: 'left' }}>
                    <Typography variant="subtitle1" fontWeight="bold" noWrap>{invitation.name}</Typography>
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
                    <Button variant="outlined" size="small" color="inherit" disabled={busy} onClick={() => onDecline(invitation)}>
                        {t('watchlist.invitations.decline')}
                    </Button>
                </Stack>
            </CardContent>
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
            await api.post(`/api/v1/watchlist`, { name: newListName, description: newListDesc });
            setOpen(false);
            setNewListName('');
            setNewListDesc('');
            fetchWatchlists();
        } catch {
            // The lists are still fine, so this is a message, not an error page.
            showSnackbar(t('watchlist.createFailed'), 'error');
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
            showSnackbar(err.response?.data?.title || t('watchlist.invitations.acceptFailed'), 'error');
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
            showSnackbar(err.response?.data?.title || t('watchlist.invitations.declineFailed'), 'error');
        } finally {
            setBusyInvitationId(null);
        }
    };

    if (isLoading) return <LoadingState />;
    if (error) return <ErrorState message={error} onRetry={() => fetchWatchlists()} />;

    return (
        <>
            <Box className="page-container" sx={{ maxWidth: '1000px' }}>
                {invitations.length > 0 && (
                    <Box component="section" sx={{ mb: 4 }} aria-labelledby="watchlist-invitations-title">
                        <Typography id="watchlist-invitations-title" variant="h6" component="h2" sx={{ mb: 1.5 }}>
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

                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3, gap: 2, flexWrap: 'wrap' }}>
                    <Typography variant="h4" component="h1">{t('watchlist.title')}</Typography>
                    <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>{t('watchlist.create')}</Button>
                </Box>

                {watchlists.length === 0 ? (
                    <EmptyState
                        icon={PlaylistAddIcon}
                        title={t('watchlist.emptyTitle')}
                        description={t('watchlist.emptyBody')}
                    />
                ) : (
                    <Grid container spacing={3}>
                        {watchlists.map(list => (
                            <Grid size={{ xs: 12, sm: 6, md: 4 }} key={list.id}>
                                <Card component={Link} to={`/watchlist/${list.id}`} sx={{ textDecoration: 'none', height: '100%', display: 'block' }}>
                                    <CardContent>
                                        <Typography variant="h6">{list.name}</Typography>
                                        {!list.isOwner && list.ownerUsername && (
                                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                                                {t('watchlist.ownedBy', { userName: list.ownerUsername })}
                                            </Typography>
                                        )}
                                        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{list.description}</Typography>
                                        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 1, flexWrap: 'wrap' }}>
                                            <Typography variant="body2" color="text.secondary">{t('watchlist.movieCount', { count: list.itemCount })}</Typography>
                                            {list.isShared && (
                                                <Chip
                                                    size="small"
                                                    color="primary"
                                                    variant="outlined"
                                                    icon={<GroupIcon />}
                                                    label={t('watchlist.sharedChip', { count: list.memberCount })}
                                                />
                                            )}
                                        </Box>
                                    </CardContent>
                                </Card>
                            </Grid>
                        ))}
                    </Grid>
                )}
            </Box>

            <Modal open={open} onClose={() => setOpen(false)}>
                <Box sx={modalStyle} component="form" onSubmit={handleCreateList}>
                    <Typography variant="h6" component="h2" sx={{ mb: 2 }}>{t('watchlist.create')}</Typography>
                    <TextField autoFocus required margin="dense" label={t('watchlist.name')} fullWidth variant="outlined" value={newListName} onChange={(e) => setNewListName(e.target.value)} />
                    <TextField margin="dense" label={t('watchlist.description')} fullWidth multiline rows={3} variant="outlined" value={newListDesc} onChange={(e) => setNewListDesc(e.target.value)} />
                    <Button type="submit" variant="contained" sx={{ mt: 2 }}>{t('common.create')}</Button>
                </Box>
            </Modal>
        </>
    );
};
