import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Card, CardMedia, CircularProgress } from '@mui/material';
import { useDraggableScroll } from '../hooks/useDraggableScroll';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w500';

// Yatay film listesi için yeniden kullanılabilir bir bileşen
const MovieRow = ({ title, movies }) => {
    // --- DEĞİŞİKLİK BURADA: Sürükleme hook'unu çağırıp ref'i alıyoruz ---
    const scrollRef = useDraggableScroll();

    return (
        <Box sx={{ my: 4 }}>
            <Typography variant="h4" component="h2" gutterBottom sx={{ textAlign: 'left', fontWeight: 'bold' }}>
                {title}
            </Typography>
            {/* --- DEĞİŞİKLİK BURADA: ref'i kaydırılacak olan Box'a atıyoruz --- */}
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
    const { token } = useAuth();
    const [popularMovies, setPopularMovies] = useState([]);
    const [nowPlayingMovies, setNowPlayingMovies] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchMovies = async () => {
            if (!token) return;
            setLoading(true);
            try {
                const [popularRes, nowPlayingRes] = await Promise.all([
                    axios.get(`${API_URL}/api/movies/popular`, { headers: { 'Authorization': `Bearer ${token}` } }),
                    axios.get(`${API_URL}/api/movies/now_playing`, { headers: { 'Authorization': `Bearer ${token}` } })
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
    }, [token]);

    // --- DEĞİŞİKLİK BURADA: Eski page-container yerine MUI Box kullanıyoruz ---
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

