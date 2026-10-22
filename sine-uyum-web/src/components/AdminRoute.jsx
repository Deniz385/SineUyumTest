import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export const AdminRoute = () => {
  const { user } = useAuth();

  // Logged in AND has the 'Admin' role: render the nested pages (<AdminPage />).
  if (user && user.roles?.includes('Admin')) {
    return <Outlet />;
  }

  // Not an admin: redirect to the home page.
  return <Navigate to="/home" replace />;
};