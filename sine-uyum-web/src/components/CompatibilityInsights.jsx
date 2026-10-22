import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import ForumIcon from '@mui/icons-material/Forum';
import FavoriteIcon from '@mui/icons-material/Favorite';

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

const MovieRow = ({ movie, children }) => (
    <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', textAlign: 'left' }}>
        <Link to={`/movie/${movie.movieId}`}>
            <img
                src={movie.posterPath ? `${IMAGE_BASE_URL}${movie.posterPath}` : '/vite.svg'}
                alt={movie.title}
                style={{ width: 64, borderRadius: 6 }}
            />
        </Link>
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
                    <Stack direction="row" spacing={1} sx={{ mt: 1, justifyContent: 'center', flexWrap: 'wrap' }}>
                        {sharedGenres.map(g => <Chip key={`s-${g}`} label={g} color="success" size="small" />)}
                        {divergentGenres.map(g => <Chip key={`d-${g}`} label={g} color="warning" size="small" variant="outlined" />)}
                    </Stack>
                </Box>
            )}

            {sharedFavorite && (
                <Card variant="outlined">
                    <CardContent>
                        <MovieRow movie={sharedFavorite}>
                            <Typography variant="overline" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                <FavoriteIcon fontSize="inherit" color="error" /> {t('compatibility.sharedFavorite')}
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
                <Card variant="outlined" sx={{ borderColor: 'warning.main' }}>
                    <CardContent>
                        <MovieRow movie={debateMovie}>
                            <Typography variant="overline" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                <ForumIcon fontSize="inherit" color="warning" /> {t('compatibility.debateMovie')}
                            </Typography>
                            <Typography variant="subtitle1">{debateMovie.title}</Typography>
                            <Typography variant="body2" color="text.secondary">
                                {t('compatibility.debateRatings', { myRating: debateMovie.currentUserRating, userName: otherUserName, theirRating: debateMovie.targetUserRating })}
                            </Typography>
                            <Button size="small" startIcon={<ForumIcon />} sx={{ mt: 1 }} onClick={() => askAbout(debateMovie)}>
                                {t('compatibility.askThem')}
                            </Button>
                        </MovieRow>
                    </CardContent>
                </Card>
            )}
        </Stack>
    );
};
