import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import ForumIcon from '@mui/icons-material/ForumRounded';
import FavoriteIcon from '@mui/icons-material/FavoriteRounded';
import { PosterImage } from './ui/PosterImage';


const MovieRow = ({ movie, children }) => (
    <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', textAlign: 'left' }}>
        <Box component={Link} to={`/movie/${movie.movieId}`} sx={{ width: 64, flexShrink: 0, borderRadius: 1 }}>
            <PosterImage path={movie.posterPath} title={movie.title} width={64} radius="sm" />
        </Box>
        <Box sx={{ flexGrow: 1 }}>{children}</Box>
    </Box>
);

// Why the compatibility score is what it is: shared/divergent genres, a shared
// favourite and a "debate movie" as a conversation starter.
export const CompatibilityInsights = ({ insights, otherUserId, otherUserName }) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    if (!insights) return null;
    const { summary, sharedGenres = [], divergentGenres = [], sharedFavorite, debateMovie } = insights;
    if (!summary && !sharedFavorite && !debateMovie) return null;

    const askAbout = (movie) => {
        const draft = t('compatibility.askDraft', { title: movie.title, myRating: movie.currentUserRating, theirRating: movie.targetUserRating });
        navigate(`/messages/${otherUserId}?draft=${encodeURIComponent(draft)}`);
    };

    return (
        <Stack spacing={2} sx={{ my: 3 }} data-testid="compatibility-insights">
            {summary && (
                <Box>
                    <Typography variant="body1" sx={{ fontWeight: 500 }}>{summary}</Typography>
                    <Stack direction="row" useFlexGap sx={{ mt: 1, flexWrap: 'wrap', gap: 1 }}>
                        {sharedGenres.map(g => <Chip key={`s-${g}`} label={g} size="small" sx={{ bgcolor: 'transparent', border: 1, borderColor: 'success.main', color: 'text.primary' }} />)}
                        {divergentGenres.map(g => <Chip key={`d-${g}`} label={g} size="small" variant="outlined" sx={{ borderStyle: 'dashed', color: 'text.secondary' }} />)}
                    </Stack>
                </Box>
            )}

            {sharedFavorite && (
                <Card variant="outlined">
                    <CardContent>
                        <MovieRow movie={sharedFavorite}>
                            <Typography variant="overline" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                <FavoriteIcon fontSize="inherit" sx={{ color: 'accent.main' }} /> {t('compatibility.sharedFavorite')}
                            </Typography>
                            <Typography variant="subtitle1">{sharedFavorite.title}</Typography>
                            <Typography variant="body2" color="text.secondary">
                                {t('compatibility.sharedFavoriteRatings', { myRating: sharedFavorite.currentUserRating, userName: otherUserName, theirRating: sharedFavorite.targetUserRating })}
                            </Typography>
                        </MovieRow>
                    </CardContent>
                </Card>
            )}

            {debateMovie && (
                <Card variant="outlined">
                    <CardContent>
                        <MovieRow movie={debateMovie}>
                            <Typography variant="overline" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                <ForumIcon fontSize="inherit" sx={{ color: 'primary.main' }} /> {t('compatibility.debateMovie')}
                            </Typography>
                            <Typography variant="subtitle1">{debateMovie.title}</Typography>
                            <Typography variant="body2" color="text.secondary">
                                {t('compatibility.debateRatings', { myRating: debateMovie.currentUserRating, userName: otherUserName, theirRating: debateMovie.targetUserRating })}
                            </Typography>
                            <Button size="small" variant="tonal" startIcon={<ForumIcon />} sx={{ mt: 1 }} onClick={() => askAbout(debateMovie)}>
                                {t('compatibility.askThem')}
                            </Button>
                        </MovieRow>
                    </CardContent>
                </Card>
            )}
        </Stack>
    );
};
