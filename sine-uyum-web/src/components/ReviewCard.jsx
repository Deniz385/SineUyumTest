import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider';
import {
    Avatar, Box, Button, Card, CardContent, Chip, CircularProgress, IconButton, Stack, TextField, Tooltip, Typography
} from '@mui/material';
import FavoriteIcon from '@mui/icons-material/Favorite';
import FavoriteBorderIcon from '@mui/icons-material/FavoriteBorder';
import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutline';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import StarIcon from '@mui/icons-material/Star';

const POSTER = 'https://image.tmdb.org/t/p/w92';

const formatWhen = (iso, language) => new Date(iso).toLocaleDateString(language, { day: 'numeric', month: 'short', year: 'numeric' });

export const AuthorLine = ({ author, rating, compatibility, when, children }) => {
    const { t, i18n } = useTranslation();
    return (
        <Stack direction="row" spacing={1.5} alignItems="center" sx={{ flexWrap: 'wrap', rowGap: 1 }}>
            <Avatar component={Link} to={`/profile/${author.id}`} src={mediaUrl(author.profileImageUrl)} sx={{ width: 36, height: 36 }}>
                {author.userName?.charAt(0).toUpperCase()}
            </Avatar>
            <Box sx={{ flexGrow: 1, textAlign: 'left' }}>
                <Typography component={Link} to={`/profile/${author.id}`} sx={{ fontWeight: 600, color: 'inherit', textDecoration: 'none' }}>
                    {author.userName}
                </Typography>
                {children}
                {when && <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{formatWhen(when, i18n.language)}</Typography>}
            </Box>
            {rating != null && <Chip size="small" icon={<StarIcon />} label={`${rating}/10`} />}
            {compatibility != null && (
                <Tooltip title={t('reviews.compatibilityTooltip')}>
                    <Chip size="small" color={compatibility >= 70 ? 'success' : 'default'} variant="outlined" label={t('reviews.compatibilityChip', { compatibility })} />
                </Tooltip>
            )}
        </Stack>
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
            showSnackbar(error.response?.data?.title || t('reviews.comments.sendFailed'), 'error');
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

    if (comments === null) return <CircularProgress size={20} sx={{ mt: 1 }} />;
    return (
        <Box sx={{ mt: 1.5, pl: { xs: 0, sm: 6 } }}>
            {comments.map((c) => (
                <Box key={c.id} sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', mb: 1, textAlign: 'left' }}>
                    <Avatar src={mediaUrl(c.author.profileImageUrl)} sx={{ width: 24, height: 24, fontSize: 12 }}>
                        {c.author.userName?.charAt(0).toUpperCase()}
                    </Avatar>
                    <Typography variant="body2" sx={{ flexGrow: 1 }}>
                        <Link to={`/profile/${c.author.id}`} style={{ fontWeight: 600, color: 'inherit', textDecoration: 'none' }}>{c.author.userName}</Link>{' '}
                        {c.text}
                    </Typography>
                    {c.canDelete && (
                        <IconButton size="small" aria-label={t('reviews.comments.delete')} onClick={() => remove(c)}><DeleteOutlineIcon fontSize="small" /></IconButton>
                    )}
                </Box>
            ))}
            <Box component="form" onSubmit={send} sx={{ display: 'flex', gap: 1 }}>
                <TextField size="small" fullWidth placeholder={t('reviews.comments.placeholder')} value={text} inputProps={{ maxLength: 500 }}
                    onChange={(e) => setText(e.target.value)} />
                <Button type="submit" disabled={isSending || !text.trim()}>{t('common.send')}</Button>
            </Box>
        </Box>
    );
};

// A single review: author, rating, compatibility badge, spoiler blur, likes and comments.
export const ReviewCard = ({ review: initial, showMovie = false, headline }) => {
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
            showSnackbar(error.response?.data?.title || t('reviews.likeFailed'), 'error');
        }
    };

    const hidden = review.containsSpoilers && !revealed && !review.isMine;

    return (
        <Card variant="outlined" data-testid="review-card">
            <CardContent>
                <AuthorLine
                    author={review.author}
                    rating={review.rating}
                    compatibility={review.authorCompatibility}
                    when={review.createdAt}
                >
                    {headline}
                </AuthorLine>

                {showMovie && (
                    <Box component={Link} to={`/movie/${review.movie.id}`}
                        sx={{ display: 'flex', gap: 1.5, alignItems: 'center', mt: 1.5, color: 'inherit', textDecoration: 'none' }}>
                        {review.movie.posterPath && <img src={`${POSTER}${review.movie.posterPath}`} alt="" style={{ width: 46, borderRadius: 4 }} />}
                        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{review.movie.title}</Typography>
                    </Box>
                )}

                <Box sx={{ mt: 1.5, textAlign: 'left', position: 'relative' }}>
                    <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap', filter: hidden ? 'blur(6px)' : 'none', userSelect: hidden ? 'none' : 'auto' }}>
                        {review.text}
                    </Typography>
                    {hidden && (
                        <Box sx={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <Button variant="contained" size="small" onClick={() => setRevealed(true)}>{t('reviews.spoilerReveal')}</Button>
                        </Box>
                    )}
                </Box>

                <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                    <Button size="small" color={review.likedByMe ? 'error' : 'inherit'}
                        startIcon={review.likedByMe ? <FavoriteIcon /> : <FavoriteBorderIcon />} onClick={toggleLike}
                        aria-label={review.likedByMe ? t('reviews.unlike') : t('reviews.like')}>
                        {review.likeCount}
                    </Button>
                    <Button size="small" color="inherit" startIcon={<ChatBubbleOutlineIcon />} onClick={() => setShowComments((v) => !v)}>
                        {t('reviews.commentCount', { count: review.commentCount })}
                    </Button>
                </Stack>
                {showComments && (
                    <Comments
                        reviewId={review.id}
                        onCountChange={(delta) => setReview((r) => ({ ...r, commentCount: r.commentCount + delta }))}
                    />
                )}
            </CardContent>
        </Card>
    );
};
