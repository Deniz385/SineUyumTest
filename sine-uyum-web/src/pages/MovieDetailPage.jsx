import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Chip, CircularProgress, Button } from '@mui/material';
import { RatingModal } from '../components/RatingModal'; // Puanlama modalını import et
import { AlertMessage } from '../components/AlertMessage'; // Mesaj bileşenini import et

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w500';

export const MovieDetailPage = () => {
    const { movieId } = useParams();
    const { token } = useAuth();
    const [movie, setMovie] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    
    // Modal ve puanlama için state'ler
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [ratingMessage, setRatingMessage] = useState({ type: '', text: '' });


    useEffect(() => {
        const fetchMovieDetails = async () => {
            if (!token) return;
            setIsLoading(true);
            try {
                const response = await axios.get(`${API_URL}/api/movies/${movieId}`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                setMovie(response.data);
            } catch (err) {
                setError('Film detayları yüklenirken bir hata oluştu.');
                console.error(err);
            } finally {
                setIsLoading(false);
            }
        };
        fetchMovieDetails();
    }, [movieId, token]);

    const handleRateMovie = async (movieToRate, rating) => {
        setRatingMessage({ type: 'info', text: `'${movieToRate.title}' için puanınız kaydediliyor...` });
        try {
            await axios.post(`${API_URL}/api/ratings/addmovie`, 
                { id: movieToRate.id, title: movieToRate.title, posterPath: movieToRate.poster_path },
                { headers: { 'Authorization': `Bearer ${token}` } }
            );
            await axios.post(`${API_URL}/api/ratings`, 
                { movieId: movieToRate.id, rating: rating },
                { headers: { 'Authorization': `Bearer ${token}` } }
            );
            setRatingMessage({ type: 'success', text: `'${movieToRate.title}' filmine ${rating} puan verdiniz. Başarıyla kaydedildi!` });
        } catch (error) {
            console.error("Puanlama hatası:", error);
            setRatingMessage({ type: 'error', text: 'Puan kaydedilirken bir hata oluştu.' });
        }
    };


    if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;
    if (error) return <div className="page-container"><AlertMessage type="error" text={error} /></div>;
    if (!movie) return null;

    const tmdbScore = Math.round(movie.vote_average * 10);

    return (
        <>
            <div className="page-container movie-detail-container">
                <img 
                    src={movie.poster_path ? `${IMAGE_BASE_URL}${movie.poster_path}` : '/vite.svg'} 
                    alt={movie.title} 
                    className="movie-detail-poster"
                />
                <div className="movie-detail-info">
                    <Typography variant="h3" component="h1" sx={{ fontWeight: 'bold' }}>{movie.title}</Typography>
                    {movie.tagline && <Typography variant="h6" color="text.secondary" gutterBottom>"{movie.tagline}"</Typography>}
                    
                    <Box sx={{ display: 'flex', alignItems: 'center', my: 2, gap: 2 }}>
                        <Box className="tmdb-score-circle" sx={{ borderColor: tmdbScore > 70 ? 'success.main' : tmdbScore > 40 ? 'warning.main' : 'error.main' }}>
                            <Typography variant="h6" component="span">{tmdbScore}%</Typography>
                        </Box>
                        <Button variant="contained" size="large" onClick={() => setIsModalOpen(true)}>
                            Bu Filmi Puanla
                        </Button>
                    </Box>

                    {ratingMessage.text && <AlertMessage type={ratingMessage.type} text={ratingMessage.text} sx={{ mb: 2 }} />}
                    
                    <Box sx={{ my: 2 }}>
                        {movie.genres.map(genre => <Chip label={genre.name} key={genre.id} sx={{ mr: 1, mb: 1 }} />)}
                    </Box>
                    <Typography variant="body1" paragraph>{movie.overview}</Typography>
                    <Typography variant="subtitle1">**Süre:** {movie.runtime} dakika</Typography>
                    <Typography variant="subtitle1">**Yayın Tarihi:** {new Date(movie.release_date).toLocaleDateString('tr-TR')}</Typography>
                    
                    <Typography variant="h5" sx={{ mt: 3, mb: 2, fontWeight: 'bold' }}>Başrol Oyuncuları</Typography>
                    <div className="cast-list-visual">
                        {movie.credits.cast.slice(0, 10).map(actor => (
                            <div key={actor.cast_id} className="actor-card-visual">
                               <img 
                                    src={actor.profile_path ? `https://image.tmdb.org/t/p/w200${actor.profile_path}` : '/vite.svg'} 
                                    alt={actor.name} 
                               />
                               <Typography variant="caption" component="div" sx={{ mt: 1 }}><strong>{actor.name}</strong></Typography>
                               <Typography variant="caption" color="text.secondary">{actor.character}</Typography>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
            
            {movie && (
                <RatingModal 
                    movie={movie}
                    open={isModalOpen}
                    onClose={() => setIsModalOpen(false)}
                    onRate={handleRateMovie}
                />
            )}
        </>
    );
};

