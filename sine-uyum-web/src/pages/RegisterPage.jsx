import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { TextField, Button, Box, Typography, CircularProgress } from '@mui/material';
import { useSnackbar } from '../context/SnackbarProvider'; // <-- SNACKBAR KANCASINI İÇE AKTAR
import api from '../api/axiosConfig';

export const RegisterPage = () => {
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();
  const { showSnackbar } = useSnackbar(); // <-- SNACKBAR FONKSİYONUNU AL

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleRegister = async (event) => {
    event.preventDefault();

    if (formData.password !== formData.confirmPassword) {
      showSnackbar('Şifreler uyuşmuyor.', 'error');
      return;
    }

    setIsLoading(true);

    try {
      await api.post(`/api/account/register`, {
        username: formData.username,
        email: formData.email,
        password: formData.password,
      });

      showSnackbar('Kayıt başarılı! Giriş sayfasına yönlendiriliyorsunuz...', 'success');
      
      setTimeout(() => {
        navigate('/login');
      }, 2000);

    } catch (err) {
      const errorMessages = err.response?.data?.errors
        ? err.response.data.errors.map(e => e.description).join(' ')
        : err.response?.data?.message || 'Kayıt sırasında bir sunucu hatası oluştu.';
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
        // Temadan renk almak için bgcolor güncellendi
        backgroundColor: 'background.paper',
        textAlign: 'center'
      }}
    >
      <Typography variant="h4" component="h1" gutterBottom>
        Kayıt Ol
      </Typography>
      <Box component="form" onSubmit={handleRegister} sx={{ mt: 1 }}>
        {/* AlertMessage bileşenleri tamamen kaldırıldı */}
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

