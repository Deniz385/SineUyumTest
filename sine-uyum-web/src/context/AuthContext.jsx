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

// Whether the first-run welcome is done, remembered per user and device so
// returning users never wait for /account/me (or see the welcome again).
const onboardingKey = (userId) => `onboardingDone:${userId}`;
const readOnboardingDone = (userId) => {
  try {
    return localStorage.getItem(onboardingKey(userId)) === '1';
  } catch {
    return false;
  }
};
const rememberOnboardingDone = (userId) => {
  try {
    localStorage.setItem(onboardingKey(userId), '1');
  } catch {
    // Ignore: /account/me answers again next time.
  }
};

// The token carries the identity; the user summary (login/refresh response,
// /account/me) adds the rest. onboardingCompleted: true / false, or
// undefined while unknown (null if /account/me could not be loaded).
const withSummary = (user, summary) => {
  if (!user) return user;
  const next = { ...user };
  if (summary) {
    if (typeof summary.onboardingCompleted === 'boolean') next.onboardingCompleted = summary.onboardingCompleted;
    if (summary.profileImageUrl !== undefined) next.profileImageUrl = summary.profileImageUrl;
  }
  if (next.onboardingCompleted === undefined && readOnboardingDone(user.id)) next.onboardingCompleted = true;
  if (next.onboardingCompleted === true) rememberOnboardingDone(user.id);
  return next;
};

// Tells the server which language to use for notifications and pushes.
// Fire-and-forget: the UI language does not depend on it.
const syncLanguageToServer = (language) => {
  api.put('/api/v1/account/language', { language }).catch(() => {});
};

export const AuthProvider = ({ children }) => {
  const navigate = useNavigate();
  const [user, setUser] = useState(() => withSummary(getUserFromToken(localStorage.getItem(TOKEN_KEY))));
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
      if (restored) setUser(withSummary(restored));
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
    // Keep what the summary added (onboarding state, photo).
    if (userData) setUser((prev) => withSummary({ ...prev, ...userData }));
  }, []);

  // Hand the callbacks to the interceptors; they are only replaced when the
  // callbacks themselves change.
  useEffect(() => {
    setupInterceptors(logoutAction, handleTokenRefreshed);
  }, [logoutAction, handleTokenRefreshed]);

  // The user summary (onboarding state, photo) when the token alone does
  // not tell, e.g. on app start; a failure never blocks the app.
  const userId = user?.id;
  const needsSummary = Boolean(userId) && (user?.onboardingCompleted === undefined || user?.profileImageUrl === undefined);
  useEffect(() => {
    if (!needsSummary) return undefined;
    let cancelled = false;
    api.get('/api/v1/account/me')
      .then((res) => {
        if (cancelled) return;
        setUser((prev) => {
          if (!prev) return prev;
          const next = withSummary(prev, res.data);
          // Never ask twice: missing fields count as unknown (null).
          return { ...next, onboardingCompleted: next.onboardingCompleted ?? null, profileImageUrl: next.profileImageUrl ?? null };
        });
      })
      .catch(() => {
        if (!cancelled) setUser((prev) => (prev && prev.onboardingCompleted === undefined ? { ...prev, onboardingCompleted: null, profileImageUrl: prev.profileImageUrl ?? null } : prev));
      });
    return () => { cancelled = true; };
  }, [needsSummary]);

  // Once per login (or app start while logged in), and on every language
  // change after that, the server learns the user's language.
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

    const userData = withSummary(getUserFromToken(token), typeof authResponse === 'string' ? null : authResponse?.user);
    if (userData) {
      saveTokens({ token, refreshToken });
      setUser(userData);

      // New accounts start with the first-run welcome (shown once).
      if (userData.onboardingCompleted === false) {
        navigate('/welcome');
      } else if (userData.roles.includes('Admin')) {
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
    if (refreshed) setUser((prev) => withSummary({ ...prev, ...refreshed }));
    return refreshed;
  }, []);

  // Finishing or skipping the welcome (POST /account/onboarding/complete).
  // The welcome is not shown again on this device even if the call fails;
  // the server asks again on the next sign-in in that case.
  const completeOnboarding = useCallback(async () => {
    setUser((prev) => (prev ? withSummary({ ...prev, onboardingCompleted: true }) : prev));
    try {
      await api.post('/api/v1/account/onboarding/complete');
      return true;
    } catch {
      return false;
    }
  }, []);

  // A changed profile photo shows in the top bar at once.
  const updateUserSummary = useCallback((summary) => {
    setUser((prev) => (prev ? withSummary(prev, summary) : prev));
  }, []);

  const authContextValue = {
    user,
    isRestoring,
    refreshSession,
    completeOnboarding,
    updateUserSummary,
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
