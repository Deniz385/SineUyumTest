// sine-uyum-web/src/pages/MovieDetailPage.jsx
import React, { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Chip, CircularProgress, Button, IconButton, Modal, List, ListItem, ListItemButton, ListItemText, Divider } from '@mui/material'; // ListItemButton eklendi
import { RatingModal } from '../components/RatingModal';
import { AlertMessage } from '../components/AlertMessage';
import { BookmarkAdd } from '@mui/icons-material';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w500';

const modalStyle = {
  position: 'absolute',
  top: '50%',
  left: '50%',
  transform: 'translate(-50%, -50%)',
  width: 400,
  bgcolor: 'background.paper',
  border: '2px solid #000',
  boxShadow: 24,
  p: 4,
};

export const MovieDetailPage = () => {
    const { movieId } = useParams();
    const { token } = useAuth();
    const [movie, setMovie] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [isRatingModalOpen, setIsRatingModalOpen] = useState(false);
    const [ratingMessage, setRatingMessage] = useState({ type: '', text: '' });
    const [isWatchlistModalOpen, setIsWatchlistModalOpen] = useState(false);
    const [userLists, setUserLists] = useState([]);
    const [watchlistMessage, setWatchlistMessage] = useState({ type: '', text: '' });

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
            } finally {
                setIsLoading(false);
            }
        };
        fetchMovieDetails();
    }, [movieId, token]);
    
    const handleOpenWatchlistModal = async () => {
        setIsWatchlistModalOpen(true);
        setWatchlistMessage({ type: '', text: '' });
        try {
            const response = await axios.get(`${API_URL}/api/watchlist`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            setUserLists(response.data);
        } catch (err) {
            setWatchlistMessage({ type: 'error', text: 'Listeleriniz yüklenemedi.' });
        }
    };

    const handleAddMovieToList = async (listId) => {
        const movieData = {
            movieId: movie.id,
            title: movie.title,
            posterPath: movie.poster_path
        };

        try {
            await axios.post(`${API_URL}/api/watchlist/${listId}/movies`, movieData, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            setWatchlistMessage({ type: 'success', text: 'Film listeye başarıyla eklendi!' });
        } catch (err) {
            const errorMessage = err.response?.data?.message || 'Film listeye eklenemedi.';
            setWatchlistMessage({ type: 'error', text: errorMessage });
        } finally {
            setIsWatchlistModalOpen(false);
        }
    };

    const handleRateMovie = async (movieToRate, rating) => {
        setRatingMessage({ type: 'info', text: `'${movieToRate.title}' için puanınız kaydediliyor...` });
        try {
            try {
                await axios.post(`${API_URL}/api/ratings/addmovie`,
                    { id: movieToRate.id, title: movieToRate.title, posterPath: movieToRate.poster_path },
                    { headers: { 'Authorization': `Bearer ${token}` } }
                );
            } catch (addMovieError) {
                if (addMovieError.response && addMovieError.response.status !== 400) {
                    throw addMovieError;
                }
            }
            await axios.post(`${API_URL}/api/ratings`,
                { movieId: movieToRate.id, rating: rating },
                { headers: { 'Authorization': `Bearer ${token}` } }
            );
            setRatingMessage({ type: 'success', text: `'${movieToRate.title}' filmine ${rating} puan verdiniz. Başarıyla kaydedildi!` });
        } catch (error) {
            setRatingMessage({ type: 'error', text: 'Puan kaydedilirken bir hata oluştu.' });
        }
    };
    
    if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;
    if (error) return <div className="page-container"><AlertMessage type="error" message={error} /></div>;
    if (!movie) return null;

    const tmdbScore = Math.round(movie.vote_average * 10);

    return (
        <>
            <div className="page-container movie-detail-container">
                <img src={movie.poster_path ? `${IMAGE_BASE_URL}${movie.poster_path}` : '/vite.svg'} alt={movie.title} className="movie-detail-poster" />
                <div className="movie-detail-info">
                    <Typography variant="h3" component="h1" sx={{ fontWeight: 'bold' }}>{movie.title}</Typography>
                    {movie.tagline && <Typography variant="h6" color="text.secondary" gutterBottom>"{movie.tagline}"</Typography>}
                    <Box sx={{ display: 'flex', alignItems: 'center', my: 2, gap: 2 }}>
                        <Box className="tmdb-score-circle" sx={{ borderColor: tmdbScore > 70 ? 'success.main' : tmdbScore > 40 ? 'warning.main' : 'error.main' }}>
                            <Typography variant="h6" component="span">{tmdbScore}%</Typography>
                        </Box>
                        <Button variant="contained" size="large" onClick={() => setIsRatingModalOpen(true)}>Bu Filmi Puanla</Button>
                        <IconButton onClick={handleOpenWatchlistModal} color="primary" title="Listeye Ekle"><BookmarkAdd fontSize="large" /></IconButton>
                    </Box>
                    {watchlistMessage.text && <AlertMessage type={watchlistMessage.type} message={watchlistMessage.text} />}
                    {ratingMessage.text && <AlertMessage type={ratingMessage.type} message={ratingMessage.text} sx={{ mt: 2 }} />}
                    <Box sx={{ my: 2 }}>{movie.genres.map(genre => <Chip label={genre.name} key={genre.id} sx={{ mr: 1, mb: 1 }} />)}</Box>
                    <Typography variant="body1" paragraph>{movie.overview}</Typography>
                    <Typography variant="subtitle1">**Süre:** {movie.runtime} dakika</Typography>
                    <Typography variant="subtitle1">**Yayın Tarihi:** {new Date(movie.release_date).toLocaleDateString('tr-TR')}</Typography>
                    <Typography variant="h5" sx={{ mt: 3, mb: 2, fontWeight: 'bold' }}>Başrol Oyuncuları</Typography>
                    <div className="cast-list-visual">{movie.credits.cast.slice(0, 10).map(actor => (
                        <div key={actor.cast_id} className="actor-card-visual">
                           <img src={actor.profile_path ? `https://image.tmdb.org/t/p/w200${actor.profile_path}` : '/vite.svg'} alt={actor.name} />
                           <Typography variant="caption" component="div" sx={{ mt: 1 }}><strong>{actor.name}</strong></Typography>
                           <Typography variant="caption" color="text.secondary">{actor.character}</Typography>
                        </div>
                    ))}</div>
                </div>
            </div>

            {movie && <RatingModal movie={movie} open={isRatingModalOpen} onClose={() => setIsRatingModalOpen(false)} onRate={handleRateMovie} />}

            <Modal open={isWatchlistModalOpen} onClose={() => setIsWatchlistModalOpen(false)}>
                <Box sx={modalStyle}>
                    <Typography variant="h6" component="h2">Hangi Listeye Ekleyeceksin?</Typography>
                    <List sx={{ mt: 2 }}>
                        {userLists.length > 0 ? userLists.map((list) => (
                            // --- DEĞİŞİKLİK 1: ListItem + ListItemButton kullanımı ---
                            <ListItem key={list.id} disablePadding>
                                <ListItemButton onClick={() => handleAddMovieToList(list.id)}>
                                    <ListItemText primary={list.name} />
                                </ListItemButton>
                            </ListItem>
                        )) : (<Typography>Henüz listeniz yok.</Typography>)}
                         <Divider sx={{ my: 1 }} />
                         {/* --- DEĞİŞİKLİK 2: Link'i ListItemButton içine almak --- */}
                         <ListItem disablePadding>
                            <ListItemButton component={Link} to="/watchlist">
                                <ListItemText primary="Listeleri Yönet veya Yeni Liste Oluştur..." />
                            </ListItemButton>
                         </ListItem>
                    </List>
                </Box>
            </Modal>
        </>
    );
};