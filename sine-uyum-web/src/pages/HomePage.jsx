import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Card, CardMedia, CircularProgress } from '@mui/material';
import { useDraggableScroll } from '../hooks/useDraggableScroll';

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
    const { user } = useAuth();
    const [popularMovies, setPopularMovies] = useState([]);
    const [nowPlayingMovies, setNowPlayingMovies] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchMovies = async () => {
            if (!user) return;
            
            setLoading(true);
            try {
                const [popularRes, nowPlayingRes] = await Promise.all([
                    api.get('/api/movies/popular'),
                    api.get('/api/movies/now_playing')
                ]);
                setPopularMovies(popularRes.data.results || []);
                setNowPlayingMovies(nowPlayingRes.data.results || []);
            } catch (error) {
                console.error("Ana sayfa filmleri yüklenemedi:", error);
            } finally {
                setLoading(false);
            }
        };
        fetchMovies();
    }, [user]);

    return (
        <Box sx={{ width: '100%', maxWidth: '1400px', margin: 'auto', px: { xs: 2, md: 4 }, py: 2 }}>
            <Typography variant="h3" component="h1" gutterBottom align="left">
                Keşfet
            </Typography>
            
            {loading ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', my: 5 }}>
                    <CircularProgress />
                </Box>
            ) : (
                <>
                    <MovieRow title="Popüler Filmler" movies={popularMovies} />
                    <MovieRow title="Vizyondakiler" movies={nowPlayingMovies} />
                </>
            )}
        </Box>
    );
};