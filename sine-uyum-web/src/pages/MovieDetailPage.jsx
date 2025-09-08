// sine-uyum-web/src/pages/MovieDetailPage.jsx
import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Chip, CircularProgress, Button, IconButton } from '@mui/material'; // IconButton eklendi
import { RatingModal } from '../components/RatingModal';
import { AlertMessage } from '../components/AlertMessage';
import { BookmarkAdd, BookmarkAdded } from '@mui/icons-material'; // İkonlar import edildi

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

    // İzleme listesi için state'ler
    const [isInWatchlist, setIsInWatchlist] = useState(false);
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
                console.error(err);
            } finally {
                setIsLoading(false);
            }
        };
        fetchMovieDetails();
    }, [movieId, token]);

    // Sayfa yüklendiğinde filmin izleme listesinde olup olmadığını kontrol et
    useEffect(() => {
        const checkWatchlistStatus = async () => {
            if (!token) return;
            try {
                const response = await axios.get(`${API_URL}/api/watchlist`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                const watchlist = response.data;
                const movieInList = watchlist.some(item => item.movieId === parseInt(movieId));
                setIsInWatchlist(movieInList);
            } catch (err) {
                console.error("İzleme listesi durumu kontrol edilirken hata:", err);
            }
        };
        checkWatchlistStatus();
    }, [movieId, token]);

   const handleRateMovie = async (movieToRate, rating) => {
    setRatingMessage({ type: 'info', text: `'${movieToRate.title}' için puanınız kaydediliyor...` });

    try {
        // Adım 1: Filmi veritabanına eklemeyi dene.
        // Bu isteğin başarısız olması (çünkü film zaten var) sorun değil.
        try {
            await axios.post(`${API_URL}/api/ratings/addmovie`,
                { id: movieToRate.id, title: movieToRate.title, posterPath: movieToRate.poster_path },
                { headers: { 'Authorization': `Bearer ${token}` } }
            );
        } catch (addMovieError) {
            // Sadece 400 (Bad Request) hatasını görmezden geliyoruz,
            // çünkü bunun "film zaten mevcut" anlamına geldiğini varsayıyoruz.
            if (addMovieError.response && addMovieError.response.status !== 400) {
                // Eğer hata 500 gibi başka bir sunucu hatasıysa, işlemi durdur.
                throw addMovieError;
            }
            // Hata 400 ise, loglayıp devam et.
            console.log("Film veritabanında zaten vardı, oylama işlemine devam ediliyor.");
        }

        // Adım 2: Puanı kaydet/güncelle. Bu asıl önemli olan kısım.
        await axios.post(`${API_URL}/api/ratings`,
            { movieId: movieToRate.id, rating: rating },
            { headers: { 'Authorization': `Bearer ${token}` } }
        );

        setRatingMessage({ type: 'success', text: `'${movieToRate.title}' filmine ${rating} puan verdiniz. Başarıyla kaydedildi!` });

    } catch (error) {
        // Bu blok artık sadece asıl puanlama işlemindeki veya beklenmedik
        // film ekleme hatalarındaki hataları yakalayacak.
        console.error("Puanlama hatası:", error);
        setRatingMessage({ type: 'error', text: 'Puan kaydedilirken bir hata oluştu.' });
    }
};
    // İzleme listesine ekleme/çıkarma fonksiyonu
    const handleToggleWatchlist = async () => {
    setWatchlistMessage({ type: 'info', text: 'İşleniyor...' });
    
    // POST URL'i artık movieID içermiyor
    const postUrl = `${API_URL}/api/watchlist`; 
    // DELETE URL'i eskisi gibi movieID içeriyor
    const deleteUrl = `${API_URL}/api/watchlist/${movieId}`;

    try {
        if (isInWatchlist) {
            // Listeden çıkar (DELETE isteği değişmedi)
            await axios.delete(deleteUrl, { headers: { 'Authorization': `Bearer ${token}` } });
            setWatchlistMessage({ type: 'success', text: 'Film izleme listesinden kaldırıldı.' });
        } else {
            // Listeye ekle (POST isteği güncellendi)
            const movieData = {
                movieId: movie.id,
                title: movie.title,
                posterPath: movie.poster_path
            };
            await axios.post(postUrl, movieData, { headers: { 'Authorization': `Bearer ${token}` } });
            setWatchlistMessage({ type: 'success', text: 'Film izleme listesine eklendi.' });
        }
        setIsInWatchlist(!isInWatchlist); // Durumu tersine çevir
    } catch (error) {
        console.error("İzleme listesi hatası:", error.response?.data?.message || error.message);
        const errorMessage = error.response?.data?.message || 'İşlem sırasında bir hata oluştu.';
        setWatchlistMessage({ type: 'error', text: errorMessage });
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
                        <IconButton onClick={handleToggleWatchlist} color="primary" title={isInWatchlist ? "İzleme Listesinden Kaldır" : "İzleme Listesine Ekle"}>
                            {isInWatchlist ? <BookmarkAdded fontSize="large" /> : <BookmarkAdd fontSize="large" />}
                        </IconButton>
                    </Box>

                    {/* Mesajları göstermek için AlertMessage bileşenleri */}
                    {watchlistMessage.text && <AlertMessage type={watchlistMessage.type} message={watchlistMessage.text} />}
                    {ratingMessage.text && <AlertMessage type={ratingMessage.type} message={ratingMessage.text} sx={{ mt: 2 }} />}

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