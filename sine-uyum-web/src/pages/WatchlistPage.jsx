import React, { useEffect, useState, useCallback } from 'react';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, Grid, Card, CardContent, Button, Modal, TextField } from '@mui/material';
import { Link } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';

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

export const WatchlistPage = () => {
    const { user } = useAuth(); // token yerine user kullanılıyor
    const [watchlists, setWatchlists] = useState([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [open, setOpen] = useState(false);
    const [newListName, setNewListName] = useState('');
    const [newListDesc, setNewListDesc] = useState('');

    const fetchWatchlists = useCallback(async () => {
        if (!user) return; // user objesinin varlığı kontrol ediliyor
        setIsLoading(true);
        try {
            // İstek 'api' üzerinden yapılıyor ve header kaldırılıyor
            const response = await api.get(`/api/watchlist`);
            setWatchlists(response.data);
        } catch (err) {
            setError('Listeler yüklenirken bir hata oluştu.');
        } finally {
            setIsLoading(false);
        }
    }, [user]); // useCallback bağımlılığı 'user' olarak güncellendi

    useEffect(() => {
        fetchWatchlists();
    }, [fetchWatchlists]);

    const handleCreateList = async (e) => {
        e.preventDefault();
        if (!newListName.trim()) return;

        try {
            // İstek 'api' üzerinden yapılıyor ve header kaldırılıyor
            await api.post(`/api/watchlist`, { name: newListName, description: newListDesc });
            setOpen(false);
            setNewListName('');
            setNewListDesc('');
            fetchWatchlists(); // Listeyi yenile
        } catch (err) {
            setError("Yeni liste oluşturulurken bir hata oluştu.");
        }
    };

    if (isLoading) return <CircularProgress />;
    if (error) return <Typography color="error">{error}</Typography>;

    return (
        <>
            <Box className="page-container" sx={{ maxWidth: '1000px' }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
                    <Typography variant="h4" component="h1">İzleme Listelerim</Typography>
                    <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>Yeni Liste Oluştur</Button>
                </Box>
                
                {watchlists.length === 0 ? (
                    <Typography>Henüz hiç izleme listeniz yok. Hemen bir tane oluşturun!</Typography>
                ) : (
                    <Grid container spacing={3}>
                        {watchlists.map(list => (
                            <Grid item xs={12} sm={6} md={4} key={list.id}>
                                <Card component={Link} to={`/watchlist/${list.id}`} sx={{ textDecoration: 'none', height: '100%' }}>
                                    <CardContent>
                                        <Typography variant="h6">{list.name}</Typography>
                                        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{list.description}</Typography>
                                        <Typography variant="body2" color="text.secondary">{list.itemCount} film</Typography>
                                    </CardContent>
                                </Card>
                            </Grid>
                        ))}
                    </Grid>
                )}
            </Box>

            <Modal open={open} onClose={() => setOpen(false)}>
                <Box sx={modalStyle} component="form" onSubmit={handleCreateList}>
                    <Typography variant="h6" component="h2" sx={{ mb: 2 }}>Yeni Liste Oluştur</Typography>
                    <TextField autoFocus required margin="dense" label="Liste Adı" fullWidth variant="outlined" value={newListName} onChange={(e) => setNewListName(e.target.value)} />
                    <TextField margin="dense" label="Açıklama (Opsiyonel)" fullWidth multiline rows={3} variant="outlined" value={newListDesc} onChange={(e) => setNewListDesc(e.target.value)} />
                    <Button type="submit" variant="contained" sx={{ mt: 2 }}>Oluştur</Button>
                </Box>
            </Modal>
        </>
    );
};