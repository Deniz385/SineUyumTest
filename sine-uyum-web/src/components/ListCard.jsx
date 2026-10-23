import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
    Avatar, Box, Button, Card, CardActionArea, CardActions, CardContent, Grid, Link as MuiLink, Stack, Tooltip, Typography,
} from '@mui/material';
import FavoriteIcon from '@mui/icons-material/FavoriteRounded';
import FavoriteBorderIcon from '@mui/icons-material/FavoriteBorderRounded';
import BookmarkIcon from '@mui/icons-material/BookmarkRounded';
import BookmarkBorderIcon from '@mui/icons-material/BookmarkBorderRounded';
import MovieIcon from '@mui/icons-material/MovieRounded';
import PlaylistPlayIcon from '@mui/icons-material/PlaylistPlayRounded';
import { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { CompatibilityBadge } from './ui/CompatibilityBadge';
import { posterHairline } from './ui/posterUtils';

const POSTER_BASE_URL = 'https://image.tmdb.org/t/p/w342';

const PosterCell = ({ path }) => {
    const [failed, setFailed] = useState(false);
    if (!path || failed) {
        return (
            <Box data-testid="poster-placeholder" sx={{ bgcolor: 'surface.raised', display: 'flex', alignItems: 'center', justifyContent: 'center', minWidth: 0, minHeight: 0 }}>
                <MovieIcon aria-hidden="true" sx={{ color: 'text.muted', fontSize: 28, opacity: 0.7 }} />
            </Box>
        );
    }
    return (
        <Box
            component="img"
            src={`${POSTER_BASE_URL}${path}`}
            alt=""
            loading="lazy"
            onError={() => setFailed(true)}
            sx={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block', minWidth: 0, minHeight: 0 }}
        />
    );
};

// A 2×2 collage of the latest posters; missing ones become placeholders.
export const PosterCollage = ({ posters = [] }) => {
    if (posters.length === 0) {
        return (
            <Box data-testid="poster-collage" sx={{ aspectRatio: '1 / 1', bgcolor: 'surface.raised', borderRadius: 1.5, position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center', '&::after': posterHairline }}>
                <PlaylistPlayIcon aria-hidden="true" sx={{ color: 'text.muted', fontSize: 56 }} />
            </Box>
        );
    }
    const cells = Array.from({ length: 4 }, (_, i) => posters[i] ?? null);
    return (
        <Box
            data-testid="poster-collage"
            sx={{ aspectRatio: '1 / 1', display: 'grid', gridTemplateColumns: '1fr 1fr', gridTemplateRows: '1fr 1fr', gap: '2px', bgcolor: 'background.default', overflow: 'hidden', borderRadius: 1.5, position: 'relative', '&::after': posterHairline }}
        >
            {cells.map((path, i) => <PosterCell key={i} path={path} />)}
        </Box>
    );
};

/**
 * Like and save buttons with counts. Without `onToggle` (my own list) the
 * counts are shown read-only. `size="medium"` suits a page header.
 */
export const ListReactions = ({ list, onToggle, size = 'small' }) => {
    const { t } = useTranslation();
    const liked = Boolean(list.likedByMe);
    const saved = Boolean(list.savedByMe);
    const likeCount = list.likeCount ?? 0;
    const saveCount = list.saveCount ?? 0;

    if (!onToggle) {
        return (
            <Stack direction="row" spacing={2} alignItems="center" sx={{ color: 'text.secondary' }} data-testid="list-counts">
                <Tooltip title={t('lists.likeCount', { count: likeCount })}>
                    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }} aria-label={t('lists.likeCount', { count: likeCount })} role="img">
                        <FavoriteIcon fontSize="small" aria-hidden="true" />
                        <Typography variant="body2" component="span">{likeCount}</Typography>
                    </Box>
                </Tooltip>
                <Tooltip title={t('lists.saveCount', { count: saveCount })}>
                    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }} aria-label={t('lists.saveCount', { count: saveCount })} role="img">
                        <BookmarkIcon fontSize="small" aria-hidden="true" />
                        <Typography variant="body2" component="span">{saveCount}</Typography>
                    </Box>
                </Tooltip>
            </Stack>
        );
    }

    const buttonSx = { minWidth: 0, px: size === 'small' ? 1 : 2 };
    return (
        <Stack direction="row" spacing={1} alignItems="center">
            <Tooltip title={liked ? t('lists.unlike') : t('lists.like')}>
                <Button
                    size={size}
                    variant={size === 'small' ? 'text' : (liked ? 'contained' : 'outlined')}
                    color={liked ? 'secondary' : 'inherit'}
                    startIcon={liked ? <FavoriteIcon /> : <FavoriteBorderIcon />}
                    aria-pressed={liked}
                    aria-label={`${liked ? t('lists.unlike') : t('lists.like')} (${t('lists.likeCount', { count: likeCount })})`}
                    onClick={() => onToggle(list, 'like')}
                    sx={buttonSx}
                    data-testid="like-button"
                >
                    {likeCount}
                </Button>
            </Tooltip>
            <Tooltip title={saved ? t('lists.unsave') : t('lists.save')}>
                <Button
                    size={size}
                    variant={size === 'small' ? 'text' : (saved ? 'contained' : 'outlined')}
                    color={saved ? 'primary' : 'inherit'}
                    startIcon={saved ? <BookmarkIcon /> : <BookmarkBorderIcon />}
                    aria-pressed={saved}
                    aria-label={`${saved ? t('lists.unsave') : t('lists.save')} (${t('lists.saveCount', { count: saveCount })})`}
                    onClick={() => onToggle(list, 'save')}
                    sx={buttonSx}
                    data-testid="save-button"
                >
                    {saveCount}
                </Button>
            </Tooltip>
        </Stack>
    );
};

