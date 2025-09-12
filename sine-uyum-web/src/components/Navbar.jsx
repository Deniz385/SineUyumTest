import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useThemeContext } from '../context/ThemeContext';
import { useNotifications } from '../context/NotificationContext'; // <-- BİLDİRİM CONTEXT'İ
import { AppBar, Toolbar, Typography, Box, IconButton, Drawer, List, ListItem, ListItemButton, ListItemText, Button, Badge, Menu, MenuItem, Divider, ListItemIcon, Tooltip } from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import NotificationsIcon from '@mui/icons-material/Notifications'; // <-- ZİL İKONU
import MarkEmailReadIcon from '@mui/icons-material/MarkEmailRead';
import { UserSearch } from './UserSearch';
import { MovieSearchBar } from './MovieSearchBar';

// Zamanı "5 dakika önce" gibi göstermek için yardımcı fonksiyon
const timeSince = (date) => {
  const seconds = Math.floor((new Date() - new Date(date)) / 1000);
  let interval = seconds / 31536000;
  if (interval > 1) return Math.floor(interval) + " yıl önce";
  interval = seconds / 2592000;
  if (interval > 1) return Math.floor(interval) + " ay önce";
  interval = seconds / 86400;
  if (interval > 1) return Math.floor(interval) + " gün önce";
  interval = seconds / 3600;
  if (interval > 1) return Math.floor(interval) + " saat önce";
  interval = seconds / 60;
  if (interval > 1) return Math.floor(interval) + " dakika önce";
  return "şimdi";
};


export const Navbar = () => {
  const { user, logoutAction } = useAuth();
  const { mode, toggleTheme } = useThemeContext();
  const { notifications, unreadCount, markAsRead, markAllAsRead } = useNotifications(); // <-- BİLDİRİM VERİLERİNİ AL
  const navigate = useNavigate();

  const [mobileOpen, setMobileOpen] = useState(false);
  const [anchorEl, setAnchorEl] = useState(null); // <-- BİLDİRİM MENÜSÜ İÇİN STATE

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
          <ListItemButton component={Link} to="/my-event"><ListItemText primary="Etkinliğim" /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/messages"><ListItemText primary="Mesajlar" /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/watchlist"><ListItemText primary="İzleme Listem" /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to={`/profile/${user?.id}`}><ListItemText primary="Profilim" /></ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton onClick={logoutAction}><ListItemText primary="Çıkış Yap" /></ListItemButton>
        </ListItem>
      </List>
    </Box>
  );

  return (
    <>
      <AppBar component="nav" position="static" sx={{ backgroundColor: '#212529' }}>
        <Toolbar>
          <IconButton color="inherit" aria-label="open drawer" edge="start" onClick={handleDrawerToggle} sx={{ mr: 2, display: { sm: 'none' } }} >
            <MenuIcon />
          </IconButton>
          <Typography variant="h6" component={Link} to="/home" sx={{ flexGrow: 1, textDecoration: 'none', color: 'inherit' }}>
            SineUyum
          </Typography>

          {user && (
            <Box sx={{ display: { xs: 'none', sm: 'flex' }, alignItems: 'center' }}>
              <MovieSearchBar />
              <UserSearch />
              <Link to="/my-event" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>Etkinliğim</Link>
              <Link to="/messages" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>Mesajlar</Link>
              <Link to="/watchlist" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>İzleme Listem</Link>
              <Link to={`/profile/${user.id}`} style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>Profilim</Link>
              
              {/* --- YENİ BİLDİRİM İKONU --- */}
              <Tooltip title="Bildirimler">
                <IconButton color="inherit" onClick={handleNotificationMenuOpen}>
                  <Badge badgeContent={unreadCount} color="error">
                    <NotificationsIcon />
                  </Badge>
                </IconButton>
              </Tooltip>

              <Button onClick={logoutAction} sx={{ color: '#fff' }}>Çıkış Yap</Button>
              <IconButton sx={{ ml: 1 }} onClick={toggleTheme} color="inherit">
                {mode === 'dark' ? <Brightness7Icon /> : <Brightness4Icon />}
              </IconButton>
            </Box>
          )}
        </Toolbar>
      </AppBar>
      
      {/* --- YENİ BİLDİRİM MENÜSÜ --- */}
      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={handleNotificationMenuClose} PaperProps={{ sx: { maxHeight: 400, width: '350px' } }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', px: 2, py: 1 }}>
            <Typography variant="subtitle1" fontWeight="bold">Bildirimler</Typography>
            {unreadCount > 0 && (
                <Button size="small" startIcon={<MarkEmailReadIcon />} onClick={handleMarkAllRead}>Tümünü Okundu İşaretle</Button>
            )}
        </Box>
        <Divider />
        {notifications.length > 0 ? (
          notifications.map(notification => (
            <MenuItem key={notification.id} onClick={() => handleNotificationClick(notification)} sx={{ backgroundColor: notification.isRead ? 'transparent' : 'action.hover', whiteSpace: 'normal' }}>
              <ListItemText 
                primary={<Typography variant="body2">{notification.message}</Typography>} 
                secondary={timeSince(notification.createdAt)} 
              />
            </MenuItem>
          ))
        ) : (
          <MenuItem disabled>
            <ListItemText primary="Yeni bildirim yok." />
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

