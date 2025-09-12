import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export const AdminRoute = () => {
  const { user } = useAuth();

  // Eğer kullanıcı varsa VE 'Admin' rolüne sahipse, içindeki sayfaları (<AdminPage />) göster.
  if (user && user.roles?.includes('Admin')) {
    return <Outlet />;
  }

  // Admin değilse, ana sayfaya yönlendir.
  return <Navigate to="/home" replace />;
};