export const OwnerLink = ({ owner, size = 24 }) => (
    <MuiLink
        component={Link}
        to={`/profile/${owner?.id}`}
        underline="hover"
        color="text.secondary"
        sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, minWidth: 0, maxWidth: '100%' }}
    >
        <Avatar src={mediaUrl(owner?.profileImageUrl)} alt="" sx={{ width: size, height: size, fontSize: size * 0.5 }}>
            {owner?.userName?.charAt(0).toUpperCase()}
        </Avatar>
        <Typography variant="body2" component="span" noWrap>@{owner?.userName}</Typography>
    </MuiLink>
);

const clamp = (lines) => ({ display: '-webkit-box', WebkitLineClamp: lines, WebkitBoxOrient: 'vertical', overflow: 'hidden' });

// A public list in discovery, on a profile or among saved lists: a 2×2
// poster collage, name, owner chip, film count, compatibility, like/save.
export const PublicListCard = ({ list, onToggle }) => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const isMine = list.owner?.id === user?.id;
    const compatibility = typeof list.ownerCompatibility === 'number' ? Math.round(list.ownerCompatibility) : null;

    return (
        <Card data-testid="list-card" sx={{ height: '100%', display: 'flex', flexDirection: 'column', textAlign: 'left', overflow: 'hidden', transition: (theme) => `background-color ${theme.motion.fast}ms`, '&:hover': { bgcolor: 'surface.raised' } }}>
            <CardActionArea component={Link} to={`/watchlist/${list.id}`} sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', alignItems: 'stretch', justifyContent: 'flex-start', p: 1.25, pb: 0, borderRadius: 0 }}>
                <PosterCollage posters={list.posters} />
                <CardContent sx={{ px: 0.75, pt: 1.5, pb: 1 }}>
                    <Typography variant="title" component="h3" sx={{ ...clamp(2), display: '-webkit-box' }}>{list.name}</Typography>
                    {list.description && (
                        <Typography variant="body2" color="text.secondary" sx={{ ...clamp(2), mt: 0.5 }}>{list.description}</Typography>
                    )}
                </CardContent>
            </CardActionArea>
            <Box sx={{ px: 2, display: 'flex', flexDirection: 'column', gap: 1 }}>
                <OwnerLink owner={list.owner} />
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                    <Typography variant="body2" color="text.secondary" className="tabular-nums">{t('lists.filmCount', { count: list.itemCount ?? 0 })}</Typography>
                    {compatibility !== null && !isMine && (
                        <CompatibilityBadge value={compatibility} size="sm" text={t('lists.compatible', { score: compatibility })} data-testid="compat-chip" />
                    )}
                </Box>
            </Box>
            <CardActions sx={{ px: 1, pt: 0.5, pb: 1 }}>
                <ListReactions list={list} onToggle={isMine ? undefined : onToggle} />
            </CardActions>
        </Card>
    );
};

// A responsive grid of list cards.
export const ListCardGrid = ({ lists, onToggle, columns = { xs: 12, sm: 6, md: 4, lg: 3 } }) => (
    <Grid container spacing={2.5}>
        {lists.map((list) => (
            <Grid key={list.id} size={columns}>
                <PublicListCard list={list} onToggle={onToggle} />
            </Grid>
        ))}
    </Grid>
);
