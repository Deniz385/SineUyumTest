import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import { Box, Button, Card, CircularProgress, IconButton, Stack, TextField, Tooltip, Typography } from '@mui/material';
import { keyframes } from '@mui/material/styles';
import FavoriteIcon from '@mui/icons-material/FavoriteRounded';
import FavoriteBorderIcon from '@mui/icons-material/FavoriteBorderRounded';
import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutlineRounded';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineRounded';
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded';
import { UserAvatar } from './ui/UserRow';
import { CompatibilityBadge } from './ui/CompatibilityBadge';
import { RatingStars } from './ui/RatingInput';
import { PosterImage } from './ui/PosterImage';
import { apiErrorMessage } from './ui/apiError';
import { timeSince } from './navigation';

// Likes pop: 1 → 1.2 → 1 in 200ms (no movement with reduced motion).
const pop = keyframes`
  0% { transform: scale(1); }
  50% { transform: scale(1.2); }
  100% { transform: scale(1); }
`;

const formatWhen = (iso, language) => new Date(iso).toLocaleDateString(language, { day: 'numeric', month: 'short', year: 'numeric' });

/**
 * Who did it: avatar, @name, an optional line (e.g. "rated a film"), the
 * rating as stars, the compatibility badge and when.
 */
export const AuthorLine = ({ author, rating, compatibility, when, relative = false, children }) => {
    const { t, i18n } = useTranslation();
    return (
        <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.25, minWidth: 0 }}>
            <Box component={Link} to={`/profile/${author.id}`} tabIndex={-1} aria-hidden="true" sx={{ borderRadius: '50%', flexShrink: 0 }}>
                <UserAvatar user={author} size={36} />
            </Box>
            <Box sx={{ flex: 1, minWidth: 0 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap', rowGap: 0.25 }}>
                    <Typography component={Link} to={`/profile/${author.id}`} variant="subtitle2" sx={{ color: 'text.primary', '&:hover': { textDecoration: 'underline' } }}>
                        @{author.userName}
                    </Typography>
                    {children}
                    {compatibility != null && (
                        <Tooltip title={t('reviews.compatibilityTooltip')}>
                            <span><CompatibilityBadge value={compatibility} size="sm" /></span>
                        </Tooltip>
                    )}
                </Box>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 0.25, flexWrap: 'wrap' }}>
                    {rating != null && <RatingStars value={rating} size="small" showNumber />}
                    {when && (
                        <Typography variant="body2" color="text.secondary" component="span">
                            {relative ? timeSince(when, i18n.language, t('notifications.justNow')) : formatWhen(when, i18n.language)}
                        </Typography>
                    )}
                </Box>
            </Box>
        </Box>
    );
};

const Comments = ({ reviewId, onCountChange }) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [comments, setComments] = useState(null);
    const [text, setText] = useState('');
    const [isSending, setIsSending] = useState(false);

    React.useEffect(() => {
        let cancelled = false;
        api.get(`/api/v1/reviews/${reviewId}/comments`)
            .then((res) => { if (!cancelled) setComments(res.data); })
            .catch(() => { if (!cancelled) setComments([]); });
        return () => { cancelled = true; };
    }, [reviewId]);

    const send = async (e) => {
        e.preventDefault();
        if (!text.trim()) return;
        setIsSending(true);
        try {
            const res = await api.post(`/api/v1/reviews/${reviewId}/comments`, { text });
            setComments((prev) => [...(prev ?? []), res.data]);
            onCountChange(1);
            setText('');
        } catch (error) {
            showSnackbar(apiErrorMessage(error, t('reviews.comments.sendFailed')), 'error');
        } finally {
            setIsSending(false);
        }
    };

    const remove = async (comment) => {
        try {
            await api.delete(`/api/v1/reviews/comments/${comment.id}`);
            setComments((prev) => prev.filter((c) => c.id !== comment.id));
            onCountChange(-1);
        } catch {
            showSnackbar(t('reviews.comments.deleteFailed'), 'error');
        }
    };

    if (comments === null) return <CircularProgress size={20} sx={{ mt: 1 }} aria-label={t('common.loading')} />;
    return (
        <Box sx={{ mt: 1.5, pt: 1.5, borderTop: 1, borderColor: 'divider' }}>
            {comments.map((c) => (
                <Box key={c.id} sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', mb: 1.25 }}>
                    <UserAvatar user={c.author} size={28} />
                    <Typography variant="body1" sx={{ flexGrow: 1, minWidth: 0, overflowWrap: 'anywhere' }}>
                        <Box component={Link} to={`/profile/${c.author.id}`} sx={{ fontWeight: 600, color: 'text.primary' }}>@{c.author.userName}</Box>{' '}
                        {c.text}
                    </Typography>
                    {c.canDelete && (
                        <Tooltip title={t('reviews.comments.delete')}>
                            <IconButton size="small" aria-label={t('reviews.comments.delete')} onClick={() => remove(c)}><DeleteOutlineIcon fontSize="small" /></IconButton>
                        </Tooltip>
                    )}
                </Box>
            ))}
            <Box component="form" onSubmit={send} sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                <TextField size="small" fullWidth placeholder={t('reviews.comments.placeholder')} value={text}
                    slotProps={{ htmlInput: { maxLength: 500, 'aria-label': t('reviews.comments.placeholder') } }}
                    onChange={(e) => setText(e.target.value)} />
                <Button type="submit" variant="tonal" disabled={isSending || !text.trim()}>{t('common.send')}</Button>
            </Box>
        </Box>
    );
};

