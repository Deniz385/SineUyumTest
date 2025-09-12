import React, { useState, useEffect, useCallback } from 'react';
import { Box, Typography, TextField, Button, CircularProgress, Paper, Grid, List, ListItem, ListItemText, Divider, IconButton, Modal } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import api from '../api/axiosConfig';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { useSnackbar } from '../context/SnackbarProvider'; // <-- SNACKBAR KANCASINI İÇE AKTAR

const EventForm = ({ onFormSubmit, initialData, onCancel, isLoading }) => {
    const [eventData, setEventData] = useState({
        locationName: initialData?.locationName || '',
        address: initialData?.address || '',
        eventDate: initialData?.eventDate ? new Date(new Date(initialData.eventDate).getTime() - (new Date().getTimezoneOffset() * 60000)).toISOString().slice(0, 16) : '',
        groupSize: initialData?.groupSize || 4,
    });

    const handleChange = (e) => {
        setEventData({ ...eventData, [e.target.name]: e.target.value });
    };

    const handleSubmit = (e) => {
        e.preventDefault();
        onFormSubmit({
            ...eventData,
            eventDate: new Date(eventData.eventDate).toISOString(),
            groupSize: parseInt(eventData.groupSize, 10)
        });
    };

    return (
        <Paper component="form" onSubmit={handleSubmit} sx={{ p: 3 }}>
            <Typography variant="h5" component="h2" gutterBottom>
                {initialData ? 'Etkinliği Düzenle' : 'Yeni Etkinlik Oluştur'}
            </Typography>
            <TextField label="Etkinlik Adı / Mekan" name="locationName" value={eventData.locationName} onChange={handleChange} fullWidth required margin="normal" />
            <TextField label="Adres" name="address" value={eventData.address} onChange={handleChange} fullWidth margin="normal" />
            <TextField label="Etkinlik Tarihi ve Saati" name="eventDate" type="datetime-local" value={eventData.eventDate} onChange={handleChange} fullWidth required margin="normal" InputLabelProps={{ shrink: true }} />
            <TextField label="Grup Büyüklüğü" name="groupSize" type="number" value={eventData.groupSize} onChange={handleChange} fullWidth required margin="normal" InputProps={{ inputProps: { min: 2, max: 10 } }} />
            <Box sx={{ mt: 2, display: 'flex', gap: 1 }}>
                 <Button type="submit" variant="contained" disabled={isLoading}>
                    {isLoading ? <CircularProgress size={24} /> : (initialData ? 'Güncelle' : 'Oluştur')}
                </Button>
                {onCancel && <Button variant="outlined" onClick={onCancel}>İptal</Button>}
            </Box>
        </Paper>
    );
};

