import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { TextField, Button, Box, Typography, CircularProgress, Link as MuiLink } from '@mui/material';
import { useSnackbar } from '../context/SnackbarProvider';
import { useAuth } from '../context/AuthContext';
import { AuthShell } from '../components/AuthShell';
import api from '../api/axiosConfig';

export const RegisterPage = () => {
  const { t } = useTranslation();
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();
  const { showSnackbar } = useSnackbar();
  const { loginAction } = useAuth();

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleRegister = async (event) => {
    event.preventDefault();

    if (formData.password !== formData.confirmPassword) {
      showSnackbar(t('auth.register.passwordMismatch'), 'error');
      return;
    }

    setIsLoading(true);

    try {
      await api.post(`/api/v1/account/register`, {
        username: formData.username,
        email: formData.email,
        password: formData.password,
      });

      // Signed straight in: a new account continues to the first-run welcome.
      try {
        const login = await api.post(`/api/v1/account/login`, {
          username: formData.username,
          password: formData.password,
        });
        loginAction(login.data);
      } catch {
        showSnackbar(t('auth.register.success'), 'success');
        setTimeout(() => {
          navigate('/login');
        }, 2000);
      }

    } catch (err) {
      const errorMessages = err.response?.data?.errors
        ? err.response.data.errors.map(e => e.description).join(' ')
        : err.response?.data?.message || t('auth.register.failed');
      showSnackbar(errorMessages, 'error');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthShell
      title={t('auth.register.title')}
      footer={(
        <Typography variant="body1" color="text.secondary">
          {t('auth.register.haveAccount')}{' '}
          <MuiLink component={Link} to="/login" sx={{ color: 'primary.main', fontWeight: 600 }}>
            {t('auth.register.loginLink')}
          </MuiLink>
        </Typography>
      )}
    >
      <Box component="form" onSubmit={handleRegister} sx={{ mt: 1 }}>
        <TextField
          margin="normal" required fullWidth autoFocus
          label={t('auth.username')} name="username" autoComplete="username"
          value={formData.username} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label={t('auth.email')} name="email" type="email" autoComplete="email"
          value={formData.email} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label={t('auth.password')} name="password" type="password" autoComplete="new-password"
          value={formData.password} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label={t('auth.confirmPassword')} name="confirmPassword" type="password" autoComplete="new-password"
          value={formData.confirmPassword} onChange={handleChange}
        />
        <Button
          type="submit" fullWidth variant="contained" size="large"
          sx={{ mt: 3 }} disabled={isLoading}
        >
          {isLoading ? <CircularProgress size={22} color="inherit" aria-label={t('common.loading')} /> : t('auth.register.submit')}
        </Button>
      </Box>
    </AuthShell>
  );
};
