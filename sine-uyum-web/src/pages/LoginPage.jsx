// src/pages/LoginPage.jsx
import React, { useState } from 'react';
import axios from 'axios'; // 1. Axios'u projemize dahil ediyoruz.

// 2. API'mizin ana adresini bir değişkene atıyoruz.
// Bu, gelecekte adresi değiştirmemiz gerektiğinde tek bir yerden yapmamızı sağlar.
const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
export const LoginPage = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(''); // Hata mesajlarını göstermek için yeni bir state

  // 3. handleLogin fonksiyonunu async/await yapısıyla güncelliyoruz.
  // Bu, API isteğinin bitmesini beklememizi sağlar.
  const handleLogin = async (event) => {
    event.preventDefault();
    setError(''); // Her denemede eski hata mesajını temizle

    try {
      // 4. Axios ile API'mize POST isteği gönderiyoruz.
      const response = await axios.post(`${API_URL}/api/account/login`, {
        username: username, // input'tan gelen kullanıcı adı
        password: password, // input'tan gelen şifre
      });

      // 5. İstek başarılı olursa...
      console.log('Giriş başarılı!', response.data);
      const token = response.data.token;

      // 6. Gelen token'ı tarayıcının yerel deposuna (localStorage) kaydediyoruz.
      // Bu sayede kullanıcı sayfayı yenilese veya kapatsa bile giriş yapmış olarak kalır.
      localStorage.setItem('token', token);

      alert('Giriş başarılı! Token kaydedildi.');
      // GELECEKTE: Kullanıcıyı ana sayfaya yönlendireceğiz.

    } catch (err) {
      // 7. Eğer API'den bir hata dönerse (örn: 401 Unauthorized)...
      console.error('Giriş hatası:', err.response?.data || err.message);

      if (err.response && err.response.status === 401) {
        setError('Kullanıcı adı veya şifre hatalı.');
      } else {
        setError('Giriş yapılırken bir sorun oluştu. Lütfen tekrar deneyin.');
      }
    }
  };

  return (
    <div>
      <h1>Giriş Yap</h1>
      <form onSubmit={handleLogin}>
        <div>
          <label>Kullanıcı Adı:</label>
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
          />
        </div>
        <div>
          <label>Şifre:</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>
        {/* Hata mesajı varsa ekranda gösteriyoruz */}
        {error && <p style={{ color: 'red' }}>{error}</p>}
        <button type="submit">Giriş Yap</button>
      </form>
    </div>
  );
};