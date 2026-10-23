import React, { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { useThemeContext } from '../context/ThemeContext';
import { useNotifications } from '../context/NotificationContext';
import {
  AppBar, Toolbar, Typography, Box, IconButton, ListItemIcon, ListItemText, Button, Badge, Menu, MenuItem, Divider,
  Tooltip, Dialog, DialogContent, Slide,
} from '@mui/material';
import NotificationsRoundedIcon from '@mui/icons-material/NotificationsRounded';
import NotificationsNoneRoundedIcon from '@mui/icons-material/NotificationsNoneRounded';
import MarkEmailReadRoundedIcon from '@mui/icons-material/MarkEmailReadRounded';
import PersonRoundedIcon from '@mui/icons-material/PersonRounded';
import BookmarksRoundedIcon from '@mui/icons-material/BookmarksRounded';
import EditCalendarRoundedIcon from '@mui/icons-material/EditCalendarRounded';
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded';
import SettingsRoundedIcon from '@mui/icons-material/SettingsRounded';
import LockRoundedIcon from '@mui/icons-material/LockRounded';
import DarkModeRoundedIcon from '@mui/icons-material/DarkModeRounded';
import LightModeRoundedIcon from '@mui/icons-material/LightModeRounded';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import CheckRoundedIcon from '@mui/icons-material/CheckRounded';
import TranslateRoundedIcon from '@mui/icons-material/TranslateRounded';
import AdminPanelSettingsRoundedIcon from '@mui/icons-material/AdminPanelSettingsRounded';
import SearchRoundedIcon from '@mui/icons-material/SearchRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import { MovieSearchBar } from './MovieSearchBar';
import { NAV_LINKS, isNavActive, timeSince } from './navigation';
import { EmptyState } from './states/EmptyState';
import { ErrorState } from './states/ErrorState';
import { UserAvatar } from './ui/UserRow';
import { Wordmark } from './ui/Wordmark';
import { SUPPORTED_LANGUAGES, changeLanguage } from '../i18n';
import { defaultRecapYear, yearInReviewPath } from './yearInReview';

const WIDE = '@media (min-width:1024px)';

const navLinkSx = {
  position: 'relative', flexShrink: 0, whiteSpace: 'nowrap', color: 'text.secondary', minWidth: 0,
  px: 1.5, minHeight: 40, borderRadius: 999, gap: 0.75,
  '&:hover': { color: 'text.primary', bgcolor: 'action.hover' },
  '&[aria-current="page"]': {
    color: 'text.primary',
    '&::after': {
      content: '""', position: 'absolute', left: '50%', bottom: -12, width: 24, height: 3, ml: '-12px',
      borderRadius: '3px 3px 0 0', bgcolor: 'primary.main',
    },
  },
};

const NotificationsMenu = ({ anchorEl, onClose }) => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { notifications, unreadCount, markAsRead, markAllAsRead, notificationsLoadError, reloadNotifications } = useNotifications();

  const handleNotificationClick = (notification) => {
    if (!notification.isRead) markAsRead(notification.id);
    onClose();
    if (notification.relatedUrl) navigate(notification.relatedUrl);
  };

  return (
    <Menu
      anchorEl={anchorEl}
      open={Boolean(anchorEl)}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      slotProps={{ paper: { sx: { maxHeight: 460, width: 380, maxWidth: 'calc(100vw - 24px)' } } }}
    >
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', px: 1.5, py: 1, gap: 1 }}>
        <Typography variant="h3" component="h2">{t('notifications.title')}</Typography>
        {unreadCount > 0 && (
          <Button size="small" startIcon={<MarkEmailReadRoundedIcon />} onClick={markAllAsRead}>{t('notifications.markAllRead')}</Button>
        )}
      </Box>
      <Divider sx={{ mb: 0.5 }} />
      {notifications.length > 0 ? (
        notifications.map((notification) => (
          <MenuItem
            key={notification.id}
            onClick={() => handleNotificationClick(notification)}
            sx={{ whiteSpace: 'normal', alignItems: 'flex-start', gap: 1.25, py: 1.25 }}
          >
            <Box
              aria-hidden="true"
              sx={{ width: 8, height: 8, mt: 0.75, borderRadius: '50%', flexShrink: 0, bgcolor: notification.isRead ? 'transparent' : 'primary.main' }}
            />
            <ListItemText
              primary={<Typography variant="body1" sx={{ fontWeight: notification.isRead ? 400 : 600 }}>{notification.message}</Typography>}
              secondary={timeSince(notification.createdAt, i18n.language, t('notifications.justNow'))}
              secondaryTypographyProps={{ variant: 'body2', color: 'text.secondary' }}
              sx={{ m: 0 }}
            />
          </MenuItem>
        ))
      ) : notificationsLoadError ? (
        <ErrorState compact message={t('notifications.loadError')} onRetry={reloadNotifications} />
      ) : (
        <EmptyState compact icon={NotificationsNoneRoundedIcon} title={t('notifications.empty')} />
      )}
    </Menu>
  );
};

