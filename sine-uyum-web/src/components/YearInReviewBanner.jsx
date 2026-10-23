import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Card, IconButton, Tooltip, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesomeRounded';
import CloseIcon from '@mui/icons-material/CloseRounded';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { bannerRecapYear, dismissBanner, isBannerDismissed, yearInReviewPath } from './yearInReview';
import { gradients, marqueeText } from '../theme';

// Announces your own yearly recap on the feed in December and early January,
// only once it is ready (enough films), so it never leads to the "not ready"
// screen. Dismissing it hides it for that year.
export const YearInReviewBanner = ({ sx }) => {
    const { t } = useTranslation();
    const theme = useTheme();
    const { user } = useAuth();
    const [year] = useState(() => bannerRecapYear());
    const [dismissed, setDismissed] = useState(() => year != null && isBannerDismissed(year));
    const [ready, setReady] = useState(false);
    const userId = user?.id;

    useEffect(() => {
        if (!userId || year == null || dismissed) return undefined;
        let active = true;
        api.get(`/api/v1/year-in-review/user/${userId}`, { params: { year } })
            .then((res) => { if (active) setReady(Boolean(res.data?.ready)); })
            .catch(() => { /* no banner without a recap */ });
        return () => { active = false; };
    }, [userId, year, dismissed]);

    if (!user || year == null || dismissed || !ready) return null;

    const dismiss = () => {
        dismissBanner(year);
        setDismissed(true);
    };

    return (
        <Card
            data-testid="yir-banner"
            component="section"
            aria-labelledby="yir-banner-title"
            sx={[{ position: 'relative', p: 2.5, pr: 6, height: '100%', overflow: 'hidden', '&::before': { content: '""', position: 'absolute', inset: '0 0 auto 0', height: 3, background: gradients.marquee } }, ...(Array.isArray(sx) ? sx : [sx])]}
        >
            <Typography variant="overline" component="p" sx={{ color: 'text.secondary', display: 'flex', alignItems: 'center', gap: 0.75 }}>
                <AutoAwesomeIcon fontSize="inherit" sx={{ color: 'primary.main' }} aria-hidden="true" /> {t('yearInReview.banner.kicker')}
            </Typography>
            <Typography id="yir-banner-title" variant="h2" component="h2" sx={{ mt: 1 }}>
                <Box component="span" sx={marqueeText(theme.palette.mode)}>{t('yearInReview.banner.title', { year })}</Box>
            </Typography>
            <Typography variant="body1" color="text.secondary" sx={{ mt: 0.75 }}>{t('yearInReview.banner.body')}</Typography>
            <Button component={Link} to={yearInReviewPath(user.id, year)} variant="contained" sx={{ mt: 2 }}>
                {t('yearInReview.banner.open')}
            </Button>
            <Tooltip title={t('yearInReview.banner.dismiss')}>
                <IconButton onClick={dismiss} aria-label={t('yearInReview.banner.dismiss')} size="small" sx={{ position: 'absolute', top: 10, right: 10, color: 'text.secondary' }}>
                    <CloseIcon fontSize="small" />
                </IconButton>
            </Tooltip>
        </Card>
    );
};
