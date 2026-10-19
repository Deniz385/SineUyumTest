import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { jwtDecode } from 'jwt-decode';
import { setupInterceptors, saveTokens, clearTokens, TOKEN_KEY } from '../api/axiosConfig';

const AuthContext = createContext(null);

const getUserFromToken = (token) => {
  if (!token) return null;
  try {
    const decodedToken = jwtDecode(token);
    // Token'ın süresi dolmuşsa null dön. Token'ı SİLMİYORUZ: interceptor
    // refresh token ile yenilemeyi deneyecek.
    if (decodedToken.exp * 1000 < Date.now()) {
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
  const [user, setUser] = useState(() => getUserFromToken(localStorage.getItem(TOKEN_KEY)));

  const logoutAction = useCallback(() => {
    setUser(null);
    clearTokens();
    navigate('/login');
  }, [navigate]);

  // Interceptor token'ı sessizce yenilediğinde kullanıcı state'i de tazelenir
  // (örneğin abonelik claim'i değişmiş olabilir).
  const handleTokenRefreshed = useCallback((newToken) => {
    const userData = getUserFromToken(newToken);
    if (userData) setUser(userData);
  }, []);

  // Interceptor'ı SADECE BİR KERE, uygulama ilk yüklendiğinde kur.
  // Bu, gereksiz yeniden kurulumları önler ve kararlı çalışmasını sağlar.
  useEffect(() => {
    setupInterceptors(logoutAction, handleTokenRefreshed);
  }, [logoutAction, handleTokenRefreshed]);

  // API artık { token, refreshToken, expiresAt, user } döndürüyor.
  // Geriye dönük uyumluluk için düz token string'i de kabul ediliyor.
  const loginAction = (authResponse) => {
    const token = typeof authResponse === 'string' ? authResponse : authResponse?.token;
    const refreshToken = typeof authResponse === 'string' ? null : authResponse?.refreshToken;

    const userData = getUserFromToken(token);
    if (userData) {
      saveTokens({ token, refreshToken });
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
