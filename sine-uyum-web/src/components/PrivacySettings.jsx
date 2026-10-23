import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { CardSkeleton } from './ui/Skeletons';
import { UserAvatar } from './ui/UserRow';
import { apiErrorMessage } from './ui/apiError';
import { prefersReducedMotion } from './ui/a11y';
import { ErrorState } from './states/ErrorState';
import {
    Box, Button, Divider, FormControl, FormControlLabel, InputLabel,
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
    const location = useLocation();
    // Navigation (e.g. the account menu's "Privacy settings") already scrolled for.
    const scrolledFor = useRef(null);

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

    // /profile/edit#privacy opens the page at this section, once it has loaded.
    useEffect(() => {
        if (!settings || location.hash !== '#privacy' || scrolledFor.current === location.key) return;
        scrolledFor.current = location.key;
        document.getElementById('privacy')?.scrollIntoView({ behavior: prefersReducedMotion() ? 'auto' : 'smooth', block: 'start' });
    }, [settings, location.hash, location.key]);

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
            showSnackbar(apiErrorMessage(error, t('privacy.saveFailed')), 'error');
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
        return <ErrorState compact message={t('privacy.loadError')} onRetry={load} />;
    }
    if (!settings) return <CardSkeleton lines={4} />;

    return (
        <Box id="privacy" component="div" sx={{ scrollMarginTop: 88 }} data-testid="privacy-settings">
            <Typography variant="h2" component="h2" sx={{ mb: 1 }}>{t('privacy.title')}</Typography>

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
            <Typography variant="body2" color="text.secondary">
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
            <Typography variant="h3" component="h3">{t('privacy.blockedTitle')}</Typography>
            {blocked.length === 0 ? (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    {t('privacy.noBlocked')}
                </Typography>
            ) : (
                <List>
                    {blocked.map((b) => (
                        <ListItem key={b.id} disableGutters secondaryAction={<Button variant="tonal" size="small" onClick={() => unblock(b)}>{t('privacy.unblock')}</Button>}>
                            <ListItemAvatar>
                                <UserAvatar user={b} size={40} />
                            </ListItemAvatar>
                            <ListItemText primary={<Box component={Link} to={`/profile/${b.id}`} sx={{ color: 'text.primary', fontWeight: 600 }}>@{b.userName}</Box>} />
                        </ListItem>
                    ))}
                </List>
            )}
        </Box>
    );
};
