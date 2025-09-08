// sine-uyum-web/src/pages/WatchlistPage.jsx
import React, { useEffect, useState } from 'react';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, Grid } from '@mui/material';
import { Link } from 'react-router-dom';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

export const WatchlistPage = () => {
    const { token } = useAuth();
    const [watchlist, setWatchlist] = useState([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        const fetchWatchlist = async () => {
            if (!token) return;
            try {
                const response = await axios.get(`${API_URL}/api/watchlist`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                setWatchlist(response.data);
            } catch (err) {
                setError('İzleme listesi yüklenirken bir hata oluştu.');
            } finally {
                setIsLoading(false);
            }
        };

        fetchWatchlist();
    }, [token]);

    if (isLoading) return <CircularProgress />;
    if (error) return <Typography color="error">{error}</Typography>;

    return (
        <Box className="page-container">
            <Typography variant="h4" component="h1" gutterBottom>
                İzleme Listem
            </Typography>
            {watchlist.length === 0 ? (
                <Typography>İzleme listeniz boş.</Typography>
            ) : (
                <Grid container spacing={3}>
                    {watchlist.map(item => (
                        <Grid item xs={6} sm={4} md={3} key={item.movieId}>
                            <Link to={`/movie/${item.movieId}`}>
                                <img 
                                    src={item.posterPath ? `${IMAGE_BASE_URL}${item.posterPath}` : '/vite.svg'} 
                                    alt={item.title} 
                                    style={{ width: '100%', borderRadius: '8px' }}
                                />
                                <Typography variant="subtitle2" align="center">{item.title}</Typography>
                            </Link>
                        </Grid>
                    ))}
                </Grid>
            )}
        </Box>
    );
};