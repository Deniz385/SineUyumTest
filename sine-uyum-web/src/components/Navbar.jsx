import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { useThemeContext } from '../context/ThemeContext';
import { useNotifications } from '../context/NotificationContext';
import { AppBar, Toolbar, Typography, Box, IconButton, Drawer, List, ListItem, ListItemButton, ListItemText, Button, Badge, Menu, MenuItem, Divider, Tooltip } from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import NotificationsIcon from '@mui/icons-material/Notifications';
import MarkEmailReadIcon from '@mui/icons-material/MarkEmailRead';
import { UserSearch } from './UserSearch';
import { MovieSearchBar } from './MovieSearchBar';
import { LanguageSwitcher } from './LanguageSwitcher';

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


export const Navbar = () => {
  const { t, i18n } = useTranslation();
  const { user, logoutAction } = useAuth();
  const { mode, toggleTheme } = useThemeContext();
  const { notifications, unreadCount, markAsRead, markAllAsRead } = useNotifications();
  const navigate = useNavigate();

  const [mobileOpen, setMobileOpen] = useState(false);
  const [anchorEl, setAnchorEl] = useState(null);

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

  const drawer = (
    <Box onClick={handleDrawerToggle} sx={{ textAlign: 'center' }}>
      <Typography variant="h6" sx={{ my: 2 }}>SineUyum</Typography>
      <List>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/feed"><ListItemText primary={t('nav.feed')} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/my-event"><ListItemText primary={t('nav.myEvent')} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/messages"><ListItemText primary={t('nav.messages')} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/watchlist"><ListItemText primary={t('nav.myWatchlist')} /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to={`/profile/${user?.id}`}><ListItemText primary={t('nav.myProfile')} /></ListItemButton>
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
        <Toolbar>
          <IconButton color="inherit" aria-label={t('nav.openMenu')} edge="start" onClick={handleDrawerToggle} sx={{ mr: 2, display: { sm: 'none' } }} >
            <MenuIcon />
          </IconButton>
          <Typography variant="h6" component={Link} to="/home" sx={{ flexGrow: 1, textDecoration: 'none', color: 'inherit' }}>
            SineUyum
          </Typography>

          {user && (
            <Box sx={{ display: { xs: 'none', sm: 'flex' }, alignItems: 'center' }}>
              <MovieSearchBar />
              <UserSearch />
              <Link to="/feed" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>{t('nav.feed')}</Link>
              <Link to="/my-event" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>{t('nav.myEvent')}</Link>
              <Link to="/messages" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>{t('nav.messages')}</Link>
              <Link to="/watchlist" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>{t('nav.myWatchlist')}</Link>
              <Link to={`/profile/${user.id}`} style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>{t('nav.myProfile')}</Link>

              <Tooltip title={t('nav.notifications')}>
                <IconButton color="inherit" onClick={handleNotificationMenuOpen} aria-label={t('nav.notifications')}>
                  <Badge badgeContent={unreadCount} color="error">
                    <NotificationsIcon />
                  </Badge>
                </IconButton>
              </Tooltip>

              <Button onClick={logoutAction} sx={{ color: '#fff' }}>{t('nav.logout')}</Button>
              <LanguageSwitcher sx={{ ml: 1 }} />
              <IconButton sx={{ ml: 1 }} onClick={toggleTheme} color="inherit" aria-label={t('nav.toggleTheme')}>
                {mode === 'dark' ? <Brightness7Icon /> : <Brightness4Icon />}
              </IconButton>
            </Box>
          )}
        </Toolbar>
      </AppBar>

      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={handleNotificationMenuClose} PaperProps={{ sx: { maxHeight: 400, width: '350px' } }}>
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
        ) : (
          <MenuItem disabled>
            <ListItemText primary={t('notifications.empty')} />
          </MenuItem>
        )}
      </Menu>

      <Box component="nav">
        <Drawer variant="temporary" open={mobileOpen} onClose={handleDrawerToggle} ModalProps={{ keepMounted: true }} sx={{ display: { xs: 'block', sm: 'none' }, '& .MuiDrawer-paper': { boxSizing: 'border-box', width: 240 }, }}>
          {drawer}
        </Drawer>
      </Box>
    </>
  );
};
