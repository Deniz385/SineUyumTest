import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { LoadingState } from './states/LoadingState';

/**
 * Only for signed-in users. With `onboarding`, people who have not finished
 * the first-run welcome are sent there first; while that is still unknown
 * (first start on a device) it waits for /account/me.
 */
export const ProtectedRoute = ({ children, onboarding = false }) => {
  const { user, isRestoring } = useAuth();

  // The session is being restored with the refresh token: wait before deciding.
  if (isRestoring) {
    return <LoadingState sx={{ minHeight: '100vh' }} />;
  }

  // No user (not logged in): redirect to the login page.
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (onboarding) {
    if (user.onboardingCompleted === undefined) return <LoadingState sx={{ minHeight: '100vh' }} />;
    if (user.onboardingCompleted === false) return <Navigate to="/welcome" replace />;
  }

  // Logged in: render the requested page (Layout and its children).
  return children;
};
