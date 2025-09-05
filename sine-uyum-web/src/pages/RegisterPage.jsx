import React, { useState } from 'react';
import axios from 'axios';
import { Link, useNavigate } from 'react-router-dom';
import { TextField, Button, Box, Typography, CircularProgress } from '@mui/material';
import { AlertMessage } from '../components/AlertMessage';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const RegisterPage = () => {
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const navigate = useNavigate();

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleRegister = async (event) => {
    event.preventDefault();
    setError('');
    setSuccess('');

    if (formData.password !== formData.confirmPassword) {
      setError('Şifreler uyuşmuyor.');
      return;
    }

    setIsLoading(true);

    try {
      await axios.post(`${API_URL}/api/account/register`, {
        username: formData.username,
        email: formData.email,
        password: formData.password,
      });

      setSuccess('Kayıt başarılı! Giriş sayfasına yönlendiriliyorsunuz...');
      
      setTimeout(() => {
        navigate('/login');
      }, 2000);

    } catch (err) {
      console.error('Kayıt hatası:', err.response?.data);
      if (err.response?.data?.errors) {
        const errorMessages = err.response.data.errors.map(e => e.description).join(' ');
        setError(errorMessages || 'Bilinmeyen bir doğrulama hatası oluştu.');
      } else if (err.response?.data?.message) {
         setError(err.response.data.message);
      } else {
        setError('Kayıt sırasında bir sunucu hatası oluştu. Lütfen tekrar deneyin.');
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
        Kayıt Ol
      </Typography>
      <Box component="form" onSubmit={handleRegister} sx={{ mt: 1 }}>
        <AlertMessage type="error" message={error} />
        <AlertMessage type="success" message={success} />
        
        <TextField
          margin="normal" required fullWidth autoFocus
          label="Kullanıcı Adı" name="username"
          value={formData.username} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label="E-posta" name="email" type="email"
          value={formData.email} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label="Şifre" name="password" type="password"
          value={formData.password} onChange={handleChange}
        />
        <TextField
          margin="normal" required fullWidth
          label="Şifre Tekrar" name="confirmPassword" type="password"
          value={formData.confirmPassword} onChange={handleChange}
        />
        <Button
          type="submit" fullWidth variant="contained"
          sx={{ mt: 3, mb: 2, py: 1.5 }} disabled={isLoading}
        >
          {isLoading ? <CircularProgress size={24} /> : 'Kayıt Ol'}
        </Button>
        <Typography variant="body2">
          Zaten bir hesabınız var mı?{' '}
          <Link to="/login" style={{ textDecoration: 'none' }}>
            Giriş Yapın
          </Link>
        </Typography>
      </Box>
    </Box>
  );
};
