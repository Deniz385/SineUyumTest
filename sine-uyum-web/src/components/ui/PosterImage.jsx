import React, { useState } from 'react';
import { Box } from '@mui/material';
import MovieRoundedIcon from '@mui/icons-material/MovieRounded';
import { posterHairline, posterSrcSet, tmdbImage } from './posterUtils';

/**
 * A movie poster: always 2:3, rounded, with a hairline and a film-icon
 * placeholder while it loads or when it is missing. Lazy by default.
 * `title` is the accessible name; `decorative` hides it from screen readers
 * (when the title is already written next to it). `width` (px) picks the
 * TMDb size; the poster itself fills its container.
 */
export const PosterImage = ({ path, title, width = 160, radius = 'md', decorative = false, eager = false, sx, ...rest }) => {
    const url = tmdbImage(path, 'w342');
    const [loadedUrl, setLoadedUrl] = useState(null);
    const [failedUrl, setFailedUrl] = useState(null);
    const loaded = url && loadedUrl === url;
    const failed = !url || failedUrl === url;
    const borderRadius = (theme) => `${theme.radii[radius] ?? radius}px`;

    return (
        <Box
            data-testid="poster"
            role={failed && !decorative ? 'img' : undefined}
            aria-label={failed && !decorative ? title : undefined}
            aria-hidden={failed && decorative ? true : undefined}
            sx={[
                {
                    position: 'relative',
                    width: '100%',
                    aspectRatio: '2 / 3',
                    borderRadius,
                    overflow: 'hidden',
                    bgcolor: 'surface.raised',
                    flexShrink: 0,
                    '&::after': posterHairline,
                },
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
            {...rest}
        >
            {!loaded && (
                <Box
                    aria-hidden="true"
                    data-testid={failed ? 'poster-placeholder' : undefined}
                    sx={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'text.muted' }}
                >
                    <MovieRoundedIcon sx={{ fontSize: Math.max(20, Math.min(40, width / 4)), opacity: 0.7 }} />
                </Box>
            )}
            {!failed && (
                <Box
                    component="img"
                    src={url}
                    srcSet={posterSrcSet(path)}
                    sizes={`${width}px`}
                    alt={decorative ? '' : title || ''}
                    loading={eager ? 'eager' : 'lazy'}
                    decoding="async"
                    onLoad={() => setLoadedUrl(url)}
                    onError={() => setFailedUrl(url)}
                    sx={{
                        position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'cover', display: 'block',
                        opacity: loaded ? 1 : 0,
                        transition: (theme) => `opacity ${theme.motion.standard}ms ${theme.motion.easing}`,
                    }}
                />
            )}
        </Box>
    );
};
