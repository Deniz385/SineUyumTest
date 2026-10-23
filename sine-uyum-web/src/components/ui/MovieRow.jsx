import React, { useCallback, useEffect, useId, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, IconButton, Tooltip, Typography } from '@mui/material';
import ChevronLeftRoundedIcon from '@mui/icons-material/ChevronLeftRounded';
import ChevronRightRoundedIcon from '@mui/icons-material/ChevronRightRounded';
import { PosterCard } from './PosterCard';
import { MovieRowSkeleton } from './Skeletons';
import { prefersReducedMotion } from './a11y';

/** A section title with an optional "See all" link or button on the right. */
export const SectionHeader = ({ title, id, seeAllTo, onSeeAll, seeAllLabel, action, component = 'h2', variant = 'h2', sx }) => {
    const { t } = useTranslation();
    const seeAll = seeAllTo || onSeeAll;
    return (
        <Box sx={[{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 1.5, minHeight: 36 }, ...(Array.isArray(sx) ? sx : [sx])]}>
            <Typography id={id} variant={variant} component={component} sx={{ minWidth: 0 }}>{title}</Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexShrink: 0 }}>
                {action}
                {seeAll && (
                    <Button
                        size="small"
                        {...(seeAllTo ? { component: Link, to: seeAllTo } : { onClick: onSeeAll })}
                        endIcon={<ChevronRightRoundedIcon />}
                        aria-label={`${seeAllLabel ?? t('ui.seeAll')}: ${title}`}
                        sx={{ color: 'text.secondary', '&:hover': { color: 'text.primary' } }}
                    >
                        {seeAllLabel ?? t('ui.seeAll')}
                    </Button>
                )}
            </Box>
        </Box>
    );
};

/**
 * A horizontal, snap-scrolling carousel of PosterCards with a title and an
 * optional "See all". The strip is a labelled, focusable region (arrow keys
 * scroll it; tabbing through the cards brings each into view) and, with a
 * mouse, arrow buttons page through it. `renderItem(movie)` replaces the
 * default card.
 */
export const MovieRow = ({
    title, movies, loading = false, size = 'M', seeAllTo, onSeeAll, renderItem, action, emptyText, sx, testId = 'movie-row',
}) => {
    const { t } = useTranslation();
    const headingId = useId();
    const scroller = useRef(null);
    const [edges, setEdges] = useState({ start: true, end: true });

    const updateEdges = useCallback(() => {
        const el = scroller.current;
        if (!el) return;
        setEdges({ start: el.scrollLeft <= 4, end: el.scrollLeft + el.clientWidth >= el.scrollWidth - 4 });
    }, []);

    useEffect(() => {
        updateEdges();
        const el = scroller.current;
        if (!el || typeof ResizeObserver === 'undefined') return undefined;
        const observer = new ResizeObserver(updateEdges);
        observer.observe(el);
        return () => observer.disconnect();
    }, [updateEdges, movies, loading]);

    const page = (direction) => {
        const el = scroller.current;
        if (!el) return;
        el.scrollBy({ left: direction * el.clientWidth * 0.85, behavior: prefersReducedMotion() ? 'auto' : 'smooth' });
    };

    if (loading) return <MovieRowSkeleton size={size} />;
    if (!movies?.length && !emptyText) return null;

    return (
        <Box component="section" data-testid={testId} sx={[{ mb: 4, minWidth: 0 }, ...(Array.isArray(sx) ? sx : [sx])]}>
            <SectionHeader
                id={headingId}
                title={title}
                seeAllTo={seeAllTo}
                onSeeAll={onSeeAll}
                action={
                    <>
                        {action}
                        {!(edges.start && edges.end) && (
                            <Box sx={{ display: { xs: 'none', md: 'flex' }, gap: 0.5 }}>
                                <Tooltip title={t('ui.scrollLeft')}>
                                    <span>
                                        <IconButton size="small" onClick={() => page(-1)} disabled={edges.start} aria-label={`${t('ui.scrollLeft')}: ${title}`}>
                                            <ChevronLeftRoundedIcon />
                                        </IconButton>
                                    </span>
                                </Tooltip>
                                <Tooltip title={t('ui.scrollRight')}>
                                    <span>
                                        <IconButton size="small" onClick={() => page(1)} disabled={edges.end} aria-label={`${t('ui.scrollRight')}: ${title}`}>
                                            <ChevronRightRoundedIcon />
                                        </IconButton>
                                    </span>
                                </Tooltip>
                            </Box>
                        )}
                    </>
                }
            />
            {movies?.length ? (
                <Box
                    ref={scroller}
                    role="region"
                    aria-labelledby={headingId}
                    tabIndex={0}
                    onScroll={updateEdges}
                    className="no-scrollbar"
                    sx={{
                        overflowX: 'auto',
                        overscrollBehaviorX: 'contain',
                        scrollSnapType: 'x mandatory',
                        scrollPaddingInline: 4,
                        // Room for the hover lift and the focus ring.
                        pt: 0.75, pb: 1, px: 0.5, mx: -0.5,
                        borderRadius: 1.5,
                    }}
                >
                    <Box component="ul" sx={{ display: 'flex', gap: { xs: 1.5, sm: 2 }, listStyle: 'none', m: 0, p: 0 }}>
                        {movies.map((movie) => (
                            <Box component="li" key={movie.id} sx={{ scrollSnapAlign: 'start', flexShrink: 0 }}>
                                {renderItem ? renderItem(movie) : <PosterCard movie={movie} size={size} />}
                            </Box>
                        ))}
                    </Box>
                </Box>
            ) : (
                <Typography variant="body2" color="text.secondary">{emptyText}</Typography>
            )}
        </Box>
    );
};
