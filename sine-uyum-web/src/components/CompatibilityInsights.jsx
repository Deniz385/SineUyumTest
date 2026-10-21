import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
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

// Uyum puanının nedeni: ortak/ayrışan türler, ortak favori ve sohbet
// başlatıcı olarak "tartışma filmi".
export const CompatibilityInsights = ({ insights, otherUserId, otherUserName }) => {
    const navigate = useNavigate();
    if (!insights) return null;
    const { summary, sharedGenres = [], divergentGenres = [], sharedFavorite, debateMovie } = insights;
    if (!summary && !sharedFavorite && !debateMovie) return null;

    const askAbout = (movie) => {
        const draft = `"${movie.title}" filmine ben ${movie.currentUserRating}, sen ${movie.targetUserRating} vermişsin. Neden? 😄`;
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
                                <FavoriteIcon fontSize="inherit" color="error" /> Ortak favoriniz
                            </Typography>
                            <Typography variant="subtitle1">{sharedFavorite.title}</Typography>
                            <Typography variant="body2" color="text.secondary">
                                Sen {sharedFavorite.currentUserRating}, {otherUserName} {sharedFavorite.targetUserRating}
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
                                <ForumIcon fontSize="inherit" color="warning" /> Tartışma filmi
                            </Typography>
                            <Typography variant="subtitle1">{debateMovie.title}</Typography>
                            <Typography variant="body2" color="text.secondary">
                                Sen {debateMovie.currentUserRating} verdin, {otherUserName} {debateMovie.targetUserRating}. Sor bakalım neden?
                            </Typography>
                            <Button size="small" startIcon={<ForumIcon />} sx={{ mt: 1 }} onClick={() => askAbout(debateMovie)}>
                                Ona sor
                            </Button>
                        </MovieRow>
                    </CardContent>
                </Card>
            )}
        </Stack>
    );
};
