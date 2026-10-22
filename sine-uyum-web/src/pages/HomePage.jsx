import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Card, CardMedia } from '@mui/material';
import { useDraggableScroll } from '../hooks/useDraggableScroll';
import { LoadingState } from '../components/states/LoadingState';
import { ErrorState } from '../components/states/ErrorState';

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w500';

const MovieRow = ({ title, movies }) => {
    const scrollRef = useDraggableScroll();
    return (
        <Box sx={{ my: 4 }}>
            <Typography variant="h4" component="h2" gutterBottom sx={{ textAlign: 'left', fontWeight: 'bold' }}>
                {title}
            </Typography>
            <Box ref={scrollRef} className="movie-row-container">
                {movies.map(movie => (
                    <Link to={`/movie/${movie.id}`} key={movie.id} className="movie-row-link">
                        <Card className="movie-row-card">
                            <CardMedia
                                component="img"
                                image={movie.poster_path ? `${IMAGE_BASE_URL.replace('w500', 'w300')}${movie.poster_path}` : '/vite.svg'}
                                alt={movie.title}
                                className="movie-row-poster"
                            />
                        </Card>
                    </Link>
                ))}
            </Box>
        </Box>
    );
};

export const HomePage = () => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const [popularMovies, setPopularMovies] = useState([]);
    const [nowPlayingMovies, setNowPlayingMovies] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(false);

    const fetchMovies = useCallback(async () => {
        if (!user) return;

        setLoading(true);
        setError(false);
        try {
            const [popularRes, nowPlayingRes] = await Promise.all([
                api.get('/api/v1/movies/popular'),
                api.get('/api/v1/movies/now_playing')
            ]);
            setPopularMovies(popularRes.data.results || []);
            setNowPlayingMovies(nowPlayingRes.data.results || []);
        } catch (error) {
            setError(true);
            console.error("Could not load home page movies:", error);
        } finally {
            setLoading(false);
        }
    }, [user]);

    useEffect(() => {
        fetchMovies();
    }, [fetchMovies]);

    return (
        <Box sx={{ width: '100%', maxWidth: '1400px', margin: 'auto', px: { xs: 2, md: 4 }, py: 2 }}>
            <Typography variant="h3" component="h1" gutterBottom align="left">
                {t('home.title')}
            </Typography>
            
            {loading ? (
                <LoadingState />
            ) : error ? (
                <ErrorState message={t('home.loadError')} onRetry={fetchMovies} />
            ) : (
                <>
                    <MovieRow title={t('home.popular')} movies={popularMovies} />
                    <MovieRow title={t('home.nowPlaying')} movies={nowPlayingMovies} />
                </>
            )}
        </Box>
    );
};