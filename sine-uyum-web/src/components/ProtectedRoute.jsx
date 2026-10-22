import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from '../context/AuthContext';

export const ProtectedRoute = ({ children }) => {
  const { user, isRestoring } = useAuth();

  // The session is being restored with the refresh token: wait before deciding.
  if (isRestoring) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  // No user (not logged in): redirect to the login page.
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  // Logged in: render the requested page (Layout and its children).
  return children;
};