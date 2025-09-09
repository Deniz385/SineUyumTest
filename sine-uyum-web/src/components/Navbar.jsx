// sine-uyum-web/src/components/Navbar.jsx
import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useThemeContext } from '../context/ThemeContext';
import { AppBar, Toolbar, Typography, Box, IconButton, Drawer, List, ListItem, ListItemButton, ListItemText, Button } from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import { UserSearch } from './UserSearch';
import { MovieSearchBar } from './MovieSearchBar';

export const Navbar = () => {
  const { token, user, logoutAction } = useAuth();
  const { mode, toggleTheme } = useThemeContext();
  const [mobileOpen, setMobileOpen] = useState(false);

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  // Mobil menüdeki linkleri içeren yapı
  const drawer = (
    <Box onClick={handleDrawerToggle} sx={{ textAlign: 'center' }}>
      <Typography variant="h6" sx={{ my: 2 }}>
        SineUyum
      </Typography>
      <List>
        {/* DÜZELTME 1: Mobil menüye "Etkinliğim" linki eklendi */}
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/my-event">
            <ListItemText primary="Etkinliğim" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/messages">
            <ListItemText primary="Mesajlar" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/watchlist">
            <ListItemText primary="İzleme Listem" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to={`/profile/${user?.id}`}>
            <ListItemText primary="Profilim" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton onClick={logoutAction}>
            <ListItemText primary="Çıkış Yap" />
          </ListItemButton>
        </ListItem>
      </List>
    </Box>
  );

  return (
    <>
      <AppBar component="nav" position="static" sx={{ backgroundColor: '#212529' }}>
        <Toolbar>
          <IconButton
            color="inherit"
            aria-label="open drawer"
            edge="start"
            onClick={handleDrawerToggle}
            sx={{ mr: 2, display: { sm: 'none' } }}
          >
            <MenuIcon />
          </IconButton>

          <Typography
            variant="h6"
            component={Link}
            to="/home"
            sx={{ flexGrow: 1, textDecoration: 'none', color: 'inherit' }}
          >
            SineUyum
          </Typography>

          {token && user && (
            <Box sx={{ display: { xs: 'none', sm: 'flex' }, alignItems: 'center' }}>
              <MovieSearchBar />
              <UserSearch />
              {/* DÜZELTME 2: Geniş ekran menüsündeki link güncellendi */}
              <Link to="/my-event" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>Etkinliğim</Link>
              <Link to="/messages" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>Mesajlar</Link>
              <Link to="/watchlist" style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>İzleme Listem</Link>
              <Link to={`/profile/${user.id}`} style={{ color: '#fff', margin: '0 15px', textDecoration: 'none' }}>Profilim</Link>
              <Button onClick={logoutAction} sx={{ color: '#fff' }}>Çıkış Yap</Button>
              <IconButton sx={{ ml: 1 }} onClick={toggleTheme} color="inherit">
                {mode === 'dark' ? <Brightness7Icon /> : <Brightness4Icon />}
              </IconButton>
            </Box>
          )}
        </Toolbar>
      </AppBar>

      <Box component="nav">
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={handleDrawerToggle}
          ModalProps={{ keepMounted: true }}
          sx={{
            display: { xs: 'block', sm: 'none' },
            '& .MuiDrawer-paper': { boxSizing: 'border-box', width: 240 },
          }}
        >
          {drawer}
        </Drawer>
      </Box>
    </>
  );
};