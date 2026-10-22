import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
    Autocomplete, Avatar, Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle,
    IconButton, List, ListItem, ListItemAvatar, ListItemText, TextField, Tooltip, Typography,
} from '@mui/material';
import PersonRemoveIcon from '@mui/icons-material/PersonRemove';
import CloseIcon from '@mui/icons-material/Close';
import api, { mediaUrl } from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';

const SEARCH_DEBOUNCE_MS = 400;
const SEARCH_LIMIT = 10;

const UserAvatar = ({ user, size = 40 }) => (
    <Avatar src={mediaUrl(user?.profileImageUrl)} alt={user?.userName} sx={{ width: size, height: size }}>
        {user?.userName?.charAt(0).toUpperCase()}
    </Avatar>
);

/**
 * The owner's member management for a shared list: who is in it, pending
 * invitations, removing/cancelling, and inviting someone new.
 * @param {object} watchlist - The list detail (owner, members).
 * @param {function} onMembersChange - Receives the updated members array.
 */
export const WatchlistMembersDialog = ({ open, onClose, watchlist, onMembersChange }) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [inputValue, setInputValue] = useState('');
    const [options, setOptions] = useState([]);
    const [isSearching, setIsSearching] = useState(false);
    const [isInviting, setIsInviting] = useState(false);
    const [busyMemberId, setBusyMemberId] = useState(null);

    const listId = watchlist?.id;
    const owner = watchlist?.owner;
    const members = watchlist?.members || [];

    // Debounced user search; a newer keystroke cancels the older request's result.
    useEffect(() => {
        const query = inputValue.trim();
        if (!open || !query) {
            setOptions([]);
            setIsSearching(false);
            return undefined;
        }
        let cancelled = false;
        setIsSearching(true);
        const timer = setTimeout(() => {
            api.get(`/api/v1/account/search`, { params: { query, limit: SEARCH_LIMIT } })
                .then((response) => { if (!cancelled) setOptions(response.data || []); })
                .catch(() => { if (!cancelled) setOptions([]); })
                .finally(() => { if (!cancelled) setIsSearching(false); });
        }, SEARCH_DEBOUNCE_MS);
        return () => {
            cancelled = true;
            clearTimeout(timer);
        };
    }, [inputValue, open]);

    const memberStatus = (userId) => {
        if (userId === owner?.id) return 'owner';
        const member = members.find((m) => m.user?.id === userId);
        if (!member) return null;
        return member.accepted ? 'accepted' : 'pending';
    };

    const handleInvite = async (selected) => {
        if (!selected?.id || memberStatus(selected.id) || isInviting) return;
        setIsInviting(true);
        try {
            const response = await api.post(`/api/v1/watchlist/${listId}/members`, { userId: selected.id });
            const invited = response.data?.user ? response.data : {
                user: { id: selected.id, userName: selected.userName, profileImageUrl: selected.profileImageUrl },
                accepted: false,
            };
            onMembersChange([...members, invited]);
            showSnackbar(t('watchlist.members.inviteSent', { userName: invited.user.userName }), 'success');
            setInputValue('');
            setOptions([]);
        } catch (err) {
            showSnackbar(err.response?.data?.title || t('watchlist.members.inviteFailed'), 'error');
        } finally {
            setIsInviting(false);
        }
    };

    const handleRemove = async (member) => {
        const { id, userName } = member.user;
        setBusyMemberId(id);
        try {
            await api.delete(`/api/v1/watchlist/${listId}/members/${id}`);
            onMembersChange(members.filter((m) => m.user?.id !== id));
            showSnackbar(
                member.accepted
                    ? t('watchlist.members.removed', { userName })
                    : t('watchlist.members.inviteCancelled', { userName }),
                'success'
            );
        } catch (err) {
            showSnackbar(err.response?.data?.title || t('watchlist.members.removeFailed'), 'error');
        } finally {
            setBusyMemberId(null);
        }
    };

    const optionNote = (status) => {
        if (status === 'owner') return t('watchlist.members.owner');
        if (status === 'accepted') return t('watchlist.members.alreadyMember');
        if (status === 'pending') return t('watchlist.members.alreadyInvited');
        return null;
    };

    return (
        <Dialog open={open} onClose={onClose} fullWidth maxWidth="xs" aria-labelledby="watchlist-members-title">
            <DialogTitle id="watchlist-members-title" sx={{ pr: 6 }}>
                {t('watchlist.members.title')}
                <IconButton onClick={onClose} aria-label={t('common.close')} sx={{ position: 'absolute', right: 8, top: 8 }}>
                    <CloseIcon />
                </IconButton>
            </DialogTitle>
            <DialogContent dividers>
                <Autocomplete
                    id="watchlist-invite-autocomplete"
                    options={options}
                    value={null}
                    inputValue={inputValue}
                    onInputChange={(event, value, reason) => {
                        // Selecting an option would otherwise copy its label into the box.
                        if (reason !== 'reset') setInputValue(value);
                    }}
                    onChange={(event, value) => handleInvite(value)}
                    // The server already filtered by the query.
                    filterOptions={(x) => x}
                    getOptionLabel={(option) => option.userName || ''}
                    getOptionDisabled={(option) => Boolean(memberStatus(option.id))}
                    isOptionEqualToValue={(option, value) => option.id === value.id}
                    loading={isSearching}
                    noOptionsText={inputValue.trim() ? t('watchlist.members.noResults') : t('watchlist.members.typeToSearch')}
                    loadingText={t('common.loadingEllipsis')}
                    renderOption={(props, option) => {
                        const { key, ...optionProps } = props;
                        const note = optionNote(memberStatus(option.id));
                        return (
                            <Box component="li" key={key} {...optionProps} sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                                <UserAvatar user={option} size={32} />
                                <Box sx={{ minWidth: 0 }}>
                                    <Typography variant="body2">@{option.userName}</Typography>
                                    {note && <Typography variant="caption" color="text.secondary">{note}</Typography>}
                                </Box>
                            </Box>
                        );
                    }}
                    renderInput={(params) => (
                        <TextField
                            {...params}
                            label={t('watchlist.members.inviteLabel')}
                            placeholder={t('watchlist.members.invitePlaceholder')}
                            slotProps={{
                                input: {
                                    ...params.InputProps,
                                    endAdornment: (
                                        <>
                                            {(isSearching || isInviting) ? <CircularProgress color="inherit" size={20} /> : null}
                                            {params.InputProps.endAdornment}
                                        </>
                                    ),
                                },
                            }}
                        />
                    )}
                />

                <List dense sx={{ mt: 1 }}>
                    {owner && (
                        <ListItem>
                            <ListItemAvatar><UserAvatar user={owner} /></ListItemAvatar>
                            <ListItemText primary={`@${owner.userName}`} />
                            <Chip size="small" color="primary" label={t('watchlist.members.owner')} />
                        </ListItem>
                    )}
                    {members.map((member) => {
                        const removeLabel = member.accepted ? t('watchlist.members.remove') : t('watchlist.members.cancelInvite');
                        return (
                            <ListItem
                                key={member.user.id}
                                secondaryAction={
                                    <Tooltip title={removeLabel}>
                                        <span>
                                            <IconButton
                                                edge="end"
                                                aria-label={`${removeLabel}: @${member.user.userName}`}
                                                disabled={busyMemberId === member.user.id}
                                                onClick={() => handleRemove(member)}
                                            >
                                                {member.accepted ? <PersonRemoveIcon /> : <CloseIcon />}
                                            </IconButton>
                                        </span>
                                    </Tooltip>
                                }
                            >
                                <ListItemAvatar><UserAvatar user={member.user} /></ListItemAvatar>
                                <ListItemText primary={`@${member.user.userName}`} />
                                <Chip
                                    size="small"
                                    variant="outlined"
                                    color={member.accepted ? 'success' : 'warning'}
                                    label={member.accepted ? t('watchlist.members.accepted') : t('watchlist.members.pending')}
                                    sx={{ mr: 1 }}
                                />
                            </ListItem>
                        );
                    })}
                </List>
                {members.length === 0 && (
                    <Typography variant="body2" color="text.secondary">{t('watchlist.members.empty')}</Typography>
                )}
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose}>{t('common.close')}</Button>
            </DialogActions>
        </Dialog>
    );
};
