import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { Avatar, Box, Button, Card, CardContent, Stack, Typography } from '@mui/material';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';

// This week's taste twin: the most compatible person you don't follow yet.
export const TasteTwinCard = () => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [twin, setTwin] = useState(undefined);

    useEffect(() => {
        let cancelled = false;
        api.get('/api/v1/taste-twin')
            // 204: no suitable candidate this week.
            .then((res) => { if (!cancelled) setTwin(res.status === 204 ? null : res.data); })
            .catch(() => { if (!cancelled) setTwin(null); });
        return () => { cancelled = true; };
    }, []);

    if (!twin) return null;

    const follow = async () => {
        try {
            await api.post(`/api/v1/follow/${twin.user.id}`);
            setTwin((t) => ({ ...t, isFollowing: true }));
        } catch (error) {
            showSnackbar(error.response?.data?.title || t('tasteTwin.followFailed'), 'error');
        }
    };

    return (
        <Card data-testid="taste-twin" sx={{ mb: 3, background: 'linear-gradient(135deg, #1b1038, #0d2a4a)', color: '#fff' }}>
            <CardContent>
                <Typography variant="overline" sx={{ color: '#ffd54f', display: 'flex', alignItems: 'center', gap: 0.5 }}>
                    <AutoAwesomeIcon fontSize="inherit" /> {t('tasteTwin.heading')}
                </Typography>
                <Stack direction="row" spacing={2} alignItems="center" sx={{ mt: 1 }}>
                    <Avatar src={mediaUrl(twin.user.profileImageUrl)} sx={{ width: 56, height: 56 }}>
                        {twin.user.userName?.charAt(0).toUpperCase()}
                    </Avatar>
                    <Box sx={{ flexGrow: 1, textAlign: 'left' }}>
                        <Typography variant="h6">{twin.user.userName}</Typography>
                        <Typography variant="body2" sx={{ color: '#e3e8ff' }}>
                            {t('tasteTwin.stats', { compatibility: twin.compatibility, count: twin.commonMovieCount })}
                        </Typography>
                        {twin.bio && <Typography variant="body2" sx={{ color: '#c5cdf5', fontStyle: 'italic' }}>"{twin.bio}"</Typography>}
                    </Box>
                </Stack>
                <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                    <Button component={Link} to={`/profile/${twin.user.id}`} variant="outlined" sx={{ color: '#fff', borderColor: 'rgba(255,255,255,0.5)' }}>
                        {t('tasteTwin.viewProfile')}
                    </Button>
                    <Button variant="contained" color="warning" disabled={twin.isFollowing} onClick={follow}>
                        {twin.isFollowing ? t('tasteTwin.following') : t('tasteTwin.follow')}
                    </Button>
                </Stack>
            </CardContent>
        </Card>
    );
};
