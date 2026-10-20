import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from '../context/AuthContext';

export const ProtectedRoute = ({ children }) => {
  const { user, isRestoring } = useAuth();

  // Oturum refresh token ile geri yükleniyor: karar vermeden bekle.
  if (isRestoring) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  // Eğer user objesi yoksa (giriş yapılmamışsa), login sayfasına yönlendir.
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  // Giriş yapılmışsa, istenen sayfayı (Layout ve içindeki çocukları) göster.
  return children;
};