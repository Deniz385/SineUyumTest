import React from 'react';
import { Navigate } from 'react-router-dom';
// 1. useAuth hook'unu import ediyoruz
import { useAuth } from '../context/AuthContext';

export const ProtectedRoute = ({ children }) => {
  // 2. Token bilgisini doğrudan context'ten alıyoruz
  const { token } = useAuth();

  // 3. Artık localStorage'a bakmıyoruz, sadece context'teki token'ı kontrol ediyoruz
  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return children;
};