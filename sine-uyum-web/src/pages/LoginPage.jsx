import React, { useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { TextField, Button, Box, Typography, CircularProgress, Link as MuiLink } from '@mui/material';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { AuthShell } from '../components/AuthShell';

export const LoginPage = () => {
  const { t } = useTranslation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  // Set once this form signs in: loginAction then decides where to go.
  const [submitted, setSubmitted] = useState(false);
  const { user, loginAction } = useAuth();
  const { showSnackbar } = useSnackbar();

  // Already signed in (e.g. opening the site again): straight to Home.
  if (user && !submitted) return <Navigate to="/home" replace />;

  const handleLogin = async (event) => {
    event.preventDefault();
    setIsLoading(true);
    setSubmitted(true);

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
      setSubmitted(false);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthShell
      title={t('auth.login.title')}
      footer={(
        <Typography variant="body1" color="text.secondary">
          {t('auth.login.noAccount')}{' '}
          <MuiLink component={Link} to="/register" sx={{ color: 'primary.main', fontWeight: 600 }}>
            {t('auth.login.registerLink')}
          </MuiLink>
        </Typography>
      )}
    >
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
          size="large"
          sx={{ mt: 3 }}
          disabled={isLoading}
        >
          {isLoading ? <CircularProgress size={22} color="inherit" aria-label={t('common.loading')} /> : t('auth.login.submit')}
        </Button>
      </Box>
    </AuthShell>
  );
};
