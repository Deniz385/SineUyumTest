import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, ButtonBase, Card, Typography } from '@mui/material';
import NotificationsNoneRoundedIcon from '@mui/icons-material/NotificationsNoneRounded';
import MarkEmailReadRoundedIcon from '@mui/icons-material/MarkEmailReadRounded';
import { useNotifications } from '../context/NotificationContext';
import { Page, PageHeader } from '../components/ui/Page';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { timeSince } from '../components/navigation';
import { srOnly } from '../components/ui/a11y';

// All notifications (the top bar menu shows the same list in short).
export const NotificationsPage = () => {
    const { t, i18n } = useTranslation();
    const navigate = useNavigate();
    const { notifications, unreadCount, markAsRead, markAllAsRead, notificationsLoadError, reloadNotifications } = useNotifications();

    const open = (notification) => {
        if (!notification.isRead) markAsRead(notification.id);
        if (notification.relatedUrl) navigate(notification.relatedUrl);
    };

    return (
        <Page width="reading">
            <PageHeader
                title={t('notifications.title')}
                actions={unreadCount > 0 ? (
                    <Button variant="tonal" startIcon={<MarkEmailReadRoundedIcon />} onClick={markAllAsRead}>{t('notifications.markAllRead')}</Button>
                ) : null}
            />
            {notifications.length > 0 ? (
                <Card component="ul" data-testid="notifications-list" sx={{ listStyle: 'none', m: 0, p: 0.75 }}>
                    {notifications.map((n) => (
                        <li key={n.id}>
                            <ButtonBase
                                onClick={() => open(n)}
                                sx={{ width: '100%', justifyContent: 'flex-start', textAlign: 'left', gap: 1.5, p: 1.5, borderRadius: 1.5, alignItems: 'flex-start', '&:hover': { bgcolor: 'action.hover' } }}
                            >
                                <Box aria-hidden="true" sx={{ width: 10, height: 10, mt: 0.75, borderRadius: '50%', flexShrink: 0, bgcolor: n.isRead ? 'transparent' : 'primary.main', border: n.isRead ? 1 : 0, borderColor: 'divider' }} />
                                <Box sx={{ minWidth: 0 }}>
                                    <Typography variant="body1" sx={{ color: 'text.primary', fontWeight: n.isRead ? 400 : 600 }}>
                                        {!n.isRead && <Box component="span" sx={srOnly}>{t('notifications.unread')}: </Box>}
                                        {n.message}
                                    </Typography>
                                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>{timeSince(n.createdAt, i18n.language, t('notifications.justNow'))}</Typography>
                                </Box>
                            </ButtonBase>
                        </li>
                    ))}
                </Card>
            ) : notificationsLoadError ? (
                <ErrorState message={t('notifications.loadError')} onRetry={reloadNotifications} />
            ) : (
                <EmptyState icon={NotificationsNoneRoundedIcon} title={t('notifications.empty')} />
            )}
        </Page>
    );
};
