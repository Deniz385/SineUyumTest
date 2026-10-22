import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { TextField, Button, Box, Typography, CircularProgress } from '@mui/material';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { LanguageSwitcher } from '../components/LanguageSwitcher';

export const LoginPage = () => {
  const { t } = useTranslation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { loginAction } = useAuth();
  const { showSnackbar } = useSnackbar();

  const handleLogin = async (event) => {
    event.preventDefault();
    setIsLoading(true);

    try {
      const response = await api.post(`/api/v1/account/login`, {
        username: username,
        password: password,
      });

      // The response contains { token, refreshToken, expiresAt, user }.
      loginAction(response.data);

    } catch (err) {
      const errorMessage = err.response?.data?.message || t('auth.login.failed');
      showSnackbar(errorMessage, 'error');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <Box
      sx={{
        width: '100%',
        maxWidth: 400,
        margin: '80px auto',
        padding: 4,
        boxShadow: 3,
        borderRadius: 2,
        // Background colour comes from the theme
        backgroundColor: 'background.paper',
        textAlign: 'center'
      }}
    >
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: -2, mr: -2 }}>
        <LanguageSwitcher variant="text" />
      </Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('auth.login.title')}
      </Typography>
      <Box component="form" onSubmit={handleLogin} sx={{ mt: 1 }}>
        <TextField
          margin="normal"
          required
          fullWidth
          id="username"
          label={t('auth.username')}
          name="username"
          autoComplete="username"
          autoFocus
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <TextField
          margin="normal"
          required
          fullWidth
          name="password"
          label={t('auth.password')}
          type="password"
          id="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <Button
          type="submit"
          fullWidth
          variant="contained"
          sx={{ mt: 3, mb: 2, py: 1.5 }}
          disabled={isLoading}
        >
          {isLoading ? <CircularProgress size={24} /> : t('auth.login.submit')}
        </Button>
        <Typography variant="body2">
          {t('auth.login.noAccount')}{' '}
          <Link to="/register" style={{ textDecoration: 'none' }}>
            {t('auth.login.registerLink')}
          </Link>
        </Typography>
      </Box>
    </Box>
  );
};

