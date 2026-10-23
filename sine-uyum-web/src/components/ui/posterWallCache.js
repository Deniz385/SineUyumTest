// Poster paths remembered for the sign-in screen's poster wall (the movie
// endpoints need a session, so the login page shows the last ones seen).
const KEY = 'posterWall';

export const readPosterWall = () => {
    try {
        const parsed = JSON.parse(localStorage.getItem(KEY) || '[]');
        return Array.isArray(parsed) ? parsed.filter((p) => typeof p === 'string' && p.startsWith('/')) : [];
    } catch {
        return [];
    }
};

export const savePosterWall = (paths) => {
    const clean = [...new Set(paths.filter(Boolean))].slice(0, 40);
    if (clean.length < 8) return;
    try {
        localStorage.setItem(KEY, JSON.stringify(clean));
    } catch {
        // Ignore: the wall falls back to abstract tiles.
    }
};
