import React from 'react';
import { Box } from '@mui/material';
import { alpha, keyframes, useTheme } from '@mui/material/styles';
import { tmdbImage } from './posterUtils';

const drift = keyframes`
  from { transform: rotate(-8deg) scale(1.25) translateY(0); }
  to { transform: rotate(-8deg) scale(1.25) translateY(-6%); }
`;

const TILE_COUNT = 48;

/**
 * A cinematic, decorative wall of posters behind sign-in and the welcome
 * flow: tilted, dimmed and covered by a scrim so text on top stays above
 * 4.5:1. Without poster paths it draws abstract tiles in warm hues. It
 * drifts slowly unless the OS asks for reduced motion. Hidden from screen
 * readers.
 */
export const PosterWall = ({ posters = [], sx }) => {
    const theme = useTheme();
    const bg = theme.palette.tokens.bg;
    const isDark = theme.palette.mode === 'dark';
    const tiles = Array.from({ length: TILE_COUNT }, (_, i) => (posters.length ? posters[i % posters.length] : null));

    return (
        <Box aria-hidden="true" data-testid="poster-wall" sx={[{ position: 'fixed', inset: 0, overflow: 'hidden', zIndex: 0, bgcolor: 'background.default' }, ...(Array.isArray(sx) ? sx : [sx])]}>
            <Box
                sx={{
                    position: 'absolute', inset: '-10%',
                    display: 'grid', gridTemplateColumns: { xs: 'repeat(4, 1fr)', sm: 'repeat(6, 1fr)', md: 'repeat(8, 1fr)' }, gap: { xs: 1, md: 1.5 },
                    transform: 'rotate(-8deg) scale(1.25)',
                    animation: `${drift} 60s linear infinite alternate`,
                    opacity: isDark ? 0.55 : 0.4,
                    '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
                }}
            >
                {tiles.map((path, i) => (
                    <Box
                        key={i}
                        sx={{
                            aspectRatio: '2 / 3', borderRadius: 1.5, overflow: 'hidden',
                            background: path
                                ? undefined
                                : `linear-gradient(${140 + (i * 37) % 80}deg, hsl(${(i * 47) % 60 + 10}, 70%, ${isDark ? 22 + (i % 4) * 4 : 70 + (i % 4) * 4}%), hsl(${(i * 47 + 200) % 360}, 45%, ${isDark ? 10 : 86}%))`,
                        }}
                    >
                        {path && (
                            <Box component="img" src={tmdbImage(path, 'w185')} alt="" loading="lazy" decoding="async"
                                sx={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }} />
                        )}
                    </Box>
                ))}
            </Box>
            <Box
                sx={{
                    position: 'absolute', inset: 0,
                    background: `radial-gradient(ellipse at 50% 40%, ${alpha(bg, 0.55)} 0%, ${alpha(bg, 0.85)} 60%, ${bg} 100%), linear-gradient(${alpha(bg, 0.4)}, ${alpha(bg, 0.4)})`,
                }}
            />
        </Box>
    );
};
