import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Card, Typography } from '@mui/material';
import { PosterWall } from './ui/PosterWall';
import { Wordmark } from './ui/Wordmark';
import { readPosterWall } from './ui/posterWallCache';
import { LanguageSwitcher } from './LanguageSwitcher';

// The cinematic frame of the sign-in and sign-up screens: a poster wall
// behind a centred card with the wordmark and the tagline.
export const AuthShell = ({ title, children, footer }) => {
    const { t } = useTranslation();
    const [posters] = useState(readPosterWall);
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
