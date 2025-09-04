import React, { useState } from 'react';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const HomePage = () => {
  const [users, setUsers] = useState([]);
  const [message, setMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { token } = useAuth();

  const handleFetchUsers = async () => {
    setMessage('');
    setUsers([]);
    setIsLoading(true);

    try {
      if (!token) {
        setMessage('Token bulunamadı, lütfen tekrar giriş yapın.');
        return;
      }
      
      const response = await axios.get(`${API_URL}/api/account/users`, {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      });
      setUsers(response.data);
      setMessage('Kullanıcı listesi başarıyla çekildi!');

    } catch (error) {
       if (error.response && error.response.status === 401) {
        setMessage('Oturum süreniz dolmuş veya token geçersiz.');
      } else {
        setMessage('Veri alınırken bir hata oluştu.');
      }
    } finally {
        setIsLoading(false);
    }
  };

  return (
    // Ana div'e yeni bir class ekledik
    <div className="page-container">
      <h1>Hoş Geldiniz!</h1>
      <p>Bu sayfa sadece giriş yapmış kullanıcılar tarafından görülebilir.</p>
      
      <button onClick={handleFetchUsers} disabled={isLoading} className="button">
        {isLoading ? 'Yükleniyor...' : 'Kullanıcı Listesini Getir'}
      </button>

      {/* Mesajlar için mevcut class'larımızı kullandık */}
      {message && <div className="message success-message">{message}</div>}
      
      {users.length > 0 && (
        <div className="user-list">
          <h2>Kullanıcılar</h2>
          <ul>
            {users.map(user => (
              <li key={user.id}>{user.userName}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};