export const AdminPage = () => {
    const [events, setEvents] = useState([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [editingEvent, setEditingEvent] = useState(null);
    const [dialog, setDialog] = useState({ isOpen: false, title: '', description: '', onConfirm: null });
    const { showSnackbar } = useSnackbar(); // <-- SNACKBAR FONKSİYONUNU AL

    const fetchEvents = useCallback(async () => {
        setIsLoading(true);
        try {
            const response = await api.get('/api/event');
            setEvents(response.data.$values || response.data);
        } catch (error) {
            showSnackbar('Etkinlikler yüklenemedi.', 'error');
        } finally {
            setIsLoading(false);
        }
    }, [showSnackbar]);

    useEffect(() => {
        fetchEvents();
    }, [fetchEvents]);

    const handleCreateEvent = async (formData) => {
        setIsSubmitting(true);
        try {
            await api.post('/api/event', formData);
            showSnackbar('Etkinlik başarıyla oluşturuldu!', 'success');
            fetchEvents();
        } catch (error) {
            showSnackbar(error.response?.data?.message || 'Etkinlik oluşturulurken bir hata oluştu.', 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleDeleteEvent = (eventId) => {
        setDialog({
            isOpen: true,
            title: 'Etkinliği Sil?',
            description: 'Bu etkinliği ve tüm katılımcılarını kalıcı olarak silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.',
            onConfirm: () => {
                setDialog({ isOpen: false });
                performDelete(eventId);
            }
        });
    };

    const performDelete = async (eventId) => {
        showSnackbar('Etkinlik siliniyor...', 'info');
        try {
            await api.delete(`/api/event/${eventId}`);
            showSnackbar('Etkinlik başarıyla silindi.', 'success');
            fetchEvents();
        } catch (error) {
            showSnackbar(error.response?.data?.message || 'Etkinlik silinirken bir hata oluştu.', 'error');
        }
    };
    
    const handleTriggerMatch = (eventId) => {
        setDialog({
            isOpen: true,
            title: 'Eşleştirmeyi Başlat?',
            description: 'Bu etkinlik için katılımcıları eşleştirmek istediğinizden emin misiniz? Bu işlem geri alınamaz.',
            onConfirm: () => {
                setDialog({ isOpen: false });
                performMatch(eventId);
            }
        });
    };

    const performMatch = async (eventId) => {
        setIsSubmitting(true);
        showSnackbar('Eşleştirme işlemi başlatılıyor...', 'info');
        try {
            const response = await api.post(`/api/admin/events/${eventId}/create-groups`);
            showSnackbar(response.data.message, 'success');
        } catch (error) {
            const errorMessage = error.response?.data?.message || 'Eşleştirme sırasında bilinmeyen bir hata oluştu.';
            showSnackbar(errorMessage, 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleUpdateEvent = async (formData) => {
        if (!editingEvent) return;
        setIsSubmitting(true);
        try {
            await api.put(`/api/event/${editingEvent.id}`, formData);
            showSnackbar('Etkinlik başarıyla güncellendi!', 'success');
            setEditingEvent(null);
            fetchEvents();
        } catch (error) {
            showSnackbar(error.response?.data?.message || 'Etkinlik güncellenirken bir hata oluştu.', 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <>
            <Box className="page-container" sx={{ maxWidth: '1000px', textAlign: 'left' }}>
                <Typography variant="h4" component="h1" gutterBottom>
                    Yönetici Paneli
                </Typography>
                
                <Grid container spacing={4}>
                    <Grid xs={12} md={4}>
                        <EventForm onFormSubmit={handleCreateEvent} isLoading={isSubmitting} />
                    </Grid>
                    <Grid xs={12} md={8}>
                        <Paper sx={{ p: 2 }}>
                            <Typography variant="h5" component="h2" gutterBottom>Mevcut Etkinlikler</Typography>
                            {isLoading ? <CircularProgress /> : (
                                <List>
                                    {events.map((event, index) => (
                                        <React.Fragment key={event.id}>
                                            <ListItem>
                                                <ListItemText
                                                    primary={event.locationName}
                                                    secondary={`Tarih: ${new Date(event.eventDate).toLocaleString('tr-TR')} - Katılımcı: ${event.participantCount} - Grup: ${event.groupSize} kişi`}
                                                />
                                                <Box sx={{ display: 'flex', gap: 0.5 }}>
                                                    <Button size="small" variant="outlined" onClick={() => handleTriggerMatch(event.id)} disabled={isSubmitting}>Eşleştir</Button>
                                                    <IconButton onClick={() => setEditingEvent(event)} color="primary" disabled={isSubmitting}><EditIcon /></IconButton>
                                                    <IconButton onClick={() => handleDeleteEvent(event.id)} color="error" disabled={isSubmitting}><DeleteIcon /></IconButton>
                                                </Box>
                                            </ListItem>
                                            {index < events.length - 1 && <Divider />}
                                        </React.Fragment>
                                    ))}
                                </List>
                            )}
                        </Paper>
                    </Grid>
                </Grid>
            </Box>

            <Modal open={!!editingEvent} onClose={() => setEditingEvent(null)}>
                 <Box sx={{ position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)', width: 400 }}>
                    <EventForm
                        initialData={editingEvent}
                        onFormSubmit={handleUpdateEvent}
                        onCancel={() => setEditingEvent(null)}
                        isLoading={isSubmitting}
                    />
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
