import React, { useEffect, useState, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Chip, CircularProgress, Button, IconButton, Modal, List, ListItem, ListItemButton, ListItemText, Divider } from '@mui/material';
import { RatingModal } from '../components/RatingModal';
import BookmarkAddIcon from '@mui/icons-material/BookmarkAdd';
import { useSnackbar } from '../context/SnackbarProvider'; // <-- SNACKBAR KANCASINI İÇE AKTAR

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
    const { user } = useAuth();
    const { showSnackbar } = useSnackbar(); // <-- SNACKBAR FONKSİYONUNU AL
    const [movie, setMovie] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [isRatingModalOpen, setIsRatingModalOpen] = useState(false);
    const [isWatchlistModalOpen, setIsWatchlistModalOpen] = useState(false);
    const [userLists, setUserLists] = useState([]);
    const [refetchTrigger, setRefetchTrigger] = useState(0);

    const fetchMovieDetails = useCallback(async () => {
        if (!user) return;
        setIsLoading(true);
        try {
            const response = await api.get(`/api/movies/${movieId}`);
            setMovie(response.data);
        } catch (err) {
            setError('Film detayları yüklenirken bir hata oluştu.');
        } finally {
            setIsLoading(false);
        }
    }, [movieId, user]);

    useEffect(() => {
        fetchMovieDetails();
    }, [fetchMovieDetails, refetchTrigger]);
    
    const handleOpenWatchlistModal = async () => {
        setIsWatchlistModalOpen(true);
        try {
            const response = await api.get(`/api/watchlist`);
            // $values kontrolü eklendi
            setUserLists(response.data.$values || response.data);
        } catch (err) {
            showSnackbar('Listeleriniz yüklenemedi.', 'error');
        }
    };

    const handleAddMovieToList = async (listId) => {
        setIsWatchlistModalOpen(false);
        const movieData = { movieId: movie.id, title: movie.title, posterPath: movie.poster_path };
        try {
            await api.post(`/api/watchlist/${listId}/movies`, movieData);
            showSnackbar('Film listeye başarıyla eklendi!', 'success');
        } catch (err) {
            const errorMessage = err.response?.data?.message || 'Film listeye eklenemedi.';
            showSnackbar(errorMessage, 'error');
        }
    };

    const handleRateMovie = async (movieToRate, rating) => {
        try {
            await api.post(`/api/ratings/addmovie`, { 
                id: movieToRate.id, 
                title: movieToRate.title, 
                posterPath: movieToRate.poster_path 
            });
            await api.post(`/api/ratings`, { 
                movieId: movieToRate.id, 
                rating: rating 
            });
            showSnackbar(`'${movieToRate.title}' filmine ${rating} puan verdiniz!`, 'success');
            setRefetchTrigger(prev => prev + 1);
        } catch (error) {
            showSnackbar('Puan kaydedilirken bir hata oluştu.', 'error');
        }
    };
    
    if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;
    if (error) return <div className="page-container"><Typography color="error">{error}</Typography></div>;
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
                        <IconButton onClick={handleOpenWatchlistModal} color="primary" title="Listeye Ekle"><BookmarkAddIcon fontSize="large" /></IconButton>
                    </Box>
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
                            <ListItem key={list.id} disablePadding>
                                <ListItemButton onClick={() => handleAddMovieToList(list.id)}>
                                    <ListItemText primary={list.name} />
                                </ListItemButton>
                            </ListItem>
                        )) : (<Typography>Henüz listeniz yok.</Typography>)}
                         <Divider sx={{ my: 1 }} />
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

