import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Typography } from '@mui/material';
import StarRoundedIcon from '@mui/icons-material/StarRounded';
import { PosterImage } from './PosterImage';
import { CompatibilityBadge } from './CompatibilityBadge';
import { srOnly } from './a11y';
import { POSTER_CARD_WIDTHS, clampLines, posterCardInteraction } from './posterUtils';

/**
 * Poster + title (2 lines max) + optional meta row: year, a rating out of 10
 * and a compatibility badge. Sizes S 96, M 140, L 200 wide; `fluid` fills
 * its grid cell instead. Links to the movie unless `onClick` is given (then it
 * is a button) or `to` overrides the target.
 */
export const PosterCard = ({
    movie, size = 'M', fluid = false, to, onClick, rating, compatibility, meta, showTitle = true,
    overlay, ariaLabel, sx, ...rest
}) => {
    const { t } = useTranslation();
    const width = POSTER_CARD_WIDTHS[size] ?? POSTER_CARD_WIDTHS.M;
    const small = size === 'S';
    const year = movie.year;
    const hasMeta = year || rating != null || compatibility != null || meta;
    const linkProps = onClick
        ? { component: 'button', type: 'button', onClick }
        : { component: Link, to: to ?? `/movie/${movie.id}` };

    return (
        <Box
            {...linkProps}
            aria-label={ariaLabel}
            data-testid="poster-card"
            sx={[
                posterCardInteraction,
                {
                    width: fluid ? '100%' : width,
                    flexShrink: 0,
                    textAlign: 'left',
                    p: 0,
                    border: 0,
                    background: 'none',
                    font: 'inherit',
                    cursor: 'pointer',
                },
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
            {...rest}
        >
            <Box sx={{ position: 'relative' }}>
                <PosterImage path={movie.posterPath} title={movie.title} width={width} radius={small ? 'sm' : 'md'} decorative={showTitle} />
                {overlay}
            </Box>
            {showTitle && (
                <Typography
                    variant={small ? 'body2' : 'subtitle2'}
                    component="span"
                    sx={{ ...clampLines(2), mt: 1, fontWeight: 600, color: 'text.primary', wordBreak: 'break-word' }}
                >
                    {movie.title}
                </Typography>
            )}
            {showTitle && hasMeta && (
                <Box
                    component="span"
                    sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mt: 0.5, color: 'text.secondary', minHeight: 20, flexWrap: 'wrap' }}
                >
                    {year && <Typography variant="body2" component="span" className="tabular-nums">{year}</Typography>}
                    {rating != null && (
                        <Typography variant="body2" component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.25, color: 'text.primary', fontWeight: 600 }} className="tabular-nums">
                            <StarRoundedIcon aria-hidden="true" sx={{ fontSize: 16, color: 'primary.main' }} />
                            <span aria-hidden="true">{rating}</span>
                            <Box component="span" sx={srOnly}>{t('rating.outOf', { value: rating })}</Box>
                        </Typography>
                    )}
                    {compatibility != null && <CompatibilityBadge value={compatibility} size="sm" />}
                    {meta}
                </Box>
            )}
        </Box>
    );
};
