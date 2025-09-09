// sine-uyum-web/src/pages/WatchlistDetailPage.jsx
import React, { useEffect, useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, Grid, IconButton, Button, Modal, TextField } from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

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

export const WatchlistDetailPage = () => {
    const { listId } = useParams();
    const { user, token } = useAuth(); // Mevcut kullanıcıyı da alıyoruz
    const navigate = useNavigate();
    const [watchlist, setWatchlist] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [isOwner, setIsOwner] = useState(false); // Listenin sahibi mi kontrolü

    const [isEditModalOpen, setIsEditModalOpen] = useState(false);
    const [editedListName, setEditedListName] = useState('');
    const [editedListDesc, setEditedListDesc] = useState('');

    const fetchWatchlistDetails = async () => {
        if (!token) return;
        setIsLoading(true);
        try {
            // Önce listenin public endpoint'ini deneriz. Bu herkes için çalışır.
            const response = await axios.get(`${API_URL}/api/watchlist/public/${listId}`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const listData = response.data;
            setWatchlist(listData);
            setEditedListName(listData.name);
            setEditedListDesc(listData.description || '');

            // Gelen verideki listenin sahibinin kullanıcı adıyla mevcut kullanıcınınkini karşılaştır
            if (user?.username === listData.ownerUsername) {
                setIsOwner(true);
            }

        } catch (err) {
            setError('Liste bulunamadı veya bir hata oluştu.');
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        fetchWatchlistDetails();
    }, [listId, token, user]); // user'ı bağımlılıklara ekledik

    const handleRemoveMovie = async (movieId) => {
        if (!window.confirm("Bu filmi listeden kaldırmak istediğinize emin misiniz?")) return;
        try {
            await axios.delete(`${API_URL}/api/watchlist/${listId}/movies/${movieId}`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            fetchWatchlistDetails();
        } catch (err) {
            alert("Film kaldırılırken bir hata oluştu.");
        }
    };

    const handleDeleteList = async () => {
        if (!window.confirm(`"${watchlist.name}" listesini kalıcı olarak silmek istediğinize emin misiniz?`)) return;
        try {
            await axios.delete(`${API_URL}/api/watchlist/${listId}`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            navigate('/watchlist');
        } catch (err) {
            alert("Liste silinirken bir hata oluştu.");
        }
    };

    const handleUpdateList = async (e) => {
        e.preventDefault();
        if (!editedListName.trim()) return;
        try {
            await axios.put(`${API_URL}/api/watchlist/${listId}`, 
                { name: editedListName, description: editedListDesc },
                { headers: { 'Authorization': `Bearer ${token}` } }
            );
            setIsEditModalOpen(false);
            fetchWatchlistDetails();
        } catch (err) {
            alert("Liste güncellenirken bir hata oluştu.");
        }
    };

    if (isLoading) return <CircularProgress />;
    if (error) return <Typography color="error">{error}</Typography>;

    return (
        <>
            <Box className="page-container" sx={{ maxWidth: '1000px' }}>
                <Box sx={{ mb: 3 }}>
                    <Typography variant="h4" component="h1" gutterBottom>{watchlist?.name}</Typography>
                    <Typography variant="body1" color="text.secondary">{watchlist?.description}</Typography>
                    <Typography variant="caption" color="text.secondary">Oluşturan: @{watchlist?.ownerUsername}</Typography>
                    
                    {/* Sadece listenin sahibi bu butonları görebilir */}
                    {isOwner && (
                        <Box sx={{ mt: 2, display: 'flex', gap: 1 }}>
                            <Button variant="outlined" startIcon={<EditIcon />} onClick={() => setIsEditModalOpen(true)}>Listeyi Düzenle</Button>
                            <Button variant="contained" color="error" startIcon={<DeleteIcon />} onClick={handleDeleteList}>Listeyi Sil</Button>
                        </Box>
                    )}
                </Box>

                {watchlist?.items.length === 0 ? (
                    <Typography>Bu listede henüz film yok.</Typography>
                ) : (
                    <Grid container spacing={3}>
                        {watchlist?.items.map(item => (
                            <Grid item xs={6} sm={4} md={3} key={item.movieId}>
                                <Box sx={{ position: 'relative' }}>
                                    <Link to={`/movie/${item.movieId}`}>
                                        <img src={item.posterPath ? `${IMAGE_BASE_URL}${item.posterPath}` : '/vite.svg'} alt={item.title} style={{ width: '100%', borderRadius: '8px', display: 'block' }}/>
                                        <Typography variant="subtitle2" align="center" sx={{ mt: 1 }}>{item.title}</Typography>
                                    </Link>
                                    {/* Sadece listenin sahibi film silebilmeli */}
                                    {isOwner && (
                                        <IconButton onClick={() => handleRemoveMovie(item.movieId)} sx={{ position: 'absolute', top: 0, right: 0, color: 'white', backgroundColor: 'rgba(0,0,0,0.6)' }} aria-label="delete">
                                            <DeleteIcon />
                                        </IconButton>
                                    )}
                                </Box>
                            </Grid>
                        ))}
                    </Grid>
                )}
            </Box>

            <Modal open={isEditModalOpen} onClose={() => setIsEditModalOpen(false)}>
                <Box sx={modalStyle} component="form" onSubmit={handleUpdateList}>
                    <Typography variant="h6" component="h2" sx={{ mb: 2 }}>Listeyi Düzenle</Typography>
                    <TextField autoFocus required margin="dense" label="Liste Adı" fullWidth variant="outlined" value={editedListName} onChange={(e) => setEditedListName(e.target.value)} />
                    <TextField margin="dense" label="Açıklama (Opsiyonel)" fullWidth multiline rows={3} variant="outlined" value={editedListDesc} onChange={(e) => setEditedListDesc(e.target.value)} />
                    <Button type="submit" variant="contained" sx={{ mt: 2 }}>Kaydet</Button>
                </Box>
            </Modal>
        </>
    );
};