import React, { useState } from 'react';
import axios from 'axios';
import { Link, useNavigate } from 'react-router-dom';

// API URL'imizi tanımlıyoruz
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

  // Input değişikliklerini tek bir yerden yönetmek için
  const handleChange = (e) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  const handleRegister = async (event) => {
    event.preventDefault();
    setError('');
    setSuccess('');

    // Client-side şifre kontrolü
    if (formData.password !== formData.confirmPassword) {
      setError('Şifreler uyuşmuyor.');
      return;
    }

    setIsLoading(true); // Yükleniyor durumunu başlat

    try {
      await axios.post(`${API_URL}/api/account/register`, {
        username: formData.username,
        email: formData.email,
        password: formData.password,
      });

      setSuccess('Kayıt başarılı! Giriş sayfasına yönlendiriliyorsunuz...');
      
      // 2 saniye sonra kullanıcıyı login sayfasına yönlendir
      setTimeout(() => {
        navigate('/login');
      }, 2000);

    } catch (err) {
      console.error('Kayıt hatası:', err.response?.data);
      if (err.response?.data?.errors) {
        // ASP.NET Core Identity'den gelen birden fazla hatayı birleştir
        const errorMessages = err.response.data.errors.map(e => e.description).join(' ');
        setError(errorMessages || 'Bilinmeyen bir doğrulama hatası oluştu.');
      } else if (err.response?.data?.message) {
        // Tek bir hata mesajı varsa
         setError(err.response.data.message);
      } else {
        setError('Kayıt sırasında bir sunucu hatası oluştu. Lütfen tekrar deneyin.');
      }
    } finally {
      setIsLoading(false); // İşlem bitince yükleniyor durumunu bitir
    }
  };

  return (
    <div className="form-container">
      <h1>Kayıt Ol</h1>
      <form onSubmit={handleRegister}>
        {error && <div className="message error-message">{error}</div>}
        {success && <div className="message success-message">{success}</div>}

        <input
          name="username"
          type="text"
          placeholder="Kullanıcı Adı"
          value={formData.username}
          onChange={handleChange}
          required
        />
        <input
          name="email"
          type="email"
          placeholder="E-posta"
          value={formData.email}
          onChange={handleChange}
          required
        />
        <input
          name="password"
          type="password"
          placeholder="Şifre"
          value={formData.password}
          onChange={handleChange}
          required
        />
        <input
          name="confirmPassword"
          type="password"
          placeholder="Şifre Tekrar"
          value={formData.confirmPassword}
          onChange={handleChange}
          required
        />
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Kaydediliyor...' : 'Kayıt Ol'}
        </button>
      </form>
      <p>
        Zaten bir hesabınız var mı? <Link to="/login">Giriş Yapın</Link>
      </p>
    </div>
  );
};
