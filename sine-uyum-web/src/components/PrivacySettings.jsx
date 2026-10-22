import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import {
    Avatar, Box, Button, CircularProgress, Divider, FormControl, FormControlLabel, InputLabel,
    List, ListItem, ListItemAvatar, ListItemText, MenuItem, Select, Switch, Typography
} from '@mui/material';

const PROFILE_VISIBILITY_OPTIONS = [
    { value: 'Public', labelKey: 'privacy.audience.everyone' },
    { value: 'Following', labelKey: 'privacy.audience.following' },
    { value: 'Private', labelKey: 'privacy.audience.onlyMe' },
];

const MESSAGE_PERMISSION_OPTIONS = [
    { value: 'Everyone', labelKey: 'privacy.audience.everyone' },
    { value: 'Following', labelKey: 'privacy.audience.following' },
    { value: 'Nobody', labelKey: 'privacy.audience.nobody' },
];

// Privacy settings (G12). Every change is saved immediately and the settings
// are independent of each other. If the server rejects one, the previous
// value is restored.
export const PrivacySettings = () => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [settings, setSettings] = useState(null);
    const [blocked, setBlocked] = useState([]);
    const [isSaving, setIsSaving] = useState(false);
    const [loadError, setLoadError] = useState(false);

    const load = useCallback(async () => {
        setLoadError(false);
        try {
            const [privacyRes, blocksRes] = await Promise.all([
                api.get('/api/v1/account/privacy'),
                api.get('/api/v1/blocks'),
            ]);
            setSettings(privacyRes.data);
            setBlocked(blocksRes.data);
        } catch {
            setLoadError(true);
        }
    }, []);

    useEffect(() => {
        load();
    }, [load]);

    const save = async (patch) => {
        const previous = settings;
        const next = { ...settings, ...patch };
        setSettings(next);
        setIsSaving(true);
        try {
            const response = await api.put('/api/v1/account/privacy', next);
            setSettings(response.data);
            showSnackbar(t('privacy.saved'), 'success');
        } catch (error) {
            setSettings(previous);
            showSnackbar(error.response?.data?.title || t('privacy.saveFailed'), 'error');
        } finally {
            setIsSaving(false);
        }
    };

    const unblock = async (blockedUser) => {
        try {
            await api.delete(`/api/v1/blocks/${blockedUser.id}`);
            setBlocked((current) => current.filter((u) => u.id !== blockedUser.id));
            showSnackbar(t('privacy.unblocked', { userName: blockedUser.userName }), 'success');
        } catch {
            showSnackbar(t('privacy.unblockFailed'), 'error');
        }
    };

    if (loadError) {
        return (
            <Box sx={{ mt: 4 }}>
                <Typography color="error">{t('privacy.loadError')}</Typography>
                <Button onClick={load}>{t('common.retry')}</Button>
            </Box>
        );
    }
    if (!settings) return <CircularProgress sx={{ mt: 4 }} />;

    return (
        <Box sx={{ mt: 5, textAlign: 'left' }} data-testid="privacy-settings">
            <Typography variant="h5" component="h2" gutterBottom>{t('privacy.title')}</Typography>

            <FormControl fullWidth margin="normal">
                <InputLabel id="profile-visibility-label">{t('privacy.profileVisibility')}</InputLabel>
                <Select
                    labelId="profile-visibility-label"
                    label={t('privacy.profileVisibility')}
                    value={settings.profileVisibility}
                    disabled={isSaving}
                    onChange={(e) => save({ profileVisibility: e.target.value })}
                >
                    {PROFILE_VISIBILITY_OPTIONS.map((o) => <MenuItem key={o.value} value={o.value}>{t(o.labelKey)}</MenuItem>)}
                </Select>
            </FormControl>
            <Typography variant="caption" color="text.secondary">
                {t('privacy.alwaysVisible')}
            </Typography>

            <FormControl fullWidth margin="normal">
                <InputLabel id="message-permission-label">{t('privacy.messagePermission')}</InputLabel>
                <Select
                    labelId="message-permission-label"
                    label={t('privacy.messagePermission')}
                    value={settings.messagePermission}
                    disabled={isSaving}
                    onChange={(e) => save({ messagePermission: e.target.value })}
                >
                    {MESSAGE_PERMISSION_OPTIONS.map((o) => <MenuItem key={o.value} value={o.value}>{t(o.labelKey)}</MenuItem>)}
                </Select>
            </FormControl>

            <FormControlLabel
                sx={{ mt: 1 }}
                control={
                    <Switch
                        checked={settings.hiddenFromSearch}
                        disabled={isSaving}
                        onChange={(e) => save({ hiddenFromSearch: e.target.checked })}
                    />
                }
                label={t('privacy.hiddenFromSearch')}
            />

            <FormControlLabel
                sx={{ mt: 1, display: 'flex' }}
                control={
                    <Switch
                        checked={settings.showReadReceipts !== false}
                        disabled={isSaving}
                        onChange={(e) => save({ showReadReceipts: e.target.checked })}
                    />
                }
                label={t('privacy.readReceipts')}
            />

            <Divider sx={{ my: 3 }} />
            <Typography variant="h6" component="h3">{t('privacy.blockedTitle')}</Typography>
            {blocked.length === 0 ? (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    {t('privacy.noBlocked')}
                </Typography>
            ) : (
                <List>
                    {blocked.map((b) => (
                        <ListItem key={b.id} secondaryAction={<Button onClick={() => unblock(b)}>{t('privacy.unblock')}</Button>}>
                            <ListItemAvatar>
                                <Avatar src={mediaUrl(b.profileImageUrl)}>{b.userName?.charAt(0).toUpperCase()}</Avatar>
                            </ListItemAvatar>
                            <ListItemText primary={<Link to={`/profile/${b.id}`}>{b.userName}</Link>} />
                        </ListItem>
                    ))}
                </List>
            )}
        </Box>
    );
};
