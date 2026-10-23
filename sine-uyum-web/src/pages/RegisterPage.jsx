import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { TextField, Button, Box, Typography, CircularProgress } from '@mui/material';
import { useSnackbar } from '../context/SnackbarProvider';
import { useAuth } from '../context/AuthContext';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
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
        {t('auth.register.title')}
      </Typography>
      <Box component="form" onSubmit={handleRegister} sx={{ mt: 1 }}>
        <TextField
          margin="normal" required fullWidth autoFocus
          label={t('auth.username')} name="username"
          value={formData.username} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label={t('auth.email')} name="email" type="email"
          value={formData.email} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label={t('auth.password')} name="password" type="password"
          value={formData.password} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label={t('auth.confirmPassword')} name="confirmPassword" type="password"
          value={formData.confirmPassword} onChange={handleChange}
        />
        <Button
          type="submit" fullWidth variant="contained"
          sx={{ mt: 3, mb: 2, py: 1.5 }} disabled={isLoading}
        >
          {isLoading ? <CircularProgress size={24} /> : t('auth.register.submit')}
        </Button>
        <Typography variant="body2">
          {t('auth.register.haveAccount')}{' '}
          <Link to="/login" style={{ textDecoration: 'none' }}>
            {t('auth.register.loginLink')}
          </Link>
        </Typography>
      </Box>
    </Box>
  );
};

