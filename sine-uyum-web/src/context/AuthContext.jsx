import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { jwtDecode } from 'jwt-decode';
import { setupInterceptors } from '../api/axiosConfig';

const AuthContext = createContext(null);

const getUserFromToken = (token) => {
  if (!token) return null;
  try {
    const decodedToken = jwtDecode(token);
    // Token'ın süresi dolmuşsa, token'ı temizle ve null dön
    if (decodedToken.exp * 1000 < Date.now()) {
      localStorage.removeItem('token');
      return null;
    }
    const userId = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || decodedToken.nameid;
    const username = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] || decodedToken.unique_name;
    const isSubscribed = decodedToken.IsSubscribed === 'True';
    const longRoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    const shortRoleClaim = "role";
    const roles = decodedToken[longRoleClaim] || decodedToken[shortRoleClaim];
    const userRoles = Array.isArray(roles) ? roles : [roles].filter(Boolean);
    return { id: userId, username: username, isSubscribed: isSubscribed, roles: userRoles };
  } catch (error) {
    console.error("Token çözümlenemedi:", error);
    return null;
  }
};

export const AuthProvider = ({ children }) => {
  const navigate = useNavigate();
  const [user, setUser] = useState(() => getUserFromToken(localStorage.getItem('token')));

  const logoutAction = useCallback(() => {
    setUser(null);
    localStorage.removeItem('token');
    navigate('/login');
  }, [navigate]);

  // Interceptor'ı SADECE BİR KERE, uygulama ilk yüklendiğinde kur.
  // Bu, gereksiz yeniden kurulumları önler ve kararlı çalışmasını sağlar.
  useEffect(() => {
    setupInterceptors(logoutAction);
  }, [logoutAction]);

  const loginAction = (newToken) => {
    const userData = getUserFromToken(newToken);
    if (userData) {
      localStorage.setItem('token', newToken);
      setUser(userData);
      
      if (userData.roles.includes('Admin')) {
        navigate('/admin');
      } else {
        navigate('/home');
      }
    } else {
      console.error("Giriş denenen token geçersiz.");
      logoutAction();
    }
  };
  
  const authContextValue = {
    user,
    loginAction,
    logoutAction,
  };

  return (
    <AuthContext.Provider value={authContextValue}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  return useContext(AuthContext);
};