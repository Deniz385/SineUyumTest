import React, { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { useThemeContext } from '../context/ThemeContext';
import { useNotifications } from '../context/NotificationContext';
import {
  AppBar, Toolbar, Typography, Box, IconButton, Drawer, List, ListItem, ListItemButton, ListItemIcon, ListItemText,
  ListSubheader, Button, Badge, Menu, MenuItem, Divider, Tooltip, Avatar
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import NotificationsIcon from '@mui/icons-material/Notifications';
import NotificationsNoneIcon from '@mui/icons-material/NotificationsNone';
import MarkEmailReadIcon from '@mui/icons-material/MarkEmailRead';
import DynamicFeedIcon from '@mui/icons-material/DynamicFeed';
import EventIcon from '@mui/icons-material/Event';
import ChatIcon from '@mui/icons-material/Chat';
import BookmarksIcon from '@mui/icons-material/Bookmarks';
import PersonIcon from '@mui/icons-material/Person';
import EditIcon from '@mui/icons-material/Edit';
import LockIcon from '@mui/icons-material/Lock';
import LogoutIcon from '@mui/icons-material/Logout';
import CheckIcon from '@mui/icons-material/Check';
import { UserSearch } from './UserSearch';
import { MovieSearchBar } from './MovieSearchBar';
import { LanguageSwitcher } from './LanguageSwitcher';
import { EmptyState } from './states/EmptyState';
import { ErrorState } from './states/ErrorState';
import { SUPPORTED_LANGUAGES, changeLanguage } from '../i18n';

const TIME_UNITS = [
  ['year', 31536000],
  ['month', 2592000],
  ['day', 86400],
  ['hour', 3600],
  ['minute', 60],
];

// Shows a time as "5 minutes ago" in the active language.
const timeSince = (date, language, justNow) => {
  const seconds = Math.floor((new Date() - new Date(date)) / 1000);
  const format = new Intl.RelativeTimeFormat(language, { numeric: 'always' });
  for (const [unit, unitSeconds] of TIME_UNITS) {
    const interval = seconds / unitSeconds;
    if (interval > 1) return format.format(-Math.floor(interval), unit);
  }
  return justNow;
};

// Main sections. From `lg` up they are text links; between `md` and `lg`
// icons with tooltips, so the search boxes keep their room; below `md` they
// live in the drawer. Profile, settings, language, theme and log out are in
// the account menu (desktop) or the drawer (mobile).
const NAV_LINKS = [
  { to: '/feed', labelKey: 'nav.feed', Icon: DynamicFeedIcon },
  { to: '/my-event', labelKey: 'nav.myEvent', Icon: EventIcon },
  { to: '/messages', labelKey: 'nav.messages', Icon: ChatIcon },
  { to: '/watchlist', labelKey: 'nav.myWatchlist', Icon: BookmarksIcon },
];

const activeLinkSx = { '&.active': { backgroundColor: 'rgba(255, 255, 255, 0.14)' } };

export const Navbar = () => {
  const { t, i18n } = useTranslation();
  const { user, logoutAction } = useAuth();
  const { mode, toggleTheme } = useThemeContext();
  const { notifications, unreadCount, markAsRead, markAllAsRead, notificationsLoadError, reloadNotifications } = useNotifications();
  const navigate = useNavigate();

  const [mobileOpen, setMobileOpen] = useState(false);
  const [anchorEl, setAnchorEl] = useState(null);
  const [accountAnchorEl, setAccountAnchorEl] = useState(null);

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  const handleNotificationMenuOpen = (event) => {
    setAnchorEl(event.currentTarget);
  };

  const handleNotificationMenuClose = () => {
    setAnchorEl(null);
  };

  const handleNotificationClick = (notification) => {
    if (!notification.isRead) {
        markAsRead(notification.id);
    }
    handleNotificationMenuClose();
    if (notification.relatedUrl) {
      navigate(notification.relatedUrl);
    }
  };

  const handleMarkAllRead = () => {
      markAllAsRead();
  }

  const closeAccountMenu = () => setAccountAnchorEl(null);

  const chooseLanguage = (code) => {
    closeAccountMenu();
    if (code !== i18n.language) changeLanguage(code);
  };

  const handleLogout = () => {
    closeAccountMenu();
    logoutAction();
  };

  const themeLabel = mode === 'dark' ? t('nav.lightTheme') : t('nav.darkTheme');
  const ThemeIcon = mode === 'dark' ? Brightness7Icon : Brightness4Icon;
  const userInitial = user?.username?.charAt(0).toUpperCase();

  const drawer = (
    <Box onClick={handleDrawerToggle} sx={{ textAlign: 'center' }}>
      <Typography variant="h6" sx={{ my: 2 }}>SineUyum</Typography>
      <List>
        {NAV_LINKS.map(({ to, labelKey }) => (
          <ListItem key={to} disablePadding>
            <ListItemButton component={Link} to={to}><ListItemText primary={t(labelKey)} /></ListItemButton>
          </ListItem>
        ))}
        <ListItem disablePadding>
          <ListItemButton component={Link} to={`/profile/${user?.id}`}><ListItemText primary={t('nav.myProfile')} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/profile/edit"><ListItemText primary={t('nav.editProfile')} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/profile/edit#privacy"><ListItemText primary={t('nav.privacy')} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton onClick={toggleTheme}><ListItemText primary={themeLabel} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton onClick={logoutAction}><ListItemText primary={t('nav.logout')} /></ListItemButton>
        </ListItem>
      </List>
      <Divider />
      <LanguageSwitcher variant="list" />
    </Box>
  );

  return (
    <>
      <AppBar component="nav" position="static" sx={{ backgroundColor: '#212529' }}>
        <Toolbar sx={{ gap: 1 }}>
          <IconButton color="inherit" aria-label={t('nav.openMenu')} edge="start" onClick={handleDrawerToggle} sx={{ mr: 1, display: { md: 'none' } }} >
            <MenuIcon />
          </IconButton>
          <Typography
            variant="h6"
            component={Link}
            to="/home"
            sx={{ flexGrow: { xs: 1, md: 0 }, flexShrink: 0, mr: { md: 1 }, whiteSpace: 'nowrap', textDecoration: 'none', color: 'inherit' }}
          >
            SineUyum
          </Typography>

          {user && (
            <Box data-testid="desktop-nav" sx={{ display: { xs: 'none', md: 'flex' }, alignItems: 'center', gap: 1, flex: 1, minWidth: 0 }}>
              <Box sx={{ flex: '1 1 340px', minWidth: 180, maxWidth: 440 }}>
                <MovieSearchBar />
              </Box>
              <Box sx={{ flex: '1 1 220px', minWidth: 150, maxWidth: 260 }}>
                <UserSearch />
              </Box>
              <Box sx={{ flexGrow: 1 }} />
              {NAV_LINKS.map((link) => (
                <React.Fragment key={link.to}>
                  <Button
                    component={NavLink}
                    to={link.to}
                    color="inherit"
                    sx={{
                      display: { xs: 'none', lg: 'inline-flex' }, flexShrink: 0, whiteSpace: 'nowrap',
                      textTransform: 'none', fontSize: '1rem', fontWeight: 400, px: 1.25, minWidth: 0, ...activeLinkSx,
                    }}
                  >
                    {t(link.labelKey)}
                  </Button>
                  <Tooltip title={t(link.labelKey)}>
                    <IconButton
                      component={NavLink}
                      to={link.to}
                      color="inherit"
                      aria-label={t(link.labelKey)}
                      sx={{ display: { xs: 'none', md: 'inline-flex', lg: 'none' }, flexShrink: 0, ...activeLinkSx }}
                    >
                      <link.Icon />
                    </IconButton>
                  </Tooltip>
                </React.Fragment>
              ))}
            </Box>
          )}

          {user && (
            <Tooltip title={t('nav.notifications')}>
              <IconButton color="inherit" onClick={handleNotificationMenuOpen} aria-label={t('nav.notifications')} sx={{ flexShrink: 0 }}>
                <Badge badgeContent={unreadCount} color="error">
                  <NotificationsIcon />
                </Badge>
              </IconButton>
            </Tooltip>
          )}

          {user && (
            <Tooltip title={t('nav.account')}>
              <IconButton
                onClick={(e) => setAccountAnchorEl(e.currentTarget)}
                aria-label={t('nav.account')}
                aria-controls={accountAnchorEl ? 'account-menu' : undefined}
                aria-haspopup="true"
                aria-expanded={accountAnchorEl ? 'true' : undefined}
                sx={{ display: { xs: 'none', md: 'inline-flex' }, flexShrink: 0, p: 0.5 }}
              >
                <Avatar sx={{ width: 32, height: 32, fontSize: '1rem', bgcolor: 'primary.main' }}>{userInitial}</Avatar>
              </IconButton>
            </Tooltip>
          )}
        </Toolbar>
      </AppBar>

      <Menu
        id="account-menu"
        variant="menu"
        anchorEl={accountAnchorEl}
        open={Boolean(accountAnchorEl)}
        onClose={closeAccountMenu}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { minWidth: 240 } } }}
      >
        {user && [
          <ListSubheader key="who" sx={{ lineHeight: 1.5, py: 1, bgcolor: 'transparent' }}>
            {t('nav.signedInAs', { userName: user.username })}
          </ListSubheader>,
          <MenuItem key="profile" component={Link} to={`/profile/${user.id}`} onClick={closeAccountMenu}>
            <ListItemIcon><PersonIcon fontSize="small" /></ListItemIcon>
            {t('nav.myProfile')}
          </MenuItem>,
          <MenuItem key="edit" component={Link} to="/profile/edit" onClick={closeAccountMenu}>
            <ListItemIcon><EditIcon fontSize="small" /></ListItemIcon>
            {t('nav.editProfile')}
          </MenuItem>,
          <MenuItem key="privacy" component={Link} to="/profile/edit#privacy" onClick={closeAccountMenu}>
            <ListItemIcon><LockIcon fontSize="small" /></ListItemIcon>
            {t('nav.privacy')}
          </MenuItem>,
          <MenuItem key="theme" onClick={toggleTheme}>
            <ListItemIcon><ThemeIcon fontSize="small" /></ListItemIcon>
            {themeLabel}
          </MenuItem>,
          <Divider key="d1" />,
          <ListSubheader key="language" sx={{ lineHeight: 2, bgcolor: 'transparent' }}>{t('language.label')}</ListSubheader>,
          ...SUPPORTED_LANGUAGES.map((l) => (
            <MenuItem key={`lang-${l.code}`} selected={l.code === i18n.language} onClick={() => chooseLanguage(l.code)} lang={l.code}>
              <ListItemIcon>{l.code === i18n.language && <CheckIcon fontSize="small" />}</ListItemIcon>
              {l.label}
            </MenuItem>
          )),
          <Divider key="d2" />,
          <MenuItem key="logout" onClick={handleLogout}>
            <ListItemIcon><LogoutIcon fontSize="small" /></ListItemIcon>
            {t('nav.logout')}
          </MenuItem>,
        ]}
      </Menu>

      <Menu
        anchorEl={anchorEl}
        open={Boolean(anchorEl)}
        onClose={handleNotificationMenuClose}
        slotProps={{ paper: { sx: { maxHeight: 400, width: 350, maxWidth: 'calc(100vw - 32px)' } } }}
      >
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', px: 2, py: 1 }}>
            <Typography variant="subtitle1" fontWeight="bold">{t('notifications.title')}</Typography>
            {unreadCount > 0 && (
                <Button size="small" startIcon={<MarkEmailReadIcon />} onClick={handleMarkAllRead}>{t('notifications.markAllRead')}</Button>
            )}
        </Box>
        <Divider />
        {notifications.length > 0 ? (
          notifications.map(notification => (
            <MenuItem key={notification.id} onClick={() => handleNotificationClick(notification)} sx={{ backgroundColor: notification.isRead ? 'transparent' : 'action.hover', whiteSpace: 'normal' }}>
              <ListItemText
                primary={<Typography variant="body2">{notification.message}</Typography>}
                secondary={timeSince(notification.createdAt, i18n.language, t('notifications.justNow'))}
              />
            </MenuItem>
          ))
        ) : notificationsLoadError ? (
          <ErrorState compact message={t('notifications.loadError')} onRetry={reloadNotifications} />
        ) : (
          <EmptyState compact icon={NotificationsNoneIcon} title={t('notifications.empty')} />
        )}
      </Menu>

      <Box component="nav">
        <Drawer variant="temporary" open={mobileOpen} onClose={handleDrawerToggle} ModalProps={{ keepMounted: true }} sx={{ display: { xs: 'block', md: 'none' }, '& .MuiDrawer-paper': { boxSizing: 'border-box', width: 240 }, }}>
          {drawer}
        </Drawer>
      </Box>
    </>
  );
};
