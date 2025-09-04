import React, { useState } from 'react';
import axios from 'axios';
// 1. Link component'ini buraya, useNavigate'in yanına ekliyoruz
import { Link, useNavigate } from 'react-router-dom';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const LoginPage = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false); // 2. Yükleniyor durumu ekledik
  const navigate = useNavigate();

  const handleLogin = async (event) => {
    event.preventDefault();
    setError('');
    setIsLoading(true); // İstek başlarken butonu pasif hale getir

    try {
      const response = await axios.post(`${API_URL}/api/account/login`, {
        username: username,
        password: password,
      });

      const token = response.data.token;
      localStorage.setItem('token', token);
      navigate('/home');

    } catch (err) {
      console.error('Giriş hatası:', err.response?.data || err.message);
      if (err.response && err.response.status === 401) {
        setError('Kullanıcı adı veya şifre hatalı.');
      } else {
        setError('Giriş yapılırken bir sorun oluştu. Lütfen tekrar deneyin.');
      }
    } finally {
      setIsLoading(false); // İstek bitince butonu tekrar aktif hale getir
    }
  };

  return (
    // 3. Daha iyi bir görünüm için RegisterPage'de kullandığımız CSS sınıflarını ekledik
    <div className="form-container">
      <h1>Giriş Yap</h1>
      <form onSubmit={handleLogin}>
        {error && <div className="message error-message">{error}</div>}

        <input
          type="text"
          placeholder="Kullanıcı Adı" // Label yerine placeholder kullanmak daha modern
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          required
        />
        <input
          type="password"
          placeholder="Şifre"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Giriş Yapılıyor...' : 'Giriş Yap'}
        </button>
      </form>
      <p>
        Hesabınız yok mu? <Link to="/register">Hemen Kayıt Olun</Link>
      </p>
    </div>
  );
};


