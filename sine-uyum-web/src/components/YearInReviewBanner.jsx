import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Card, CardContent, IconButton, Typography } from '@mui/material';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import CloseIcon from '@mui/icons-material/Close';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { bannerRecapYear, dismissBanner, isBannerDismissed, yearInReviewPath } from './yearInReview';

// Announces your own yearly recap on the feed in December and early January,
// only once it is ready (enough films), so it never leads to the "not ready"
// screen. Dismissing it hides it for that year.
export const YearInReviewBanner = () => {
    const { t } = useTranslation();
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
        <Card data-testid="yir-banner" sx={{ mb: 3, color: '#fff', background: 'linear-gradient(120deg, #2a0f45 0%, #4a1450 45%, #06243f 100%)', position: 'relative' }}>
            <CardContent sx={{ pr: 6, textAlign: 'left' }}>
                <Typography variant="overline" sx={{ color: '#ffd54f', display: 'flex', alignItems: 'center', gap: 0.5 }}>
                    <AutoAwesomeIcon fontSize="inherit" /> {t('yearInReview.banner.kicker')}
                </Typography>
                <Typography variant="h6" component="h2" sx={{ fontWeight: 800 }}>{t('yearInReview.banner.title', { year })}</Typography>
                <Typography variant="body2" sx={{ color: '#e3e8ff', mt: 0.5 }}>{t('yearInReview.banner.body')}</Typography>
                <Box sx={{ mt: 2 }}>
                    <Button component={Link} to={yearInReviewPath(user.id, year)} variant="contained" color="warning">
                        {t('yearInReview.banner.open')}
                    </Button>
                </Box>
            </CardContent>
            <IconButton onClick={dismiss} aria-label={t('yearInReview.banner.dismiss')} size="small" sx={{ position: 'absolute', top: 8, right: 8, color: 'rgba(255,255,255,0.8)' }}>
                <CloseIcon fontSize="small" />
            </IconButton>
        </Card>
    );
};
