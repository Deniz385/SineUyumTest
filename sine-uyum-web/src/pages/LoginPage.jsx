import React, { useState } from 'react';
import axios from 'axios';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { TextField, Button, Box, Typography, CircularProgress } from '@mui/material';
import { AlertMessage } from '../components/AlertMessage';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const LoginPage = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { loginAction } = useAuth();

  const handleLogin = async (event) => {
    event.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const response = await axios.post(`${API_URL}/api/account/login`, {
        username: username,
        password: password,
      });

      const token = response.data.token;
      loginAction(token);

    } catch (err) {
      console.error('Giriş hatası:', err.response?.data || err.message);
      if (err.response && err.response.status === 401) {
        setError('Kullanıcı adı veya şifre hatalı.');
      } else {
        setError('Giriş yapılırken bir sorun oluştu. Lütfen tekrar deneyin.');
      }
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
        backgroundColor: 'white',
        textAlign: 'center'
      }}
    >
      <Typography variant="h4" component="h1" gutterBottom>
        Giriş Yap
      </Typography>
      <Box component="form" onSubmit={handleLogin} sx={{ mt: 1 }}>
        <AlertMessage type="error" message={error} />

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
