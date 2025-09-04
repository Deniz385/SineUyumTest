import { createContext, useContext, useState } from 'react';
import { useNavigate } from 'react-router-dom';

// 1. Context'i oluşturuyoruz. Başlangıçta boş.
const AuthContext = createContext(null);

// 2. "Provider" component'ini oluşturuyoruz. Bu, tüm uygulamamızı saracak
//    ve context'in değerlerini sağlayacak olan component'tir.
export const AuthProvider = ({ children }) => {
  const navigate = useNavigate();
  // State'imizi localStorage'daki token değeriyle başlatıyoruz.
  // Sayfa yenilendiğinde bile giriş durumunu korur.
  const [token, setToken] = useState(localStorage.getItem('token'));

  // Giriş yapıldığında çağrılacak fonksiyon
  const loginAction = (newToken) => {
    if (newToken) {
      setToken(newToken);
      localStorage.setItem('token', newToken);
      navigate('/home'); // Giriş sonrası anasayfaya yönlendir
    }
  };

  // Çıkış yapıldığında çağrılacak fonksiyon
  const logoutAction = () => {
    setToken(null);
    localStorage.removeItem('token');
    navigate('/login'); // Çıkış sonrası login sayfasına yönlendir
  };

  // Context aracılığıyla diğer component'lere sağlanacak olan değerler
  const value = {
    token,
    loginAction,
    logoutAction,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

// 3. Kendi hook'umuzu oluşturuyoruz. Bu, diğer component'lerin context'e
//    daha kolay erişmesini sağlayacak. (örn: const { token } = useAuth();)
export const useAuth = () => {
  return useContext(AuthContext);
};