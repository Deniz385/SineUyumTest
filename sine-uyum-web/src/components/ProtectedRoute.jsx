import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { LoadingState } from './states/LoadingState';

export const ProtectedRoute = ({ children }) => {
  const { user, isRestoring } = useAuth();

  // The session is being restored with the refresh token: wait before deciding.
  if (isRestoring) {
    return <LoadingState sx={{ mt: 2 }} />;
  }

  // No user (not logged in): redirect to the login page.
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  // Logged in: render the requested page (Layout and its children).
  return children;
};