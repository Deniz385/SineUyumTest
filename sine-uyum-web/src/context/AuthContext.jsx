import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { jwtDecode } from 'jwt-decode';
import api, { setupInterceptors, saveTokens, clearTokens, refreshAccessTokenOnce, TOKEN_KEY, REFRESH_TOKEN_KEY } from '../api/axiosConfig';
import i18n from '../i18n';

const AuthContext = createContext(null);

const getUserFromToken = (token) => {
  if (!token) return null;
  try {
    const decodedToken = jwtDecode(token);
    // An expired token yields null. The token is NOT deleted: the interceptor
    // will try to renew it with the refresh token.
    if (decodedToken.exp * 1000 < Date.now()) {
      return null;
    }
    const userId = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || decodedToken.nameid;
    const username = decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] || decodedToken.unique_name;
    const isSubscribed = decodedToken.IsSubscribed === 'True';
    const longRoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    const shortRoleClaim = "role";
    const roles = decodedToken[longRoleClaim] || decodedToken[shortRoleClaim];
    const userRoles = Array.isArray(roles) ? roles : [roles].filter(Boolean);
    return { id: userId, username: username, isSubscribed: isSubscribed, roles: userRoles };
  } catch (error) {
    console.error("Could not decode the token:", error);
    return null;
  }
};

// Tells the server which language to use for notifications and pushes.
// Fire-and-forget: the UI language does not depend on it.
const syncLanguageToServer = (language) => {
  api.put('/api/v1/account/language', { language }).catch(() => {});
};

export const AuthProvider = ({ children }) => {
  const navigate = useNavigate();
  const [user, setUser] = useState(() => getUserFromToken(localStorage.getItem(TOKEN_KEY)));
  // If the access token has expired on startup but a refresh token exists, a
  // refresh is attempted before deciding. An expired token used to count as
  // "not logged in" straight away: since the access token lives 60 minutes
  // and the refresh token 60 days, everyone returning after an hour had to log
  // in again even though their refresh token was still valid.
  const [isRestoring, setIsRestoring] = useState(
    () => !user && Boolean(localStorage.getItem(REFRESH_TOKEN_KEY))
  );

  useEffect(() => {
    if (!isRestoring) return;
    let cancelled = false;
    refreshAccessTokenOnce().then((newToken) => {
      if (cancelled) return;
      const restored = getUserFromToken(newToken);
      if (restored) setUser(restored);
      else clearTokens();
      setIsRestoring(false);
    });
    return () => { cancelled = true; };
  }, [isRestoring]);

  const logoutAction = useCallback(() => {
    setUser(null);
    clearTokens();
    navigate('/login');
  }, [navigate]);

  // When the interceptor silently renews the token, the user state is
  // refreshed too (the subscription claim may have changed, for example).
  const handleTokenRefreshed = useCallback((newToken) => {
    const userData = getUserFromToken(newToken);
    if (userData) setUser(userData);
  }, []);

  // Hand the callbacks to the interceptors; they are only replaced when the
  // callbacks themselves change.
  useEffect(() => {
    setupInterceptors(logoutAction, handleTokenRefreshed);
  }, [logoutAction, handleTokenRefreshed]);

  // Once per login (or app start while logged in), and on every language
  // change after that, the server learns the user's language.
  const userId = user?.id;
  useEffect(() => {
    if (!userId) return undefined;
    syncLanguageToServer(i18n.language);
    i18n.on('languageChanged', syncLanguageToServer);
    return () => i18n.off('languageChanged', syncLanguageToServer);
  }, [userId]);

  // The API returns { token, refreshToken, expiresAt, user }.
  // A bare token string is still accepted for backward compatibility.
  const loginAction = (authResponse) => {
    const token = typeof authResponse === 'string' ? authResponse : authResponse?.token;
    const refreshToken = typeof authResponse === 'string' ? null : authResponse?.refreshToken;

    const userData = getUserFromToken(token);
    if (userData) {
      saveTokens({ token, refreshToken });
      setUser(userData);

      if (userData.roles.includes('Admin')) {
        navigate('/admin');
      } else {
        navigate('/home');
      }
    } else {
      console.error("The login token is invalid.");
      logoutAction();
    }
  };

  // Renews the token and refreshes the user info (e.g. the subscription
  // claim). When the subscription changes on the server (granted by an admin,
  // payment verified) there is no need to wait an hour for the old claim to
  // expire.
  const refreshSession = useCallback(async () => {
    const newToken = await refreshAccessTokenOnce();
    const refreshed = getUserFromToken(newToken);
    if (refreshed) setUser(refreshed);
    return refreshed;
  }, []);

  const authContextValue = {
    user,
    isRestoring,
    refreshSession,
    loginAction,
    logoutAction,
  };

  return (
    <AuthContext.Provider value={authContextValue}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  return useContext(AuthContext);
};
