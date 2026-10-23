import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Card, Typography } from '@mui/material';
import api from '../api/axiosConfig';
import { PosterWall } from './ui/PosterWall';
import { Wordmark } from './ui/Wordmark';
import { readPosterWall, savePosterWall } from './ui/posterWallCache';
import { LanguageSwitcher } from './LanguageSwitcher';

// The cinematic frame of the sign-in and sign-up screens: a poster wall
// behind a centred card with the wordmark and the tagline. The posters come
// from GET /movies/showcase (no sign-in needed); until it answers, or if it
// fails, the last ones seen are used, else tinted tiles.
export const AuthShell = ({ title, children, footer }) => {
    const { t } = useTranslation();
    const [posters, setPosters] = useState(readPosterWall);

    useEffect(() => {
        let cancelled = false;
        api.get('/api/v1/movies/showcase')
            .then(({ data }) => {
                const paths = (data?.posters || []).filter((p) => typeof p === 'string' && p.startsWith('/'));
                if (cancelled || paths.length === 0) return;
                setPosters(paths);
                savePosterWall(paths);
            })
            .catch(() => { /* keep the cached posters or the tiles */ });
        return () => { cancelled = true; };
    }, []);

    return (
        <Box sx={{ minHeight: '100dvh', position: 'relative', display: 'flex', flexDirection: 'column' }}>
            <PosterWall posters={posters} />
            <Box sx={{ position: 'relative', zIndex: 1, display: 'flex', justifyContent: 'flex-end', p: { xs: 1, sm: 2 } }}>
                <LanguageSwitcher variant="text" sx={{ color: 'text.secondary' }} />
            </Box>
            <Box component="main" sx={{ position: 'relative', zIndex: 1, flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', px: 2, pb: 6 }}>
                <Wordmark size={36} sx={{ mb: 1 }} />
                <Typography variant="body1" color="text.secondary" sx={{ mb: 3, textAlign: 'center', maxWidth: 360 }}>{t('auth.tagline')}</Typography>
                <Card sx={{ width: '100%', maxWidth: 420, p: { xs: 3, sm: 4 }, bgcolor: 'background.paper', boxShadow: '0 24px 64px rgba(0,0,0,.45)' }}>
                    <Typography variant="h1" component="h1" sx={{ mb: 1 }}>{title}</Typography>
                    {children}
                </Card>
                {footer && <Box sx={{ mt: 3, textAlign: 'center' }}>{footer}</Box>}
            </Box>
        </Box>
    );
};