/** Like and comment buttons under a review. */
const ReviewActions = ({ review, onLike, onComments, commentsOpen }) => {
    const { t } = useTranslation();
    const liked = review.likedByMe;
    return (
        <Stack direction="row" spacing={0.5} sx={{ mt: 1, ml: -1 }}>
            <Button
                size="small"
                color={liked ? 'secondary' : 'inherit'}
                startIcon={liked ? <FavoriteIcon /> : <FavoriteBorderIcon />}
                onClick={onLike}
                aria-pressed={liked}
                aria-label={`${liked ? t('reviews.unlike') : t('reviews.like')} (${review.likeCount})`}
                sx={{
                    color: liked ? undefined : 'text.secondary',
                    '& .MuiButton-startIcon': liked ? { animation: `${pop} 200ms ease-out` } : undefined,
                }}
                className="tabular-nums"
            >
                {review.likeCount}
            </Button>
            <Button size="small" color="inherit" startIcon={<ChatBubbleOutlineIcon />} onClick={onComments} aria-expanded={commentsOpen} sx={{ color: 'text.secondary' }}>
                {t('reviews.commentCount', { count: review.commentCount })}
            </Button>
        </Stack>
    );
};

/**
 * A single review: author, rating, compatibility, spoiler blur, likes and
 * comments. With `showMovie` it leads with the film's poster (feed).
 */
export const ReviewCard = ({ review: initial, showMovie = false, headline, relativeTime = false }) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [review, setReview] = useState(initial);
    const [revealed, setRevealed] = useState(false);
    const [showComments, setShowComments] = useState(false);

    React.useEffect(() => setReview(initial), [initial]);

    const toggleLike = async () => {
        const liked = !review.likedByMe;
        // Optimistic update; the server response corrects the count.
        setReview((r) => ({ ...r, likedByMe: liked, likeCount: r.likeCount + (liked ? 1 : -1) }));
        try {
            const res = liked
                ? await api.post(`/api/v1/reviews/${review.id}/like`)
                : await api.delete(`/api/v1/reviews/${review.id}/like`);
            setReview((r) => ({ ...r, likedByMe: res.data.liked, likeCount: res.data.likeCount }));
        } catch (error) {
            setReview((r) => ({ ...r, likedByMe: !liked, likeCount: r.likeCount + (liked ? -1 : 1) }));
            showSnackbar(apiErrorMessage(error, t('reviews.likeFailed')), 'error');
        }
    };

    const hidden = review.containsSpoilers && !revealed && !review.isMine;

    const body = (
        <>
            <AuthorLine author={review.author} rating={review.rating} compatibility={review.authorCompatibility} when={review.createdAt} relative={relativeTime}>
                {headline}
            </AuthorLine>
            {showMovie && (
                <Typography component={Link} to={`/movie/${review.movie.id}`} variant="title" sx={{ display: 'block', mt: 1, color: 'text.primary', '&:hover': { textDecoration: 'underline' } }}>
                    {review.movie.title}
                </Typography>
            )}
            <Box sx={{ mt: 1, position: 'relative' }}>
                <Typography
                    variant="body1"
                    aria-hidden={hidden || undefined}
                    sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', color: 'text.primary', filter: hidden ? 'blur(6px)' : 'none', userSelect: hidden ? 'none' : 'auto' }}
                >
                    {review.text}
                </Typography>
                {hidden && (
                    <Box sx={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <Button variant="tonal" size="small" startIcon={<VisibilityRoundedIcon />} onClick={() => setRevealed(true)}>{t('reviews.spoilerReveal')}</Button>
                    </Box>
                )}
            </Box>
            <ReviewActions review={review} onLike={toggleLike} onComments={() => setShowComments((v) => !v)} commentsOpen={showComments} />
            {showComments && (
                <Comments reviewId={review.id} onCountChange={(delta) => setReview((r) => ({ ...r, commentCount: r.commentCount + delta }))} />
            )}
        </>
    );

    return (
        <Card data-testid="review-card" component="article" sx={{ p: 2 }}>
            {showMovie ? (
                <Box sx={{ display: 'flex', gap: 2, alignItems: 'flex-start' }}>
                    <Box component={Link} to={`/movie/${review.movie.id}`} tabIndex={-1} aria-hidden="true" sx={{ width: { xs: 64, sm: 84 }, flexShrink: 0, borderRadius: 1 }}>
                        <PosterImage path={review.movie.posterPath} title={review.movie.title} width={84} radius="sm" decorative />
                    </Box>
                    <Box sx={{ flex: 1, minWidth: 0 }}>{body}</Box>
                </Box>
            ) : body}
        </Card>
    );
};
