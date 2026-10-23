// Poster helpers shared by the poster components (TMDb URLs, sizes, styles).

const TMDB_IMAGE_BASE = 'https://image.tmdb.org/t/p/';
const POSTER_WIDTHS = [185, 342, 500];

// A TMDb image URL for a path ("/abc.jpg"); full URLs are used as they are.
export const tmdbImage = (path, size = 'w342') => {
    if (!path) return null;
    return /^https?:\/\//.test(path) ? path : `${TMDB_IMAGE_BASE}${size}${path}`;
};

export const posterSrcSet = (path) =>
    /^https?:\/\//.test(path) ? undefined : POSTER_WIDTHS.map((w) => `${TMDB_IMAGE_BASE}w${w}${path} ${w}w`).join(', ');

// The design system's poster hairline (1px inside the edge).
export const posterHairline = {
    content: '""',
    position: 'absolute',
    inset: 0,
    borderRadius: 'inherit',
    boxShadow: (theme) => `inset 0 0 0 1px ${theme.palette.tokens.hairline}`,
    pointerEvents: 'none',
};

export const POSTER_CARD_WIDTHS = { S: 96, M: 140, L: 200 };

export const clampLines = (lines) => ({
    display: '-webkit-box',
    WebkitLineClamp: lines,
    WebkitBoxOrient: 'vertical',
    overflow: 'hidden',
});

// TMDb list results ({ id, title, poster_path, release_date, vote_average })
// in the shape PosterCard takes.
export const fromTmdb = (m) => ({
    id: m.id,
    title: m.title,
    posterPath: m.poster_path,
    year: m.release_date ? m.release_date.slice(0, 4) : null,
});

// Lift on hover, press to 0.98 (the card itself is the link or button).
export const posterCardInteraction = {
    display: 'block',
    color: 'inherit',
    textDecoration: 'none',
    borderRadius: '12px',
    WebkitTapHighlightColor: 'transparent',
    '& [data-testid="poster"]': {
        transition: (theme) => `transform ${theme.motion.fast}ms ${theme.motion.easing}, box-shadow ${theme.motion.fast}ms ${theme.motion.easing}`,
    },
    '@media (hover: hover)': {
        '&:hover [data-testid="poster"]': {
            transform: 'translateY(-4px)',
            boxShadow: '0 10px 24px rgba(0,0,0,.35)',
        },
    },
    '&:active [data-testid="poster"]': { transform: 'scale(0.98)' },
    '@media (prefers-reduced-motion: reduce)': {
        '&:hover [data-testid="poster"], &:active [data-testid="poster"]': { transform: 'none' },
    },
};

