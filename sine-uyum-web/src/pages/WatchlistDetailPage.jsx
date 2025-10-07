import React, { useEffect, useState, useCallback } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, Grid, IconButton, Button, Modal, TextField } from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { useSnackbar } from '../context/SnackbarProvider'; // <-- YENİ EKLEME

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
    const { user } = useAuth();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar(); // <-- YENİ EKLEME
    const [watchlist, setWatchlist] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [isOwner, setIsOwner] = useState(false);

    const [isEditModalOpen, setIsEditModalOpen] = useState(false);
    const [editedListName, setEditedListName] = useState('');
    const [editedListDesc, setEditedListDesc] = useState('');
    
    const [dialog, setDialog] = useState({ isOpen: false, title: '', description: '', onConfirm: null });

    const fetchWatchlistDetails = useCallback(async () => {
        if (!user) return;
        setIsLoading(true);
        try {
            const response = await api.get(`/api/watchlist/public/${listId}`);
            const listData = response.data;
            setWatchlist(listData);
            setEditedListName(listData.name);
            setEditedListDesc(listData.description || '');

            if (user?.username === listData.ownerUsername) {
                setIsOwner(true);
            }

        } catch (err) {
            setError('Liste bulunamadı veya bir hata oluştu.');
        } finally {
            setIsLoading(false);
        }
    }, [listId, user]);

    useEffect(() => {
        fetchWatchlistDetails();
    }, [fetchWatchlistDetails]);

    const handleRemoveMovie = (movieId) => {
        setDialog({
            isOpen: true,
            title: 'Filmi Kaldır?',
            description: 'Bu filmi listeden kalıcı olarak kaldırmak istediğinize emin misiniz?',
            onConfirm: () => {
                setDialog({ isOpen: false });
                performRemoveMovie(movieId);
            }
        });
    };

    const performRemoveMovie = async (movieId) => {
        try {
            await api.delete(`/api/watchlist/${listId}/movies/${movieId}`);
            showSnackbar('Film listeden başarıyla kaldırıldı!', 'success');
            fetchWatchlistDetails();
        } catch (err) {
            showSnackbar("Film kaldırılırken bir hata oluştu.", 'error');
        }
    };

    const handleDeleteList = () => {
        setDialog({
            isOpen: true,
            title: 'Listeyi Sil?',
            description: `"${watchlist.name}" listesini kalıcı olarak silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`,
            onConfirm: () => {
                setDialog({ isOpen: false });
                performDeleteList();
            }
        });
    };

    // --- ANA DÜZELTME BURADA ---
    const performDeleteList = async () => {
        try {
            await api.delete(`/api/watchlist/${listId}`);
            showSnackbar('Liste başarıyla silindi. Yönlendiriliyorsunuz...', 'success');
            // Yönlendirmeyi küçük bir gecikmeyle yaparak "aria-hidden" uyarısını önlüyoruz.
            setTimeout(() => navigate('/watchlist'), 1500);
        } catch (err) {
            showSnackbar("Liste silinirken bir hata oluştu.", 'error');
        }
    };
    // --- DÜZELTME BİTİŞİ ---

    const handleUpdateList = async (e) => {
        e.preventDefault();
        if (!editedListName.trim()) return;
        try {
            await api.put(`/api/watchlist/${listId}`, 
                { name: editedListName, description: editedListDesc }
            );
            showSnackbar('Liste başarıyla güncellendi!', 'success');
            setIsEditModalOpen(false);
            fetchWatchlistDetails();
        } catch (err) {
            showSnackbar("Liste güncellenirken bir hata oluştu.", 'error');
        }
    };

    if (isLoading) return <CircularProgress />;
    if (error) return <Typography color="error">{error}</Typography>;

    const items = watchlist?.items || [];

    return (
        <>
            <Box className="page-container" sx={{ maxWidth: '1000px' }}>
                <Box sx={{ mb: 3 }}>
                    <Typography variant="h4" component="h1" gutterBottom>{watchlist?.name}</Typography>
                    <Typography variant="body1" color="text.secondary">{watchlist?.description}</Typography>
                    <Typography variant="caption" color="text.secondary">Oluşturan: @{watchlist?.ownerUsername}</Typography>
                    
                    {isOwner && (
                        <Box sx={{ mt: 2, display: 'flex', gap: 1 }}>
                            <Button variant="outlined" startIcon={<EditIcon />} onClick={() => setIsEditModalOpen(true)}>Listeyi Düzenle</Button>
                            <Button variant="contained" color="error" startIcon={<DeleteIcon />} onClick={handleDeleteList}>Listeyi Sil</Button>
                        </Box>
                    )}
                </Box>

                {items.length === 0 ? (
                    <Typography>Bu listede henüz film yok.</Typography>
                ) : (
                    <Grid container spacing={3}>
                        {items.map(item => (
                            <Grid item xs={6} sm={4} md={3} key={item.movieId}>
                                <Box sx={{ position: 'relative' }}>
                                    <Link to={`/movie/${item.movieId}`}>
                                        <img src={item.posterPath ? `${IMAGE_BASE_URL}${item.posterPath}` : '/vite.svg'} alt={item.title} style={{ width: '100%', borderRadius: '8px', display: 'block' }}/>
                                        <Typography variant="subtitle2" align="center" sx={{ mt: 1 }}>{item.title}</Typography>
                                    </Link>
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
                    <TextField autoFocus required margin="dense" label="Liste Adı" fullWidth variant="outlined" value={editedListName} onChange={(e) => setNewListName(e.target.value)} />
                    <TextField margin="dense" label="Açıklama (Opsiyonel)" fullWidth multiline rows={3} variant="outlined" value={editedListDesc} onChange={(e) => setNewListDesc(e.target.value)} />
                    <Button type="submit" variant="contained" sx={{ mt: 2 }}>Kaydet</Button>
                </Box>
            </Modal>
            <ConfirmationDialog
                open={dialog.isOpen}
                onClose={() => setDialog({ ...dialog, isOpen: false })}
                onConfirm={dialog.onConfirm}
                title={dialog.title}
                description={dialog.description}
            />
        </>
    );
};