const AccountMenu = ({ anchorEl, onClose }) => {
  const { t, i18n } = useTranslation();
  const { user, logoutAction } = useAuth();
  const { mode, toggleTheme } = useThemeContext();
  if (!user) return null;

  const chooseLanguage = (code) => {
    onClose();
    if (code !== i18n.language) changeLanguage(code);
  };
  const handleLogout = () => {
    onClose();
    logoutAction();
  };
  const themeLabel = mode === 'dark' ? t('nav.lightTheme') : t('nav.darkTheme');
  const ThemeIcon = mode === 'dark' ? LightModeRoundedIcon : DarkModeRoundedIcon;
  const linkItem = (key, to, Icon, label) => (
    <MenuItem key={key} component={Link} to={to} onClick={onClose}>
      <ListItemIcon><Icon fontSize="small" /></ListItemIcon>
      {label}
    </MenuItem>
  );

  return (
    <Menu
      id="account-menu"
      variant="menu"
      anchorEl={anchorEl}
      open={Boolean(anchorEl)}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      slotProps={{ paper: { sx: { minWidth: 260, maxHeight: 'calc(100vh - 88px)' } } }}
    >
      <Box key="who" sx={{ display: 'flex', alignItems: 'center', gap: 1.5, px: 1.5, py: 1 }}>
        <UserAvatar user={{ userName: user.username, profileImageUrl: user.profileImageUrl }} size={40} />
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="subtitle1" noWrap>@{user.username}</Typography>
          <Typography variant="body2" color="text.secondary" noWrap>{t('nav.signedInAs', { userName: user.username })}</Typography>
        </Box>
      </Box>
      <Divider key="d0" />
      {linkItem('profile', `/profile/${user.id}`, PersonRoundedIcon, t('nav.myProfile'))}
      {linkItem('lists', '/watchlist', BookmarksRoundedIcon, t('nav.myLists'))}
      {linkItem('diary', `/profile/${user.id}?tab=diary`, EditCalendarRoundedIcon, t('nav.diary'))}
      {linkItem('recap', yearInReviewPath(user.id, defaultRecapYear()), AutoAwesomeRoundedIcon, t('nav.yearInReview'))}
      <Divider key="d1" />
      <Typography key="settings-h" variant="overline" component="div" sx={{ px: 1.5, pt: 0.5, pb: 0.25, color: 'text.muted', display: 'block' }}>
        {t('nav.settings')}
      </Typography>
      {linkItem('edit', '/profile/edit', SettingsRoundedIcon, t('nav.editProfile'))}
      {linkItem('privacy', '/profile/edit#privacy', LockRoundedIcon, t('nav.privacy'))}
      <MenuItem key="theme" onClick={toggleTheme}>
        <ListItemIcon><ThemeIcon fontSize="small" /></ListItemIcon>
        {themeLabel}
      </MenuItem>
      <Box key="language" sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.5, py: 0.75 }}>
        <TranslateRoundedIcon fontSize="small" sx={{ color: 'text.secondary', mr: 0.5 }} aria-hidden="true" />
        <Typography variant="body1" component="span" id="account-language-label" sx={{ flex: 1 }}>{t('language.label')}</Typography>
      </Box>
      {SUPPORTED_LANGUAGES.map((l) => (
        <MenuItem key={`lang-${l.code}`} selected={l.code === i18n.language} onClick={() => chooseLanguage(l.code)} lang={l.code} sx={{ pl: 6.5 }}>
          <ListItemText primary={l.label} />
          {l.code === i18n.language && <CheckRoundedIcon fontSize="small" sx={{ color: 'primary.main' }} />}
        </MenuItem>
      ))}
      {user.roles?.includes('Admin') && <Divider key="d-admin" />}
      {user.roles?.includes('Admin') && linkItem('admin', '/admin', AdminPanelSettingsRoundedIcon, t('nav.admin'))}
      <Divider key="d2" />
      <MenuItem key="logout" onClick={handleLogout}>
        <ListItemIcon><LogoutRoundedIcon fontSize="small" /></ListItemIcon>
        {t('nav.logout')}
      </MenuItem>
    </Menu>
  );
};

const SlideDown = React.forwardRef(function SlideDown(props, ref) {
  return <Slide direction="down" ref={ref} {...props} />;
});

