import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Card, Typography } from '@mui/material';
import { alpha } from '@mui/material/styles';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesomeRounded';
import api from '../api/axiosConfig';
import { FollowButton, UserAvatar } from './ui/UserRow';
import { CompatibilityBadge } from './ui/CompatibilityBadge';
import { clampLines } from './ui/posterUtils';

// This week's taste twin: the most compatible person you don't follow yet.
export const TasteTwinCard = ({ sx }) => {
    const { t } = useTranslation();
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

    return (
        <Card
            data-testid="taste-twin"
            component="section"
            aria-labelledby="taste-twin-heading"
            sx={[
                (theme) => ({
                    p: 2.5, height: '100%', display: 'flex', flexDirection: 'column',
                    background: `linear-gradient(135deg, ${alpha(theme.palette.primary.main, 0.14)}, ${alpha(theme.palette.accent.main, 0.06)} 60%), ${theme.palette.background.paper}`,
                }),
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
        >
            <Typography id="taste-twin-heading" variant="overline" component="h2" sx={{ color: 'text.secondary', display: 'flex', alignItems: 'center', gap: 0.75 }}>
                <AutoAwesomeIcon fontSize="inherit" sx={{ color: 'primary.main' }} aria-hidden="true" /> {t('tasteTwin.heading')}
            </Typography>
            <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', mt: 1.5 }}>
                <UserAvatar user={twin.user} size={56} />
                <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography component={Link} to={`/profile/${twin.user.id}`} variant="h3" sx={{ color: 'text.primary', display: 'block', '&:hover': { textDecoration: 'underline' } }} noWrap>
                        @{twin.user.userName}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                        {t('people.commonFilms', { count: twin.commonMovieCount })}
                    </Typography>
                </Box>
                <CompatibilityBadge value={twin.compatibility} variant="ring" size="md" />
            </Box>
            {twin.bio && <Typography variant="body1" color="text.secondary" sx={{ mt: 1.5, fontStyle: 'italic', ...clampLines(2) }}>“{twin.bio}”</Typography>}
            <Box sx={{ display: 'flex', gap: 1, mt: 'auto', pt: 2, flexWrap: 'wrap' }}>
                <Button component={Link} to={`/profile/${twin.user.id}`} variant="tonal" size="small">{t('tasteTwin.viewProfile')}</Button>
                <FollowButton user={twin.user} following={Boolean(twin.isFollowing)} />
            </Box>
        </Card>
    );
};
