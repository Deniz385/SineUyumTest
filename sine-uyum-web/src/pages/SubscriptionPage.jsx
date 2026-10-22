import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Alert, Box, Typography, Button, Paper, List, ListItem, ListItemIcon, ListItemText } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { useAuth } from '../context/AuthContext';
import api from '../api/axiosConfig';
import { LoadingState } from '../components/states/LoadingState';
import { ErrorState } from '../components/states/ErrorState';

const formatDate = (iso, language) => new Date(iso).toLocaleDateString(language, { day: 'numeric', month: 'long', year: 'numeric' });

// The subscription status is read from the server (single source of truth).
// The payment-free "test subscription" button was removed: a subscription is
// only activated by a verified payment or by an admin.
export const SubscriptionPage = () => {
    const { t, i18n } = useTranslation();
    const { user, refreshSession } = useAuth();
    const [status, setStatus] = useState(null);
    const [error, setError] = useState('');

    const load = useCallback(async () => {
        setError('');
        try {
            const response = await api.get('/api/v1/subscription');
            setStatus(response.data);
            // If the server's status differs from the token's (e.g. an admin just
            // granted it), the token is refreshed so the event page opens right away.
            if (user && response.data.isActive !== user.isSubscribed) {
                await refreshSession();
            }
        } catch {
            setError(t('subscription.loadError'));
        }
    }, [user, refreshSession, t]);

    useEffect(() => {
        load();
    }, [load]);

    return (
        <Box className="page-container" sx={{ maxWidth: '700px' }}>
            <Paper elevation={3} sx={{ p: 4, textAlign: 'center' }}>
                <Typography variant="h4" component="h1" gutterBottom>
                    SineUyum Premium
                </Typography>
                <Typography variant="h6" color="text.secondary" sx={{ mb: 3 }}>
                    {t('subscription.tagline')}
                </Typography>
                <List sx={{ textAlign: 'left', display: 'inline-block', mb: 3 }}>
                    <ListItem>
                        <ListItemIcon><CheckCircleIcon color="success" /></ListItemIcon>
                        <ListItemText primary={t('subscription.featureGroups')} />
                    </ListItem>
                    <ListItem>
                        <ListItemIcon><CheckCircleIcon color="success" /></ListItemIcon>
                        <ListItemText primary={t('subscription.featureVoting')} />
                    </ListItem>
                    <ListItem>
                        <ListItemIcon><CheckCircleIcon color="success" /></ListItemIcon>
                        <ListItemText primary={t('subscription.featureEvents')} />
                    </ListItem>
                </List>

                {error && <ErrorState compact message={error} onRetry={load} />}
                {!status && !error && <LoadingState compact />}

                {status?.isActive && (
                    <>
                        <Alert severity="success" sx={{ textAlign: 'left', mb: 2 }}>
                            {t(status.willRenew ? 'subscription.activeRenews' : 'subscription.activeEnds', { date: formatDate(status.expiresAt, i18n.language) })}
                        </Alert>
                        <Button component={Link} to="/my-event" variant="contained" size="large">
                            {t('subscription.goToEvents')}
                        </Button>
                    </>
                )}

                {status && !status.isActive && (
                    <Alert severity="info" sx={{ textAlign: 'left' }}>
                        {status.availableProviders.length === 0
                            ? t('subscription.comingSoon')
                            : t('subscription.preparing')}
                    </Alert>
                )}
            </Paper>
        </Box>
    );
};
