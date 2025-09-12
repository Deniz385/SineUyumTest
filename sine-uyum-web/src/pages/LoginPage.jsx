import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { TextField, Button, Box, Typography, CircularProgress } from '@mui/material';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider'; // <-- SNACKBAR KANCASINI İÇE AKTAR

export const LoginPage = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { loginAction } = useAuth();
  const { showSnackbar } = useSnackbar(); // <-- SNACKBAR FONKSİYONUNU AL

  const handleLogin = async (event) => {
    event.preventDefault();
    setIsLoading(true);

    try {
      const response = await api.post(`/api/account/login`, {
        username: username,
        password: password,
      });

      const token = response.data.token;
      loginAction(token);

    } catch (err) {
      // --- DEĞİŞİKLİK: setError yerine showSnackbar ---
      const errorMessage = err.response?.data?.message || 'Giriş yapılırken bir sorun oluştu. Lütfen tekrar deneyin.';
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
        // Temadan renk almak için bgcolor güncellendi
        backgroundColor: 'background.paper',
        textAlign: 'center'
      }}
    >
      <Typography variant="h4" component="h1" gutterBottom>
        Giriş Yap
      </Typography>
      <Box component="form" onSubmit={handleLogin} sx={{ mt: 1 }}>
        {/* AlertMessage bileşeni tamamen kaldırıldı */}
        <TextField
          margin="normal"
          required
          fullWidth
          id="username"
          label="Kullanıcı Adı"
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
          label="Şifre"
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
          {isLoading ? <CircularProgress size={24} /> : 'Giriş Yap'}
        </Button>
        <Typography variant="body2">
          Hesabınız yok mu?{' '}
          <Link to="/register" style={{ textDecoration: 'none' }}>
            Hemen Kayıt Olun
          </Link>
        </Typography>
      </Box>
    </Box>
  );
};

