import axios from 'axios';
import i18n, { FALLBACK_LANGUAGE } from '../i18n';

// API address from the .env file; falls back to the local API.
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5074';

const api = axios.create({
    baseURL: API_URL
});

// The API localizes its messages (errors, notifications, TMDb data...) by
// this header, so every request carries the active UI language.
const currentLanguage = () => i18n.language || FALLBACK_LANGUAGE;

// Uploaded media (e.g. profile photos) comes back as a relative path
// ("/media/avatars/..."), so the browser needs it prefixed with the API address.
// Older records may hold a full URL, which is used as is.
export const mediaUrl = (url) => (url && url.startsWith('/') ? `${API_URL}${url}` : url || undefined);

// Helpers so tokens are read and written in one place.
// The access token is short-lived (60 min by default); when it expires it is
// silently renewed with the refresh token.
export const TOKEN_KEY = 'token';
export const REFRESH_TOKEN_KEY = 'refreshToken';

export const saveTokens = ({ token, refreshToken }) => {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    if (refreshToken) localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
};

export const clearTokens = () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
};

// These endpoints take no Authorization header, and a 401 from them must not
// trigger another refresh attempt.
const AUTH_PATHS = ['/api/v1/account/login', '/api/v1/account/register', '/api/v1/account/refresh'];
const isAuthPath = (url = '') => AUTH_PATHS.some((path) => url.startsWith(path));

// If several requests get a 401 at once, only one refresh request is sent.
// (The server rotates refresh tokens, so a second one would be invalid.)
let refreshPromise = null;

// Resolves to a new access token (and saves the tokens), or null if the
// session cannot be renewed. Concurrent calls share a single request.
export const refreshAccessTokenOnce = () => {
    refreshPromise = refreshPromise || refreshAccessToken().finally(() => {
        refreshPromise = null;
    });
    return refreshPromise;
};

const refreshAccessToken = async () => {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) return null;

    try {
        // A separate axios call so the interceptors cannot loop.
        const response = await axios.post(
            `${API_URL}/api/v1/account/refresh`,
            { refreshToken },
            { headers: { 'Accept-Language': currentLanguage() } }
        );
        const { token, refreshToken: newRefreshToken } = response.data || {};
        if (!token || !newRefreshToken) return null;

        saveTokens({ token, refreshToken: newRefreshToken });
        return token;
    } catch {
        return null;
    }
};

// The interceptors are installed ONCE, when this module loads. They used to be
// installed in an AuthContext useEffect, which caused two problems:
//   * React runs child effects BEFORE parent effects, so the first requests on
//     page load went out without an Authorization header, got a 401 and then
//     triggered a needless token refresh + retry (and without a refresh token
//     the user was logged out on every reload).
//   * every re-run of the effect stacked another set of interceptors.
// AuthContext now only provides the callbacks (setupInterceptors).
let onLogout = null;
let onTokenRefreshed = null;

// Runs before each request. The token is read straight from localStorage every
// time, so it is never affected by React state lagging behind.
api.interceptors.request.use(
    (config) => {
        config.headers['Accept-Language'] = currentLanguage();
        if (!isAuthPath(config.url)) {
            const token = localStorage.getItem(TOKEN_KEY);
            if (token) {
                config.headers['Authorization'] = `Bearer ${token}`;
            }
        }
        return config;
    },
    (error) => Promise.reject(error)
);

// Runs after the API responds.
api.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;
        const isUnauthorized = error.response?.status === 401;

        // On a 401, first try to renew the session with the refresh token; if
        // that works, the request is retried without the user noticing.
        if (isUnauthorized && originalRequest && !originalRequest._retried && !isAuthPath(originalRequest.url)) {
            originalRequest._retried = true;

            const newToken = await refreshAccessTokenOnce();

            if (newToken) {
                if (onTokenRefreshed) onTokenRefreshed(newToken);
                originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
                return api(originalRequest);
            }

            console.error("Could not renew the session. Logging out.");
            if (onLogout) onLogout();
        }

        return Promise.reject(error);
    }
);

// AuthContext passes its logout and token-refreshed callbacks in here.
// Calling it again is safe; only the callbacks are replaced.
export const setupInterceptors = (logoutAction, tokenRefreshedAction) => {
    onLogout = logoutAction;
    onTokenRefreshed = tokenRefreshedAction;
};

export default api;
