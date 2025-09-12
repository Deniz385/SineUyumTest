import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export const ProtectedRoute = ({ children }) => {
  const { user } = useAuth();

  // Eğer user objesi yoksa (giriş yapılmamışsa), login sayfasına yönlendir.
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  // Giriş yapılmışsa, istenen sayfayı (Layout ve içindeki çocukları) göster.
  return children;
};