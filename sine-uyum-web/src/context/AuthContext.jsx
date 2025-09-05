import { createContext, useContext, useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { jwtDecode } from 'jwt-decode';

const AuthContext = createContext(null);

// Bu yardımcı fonksiyon, token'dan kullanıcı bilgilerini güvenli bir şekilde çıkarır.
const getUserFromToken = (token) => {
  if (!token) return null;
  try {
    const decodedToken = jwtDecode(token);
    // Token'ın süresinin dolup dolmadığını kontrol et
    if (decodedToken.exp * 1000 < Date.now()) {
      return null; // Süresi dolmuş token
    }
    
    // --- DEĞİŞİKLİK BURADA ---
    // ID için hem uzun ismi (nameidentifier) hem de kısa ismi (nameid) kontrol et.
    // Hangisi varsa onu kullan.
    const userId = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || decodedToken.nameid;
    const username = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] || decodedToken.unique_name;

    return { id: userId, username: username };

  } catch (error) {
    console.error("Token çözümlenemedi:", error);
    return null;
  }
};


export const AuthProvider = ({ children }) => {
  const navigate = useNavigate();
  const [token, setToken] = useState(localStorage.getItem('token'));
  const [user, setUser] = useState(null);

  useEffect(() => {
    const userData = getUserFromToken(token);
    if (userData) {
      setUser(userData);
    } else {
      // Geçersiz token varsa temizle
      logoutAction();
    }
  }, []); // Bu useEffect'in sadece ilk açılışta bir kez çalışması yeterli

  const loginAction = (newToken) => {
    const userData = getUserFromToken(newToken);
    if (userData) {
      setToken(newToken);
      localStorage.setItem('token', newToken);
      setUser(userData);
      navigate('/home');
    } else {
      console.error("Giriş denenen token geçersiz.");
    }
  };

  const logoutAction = () => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('token');
    navigate('/login');
  };

  const value = {
    token,
    user,
    loginAction,
    logoutAction,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  return useContext(AuthContext);
};