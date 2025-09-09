import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { jwtDecode } from 'jwt-decode';
import { setupInterceptors } from '../api/axiosConfig'; // Axios interceptor'ı import et

const AuthContext = createContext(null);

const getUserFromToken = (token) => {
  if (!token) return null;
  try {
    const decodedToken = jwtDecode(token);
    // Token'ın süresinin dolup dolmadığını kontrol et
    if (decodedToken.exp * 1000 < Date.now()) {
      return null; // Süresi dolmuş token
    }
    
    const userId = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || decodedToken.nameid;
    const username = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] || decodedToken.unique_name;

    const isSubscribed = decodedToken.IsSubscribed === 'True';

    return { id: userId, username: username, isSubscribed: isSubscribed };

  } catch (error) {
    console.error("Token çözümlenemedi:", error);
    return null;
  }
};


export const AuthProvider = ({ children }) => {
  const navigate = useNavigate();
  const [token, setToken] = useState(() => localStorage.getItem('token'));
  const [user, setUser] = useState(() => getUserFromToken(localStorage.getItem('token')));

  // logoutAction'ı useCallback ile sarmalayarak gereksiz render'ları önle
  const logoutAction = useCallback(() => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('token');
    navigate('/login');
  }, [navigate]);

  const loginAction = (newToken) => {
    const userData = getUserFromToken(newToken);
    if (userData) {
      setToken(newToken);
      localStorage.setItem('token', newToken);
      setUser(userData);
      navigate('/home');
    } else {
      console.error("Giriş denenen token geçersiz.");
      // Başarısız giriş denemesinde eski token'ı temizle
      logoutAction();
    }
  };
  
  // AuthContext'in değerini bir obje olarak tanımla
  const authContextValue = {
    token,
    user,
    loginAction,
    logoutAction,
  };

  // Interceptor'ı sadece bir kere, ve AuthContext değeriyle kur
  useEffect(() => {
    setupInterceptors(authContextValue);
  }, [authContextValue]); // authContextValue değiştiğinde (örneğin logout) interceptor güncellenir

  // İlk açılışta token geçerliliğini kontrol et
  useEffect(() => {
    const tokenInStorage = localStorage.getItem('token');
    if (tokenInStorage && !getUserFromToken(tokenInStorage)) {
      // Eğer depolamada geçersiz bir token varsa temizle
      logoutAction();
    }
  }, [logoutAction]);

  return (
    <AuthContext.Provider value={authContextValue}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  return useContext(AuthContext);
};