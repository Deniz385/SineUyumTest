import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api, { mediaUrl } from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import {
    Avatar, Box, Button, CircularProgress, Divider, FormControl, FormControlLabel, InputLabel,
    List, ListItem, ListItemAvatar, ListItemText, MenuItem, Select, Switch, Typography
} from '@mui/material';

const PROFILE_VISIBILITY_OPTIONS = [
    { value: 'Public', label: 'Herkes' },
    { value: 'Following', label: 'Yalnızca takip ettiklerim' },
    { value: 'Private', label: 'Yalnızca ben' },
];

const MESSAGE_PERMISSION_OPTIONS = [
    { value: 'Everyone', label: 'Herkes' },
    { value: 'Following', label: 'Yalnızca takip ettiklerim' },
    { value: 'Nobody', label: 'Hiç kimse' },
];

// Gizlilik ayarları (G12). Her değişiklik anında kaydedilir; ayarlar
// birbirinden bağımsızdır. Sunucu reddederse önceki değere dönülür.
export const PrivacySettings = () => {
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
            showSnackbar('Gizlilik ayarı kaydedildi.', 'success');
        } catch (error) {
            setSettings(previous);
            showSnackbar(error.response?.data?.title || 'Ayar kaydedilemedi.', 'error');
        } finally {
            setIsSaving(false);
        }
    };

    const unblock = async (blockedUser) => {
        try {
            await api.delete(`/api/v1/blocks/${blockedUser.id}`);
            setBlocked((current) => current.filter((u) => u.id !== blockedUser.id));
            showSnackbar(`${blockedUser.userName} engeli kaldırıldı.`, 'success');
        } catch {
            showSnackbar('Engel kaldırılamadı.', 'error');
        }
    };

    if (loadError) {
        return (
            <Box sx={{ mt: 4 }}>
                <Typography color="error">Gizlilik ayarları yüklenemedi.</Typography>
                <Button onClick={load}>Tekrar dene</Button>
            </Box>
        );
    }
    if (!settings) return <CircularProgress sx={{ mt: 4 }} />;

    return (
        <Box sx={{ mt: 5, textAlign: 'left' }} data-testid="privacy-settings">
            <Typography variant="h5" component="h2" gutterBottom>Gizlilik</Typography>

            <FormControl fullWidth margin="normal">
                <InputLabel id="profile-visibility-label">Puanlarımı ve listelerimi kim görebilir</InputLabel>
                <Select
                    labelId="profile-visibility-label"
                    label="Puanlarımı ve listelerimi kim görebilir"
                    value={settings.profileVisibility}
                    disabled={isSaving}
                    onChange={(e) => save({ profileVisibility: e.target.value })}
                >
                    {PROFILE_VISIBILITY_OPTIONS.map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
                </Select>
            </FormControl>
            <Typography variant="caption" color="text.secondary">
                Adınız, fotoğrafınız ve biyografiniz her zaman görünür.
            </Typography>

            <FormControl fullWidth margin="normal">
                <InputLabel id="message-permission-label">Bana kim mesaj gönderebilir</InputLabel>
                <Select
                    labelId="message-permission-label"
                    label="Bana kim mesaj gönderebilir"
                    value={settings.messagePermission}
                    disabled={isSaving}
                    onChange={(e) => save({ messagePermission: e.target.value })}
                >
                    {MESSAGE_PERMISSION_OPTIONS.map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
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
                label="Kullanıcı aramasında görünme"
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
                label="Okundu bilgisi (kapatırsan başkalarınınkini de göremezsin)"
            />

            <Divider sx={{ my: 3 }} />
            <Typography variant="h6" component="h3">Engellenen kullanıcılar</Typography>
            {blocked.length === 0 ? (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    Kimseyi engellemediniz. Engellemek için kişinin profilini açın.
                </Typography>
            ) : (
                <List>
                    {blocked.map((b) => (
                        <ListItem key={b.id} secondaryAction={<Button onClick={() => unblock(b)}>Engeli kaldır</Button>}>
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