export const Navbar = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { unreadCount } = useNotifications();
  const { pathname } = useLocation();

  const [notificationAnchor, setNotificationAnchor] = useState(null);
  const [accountAnchor, setAccountAnchor] = useState(null);
  const [searchOpen, setSearchOpen] = useState(false);

  const notificationsLabel = unreadCount > 0 ? t('nav.notificationsUnread', { count: unreadCount }) : t('nav.notifications');

  return (
    <>
      <Box
        component="a"
        href="#main"
        sx={{
          position: 'absolute', left: 8, top: -48, zIndex: 2000, px: 2, py: 1, borderRadius: 999,
          bgcolor: 'primary.main', color: 'primary.contrastText', fontWeight: 600,
          '&:focus': { top: 8 },
        }}
      >
        {t('nav.skipToContent')}
      </Box>
      <AppBar component="header" position="sticky">
        <Toolbar sx={{ gap: { xs: 0.5, md: 1.5 }, px: { xs: 1.5, sm: 2, md: 3 } }}>
          <Box component={Link} to="/home" aria-label={t('nav.homeLink')} sx={{ display: 'inline-flex', alignItems: 'center', flexShrink: 0, borderRadius: 1, mr: { md: 1 } }}>
            <Wordmark />
          </Box>

          {user && (
            <Box sx={{ display: { xs: 'none', md: 'flex' }, flex: '1 1 340px', minWidth: 200, maxWidth: 420 }}>
              <MovieSearchBar />
            </Box>
          )}

          <Box sx={{ flexGrow: 1 }} />

          {user && (
            <Box component="nav" aria-label={t('nav.mainNav')} data-testid="desktop-nav" sx={{ display: { xs: 'none', md: 'flex' }, alignItems: 'center', gap: 0.5 }}>
              {NAV_LINKS.map((link) => {
                const active = isNavActive(link, pathname);
                const label = t(link.labelKey);
                return (
                  <Tooltip key={link.to} title={label} slotProps={{ popper: { sx: { [WIDE]: { display: 'none' } } } }}>
                    <Button
                      component={Link}
                      to={link.to}
                      aria-current={active ? 'page' : undefined}
                      aria-label={label}
                      sx={navLinkSx}
                      className={active ? 'active' : undefined}
                    >
                      <link.Icon fontSize="small" aria-hidden="true" />
                      <Box component="span" sx={{ display: 'none', [WIDE]: { display: 'inline' } }}>{label}</Box>
                    </Button>
                  </Tooltip>
                );
              })}
            </Box>
          )}

          {user && (
            <Tooltip title={t('nav.search')}>
              <IconButton onClick={() => setSearchOpen(true)} aria-label={t('nav.search')} sx={{ display: { md: 'none' }, color: 'text.secondary' }}>
                <SearchRoundedIcon />
              </IconButton>
            </Tooltip>
          )}

          {user && (
            <Tooltip title={t('nav.notifications')}>
              <IconButton
                onClick={(e) => setNotificationAnchor(e.currentTarget)}
                aria-label={notificationsLabel}
                aria-haspopup="true"
                sx={{ flexShrink: 0, color: 'text.secondary', '&:hover': { color: 'text.primary' } }}
              >
                <Badge badgeContent={unreadCount} color="secondary" max={99}>
                  <NotificationsRoundedIcon />
                </Badge>
              </IconButton>
            </Tooltip>
          )}

          {user && (
            <Tooltip title={t('nav.account')}>
              <IconButton
                onClick={(e) => setAccountAnchor(e.currentTarget)}
                aria-label={t('nav.account')}
                aria-controls={accountAnchor ? 'account-menu' : undefined}
                aria-haspopup="true"
                aria-expanded={accountAnchor ? 'true' : undefined}
                sx={{ flexShrink: 0, p: 0.5 }}
              >
                <UserAvatar user={{ userName: user.username, profileImageUrl: user.profileImageUrl }} size={32} />
              </IconButton>
            </Tooltip>
          )}
        </Toolbar>
      </AppBar>

      <AccountMenu anchorEl={accountAnchor} onClose={() => setAccountAnchor(null)} />
      <NotificationsMenu anchorEl={notificationAnchor} onClose={() => setNotificationAnchor(null)} />

      {/* Phones: the movie search opens as a sheet from the top. */}
      <Dialog
        open={searchOpen}
        onClose={() => setSearchOpen(false)}
        fullWidth
        slots={{ transition: SlideDown }}
        aria-label={t('nav.search')}
        slotProps={{ paper: { sx: { position: 'absolute', top: 0, m: 1, width: 'calc(100% - 16px)', borderRadius: 2.5 } } }}
      >
        <DialogContent sx={{ display: 'flex', alignItems: 'center', gap: 1, p: 1.5 }}>
          <MovieSearchBar autoFocus onNavigate={() => setSearchOpen(false)} />
          <IconButton onClick={() => setSearchOpen(false)} aria-label={t('common.close')}><CloseRoundedIcon /></IconButton>
        </DialogContent>
      </Dialog>
    </>
  );
};
