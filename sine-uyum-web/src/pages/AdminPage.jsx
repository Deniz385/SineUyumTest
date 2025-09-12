import React, { useState, useEffect, useCallback } from 'react';
import { Box, Typography, TextField, Button, CircularProgress, Paper, Grid, List, ListItem, ListItemText, Divider, IconButton, Modal } from '@mui/material';
import GroupAddIcon from '@mui/icons-material/GroupAdd';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { AlertMessage } from '../components/AlertMessage';
import api from '../api/axiosConfig';

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
    const [message, setMessage] = useState({ type: '', text: '' });
    const [editingEvent, setEditingEvent] = useState(null);

    const fetchEvents = useCallback(async () => {
        setIsLoading(true);
        try {
            const response = await api.get('/api/event');
            setEvents(response.data);
        } catch (error) {
            setMessage({ type: 'error', text: 'Etkinlikler yüklenemedi.' });
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        fetchEvents();
    }, [fetchEvents]);

    const handleCreateEvent = async (formData) => {
        setIsSubmitting(true);
        setMessage({ type: '', text: '' });
        try {
            await api.post('/api/event', formData);
            setMessage({ type: 'success', text: 'Etkinlik başarıyla oluşturuldu!' });
            fetchEvents();
        } catch (error) {
            setMessage({ type: 'error', text: error.response?.data?.message || 'Etkinlik oluşturulurken bir hata oluştu.' });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleUpdateEvent = async (formData) => {
        if (!editingEvent) return;
        setIsSubmitting(true);
        setMessage({ type: '', text: '' });
        try {
            await api.put(`/api/event/${editingEvent.id}`, formData);
            setMessage({ type: 'success', text: 'Etkinlik başarıyla güncellendi!' });
            setEditingEvent(null);
            fetchEvents();
        } catch (error) {
            setMessage({ type: 'error', text: error.response?.data?.message || 'Etkinlik güncellenirken bir hata oluştu.' });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleDeleteEvent = async (eventId) => {
        if (!window.confirm("Bu etkinliği ve tüm katılımcılarını kalıcı olarak silmek istediğinizden emin misiniz?")) return;
        
        setMessage({ type: 'info', text: 'Etkinlik siliniyor...' });
        try {
            await api.delete(`/api/event/${eventId}`);
            setMessage({ type: 'success', text: 'Etkinlik başarıyla silindi.' });
            fetchEvents();
        } catch (error) {
            setMessage({ type: 'error', text: error.response?.data?.message || 'Etkinlik silinirken bir hata oluştu.' });
        }
    };
    
    // --- DÜZELTME BU FONKSİYONDA ---
    const handleTriggerMatch = async (eventId) => {
        console.log("1. Eşleştirme fonksiyonu tetiklendi. Etkinlik ID:", eventId); // DEBUG

        const confirmation = window.confirm("Bu etkinlik için katılımcıları eşleştirmek istediğinizden emin misiniz? Bu işlem geri alınamaz.");
        
        console.log("2. Onay kutusu sonucu:", confirmation); // DEBUG

        if (!confirmation) {
            console.log("3. İşlem kullanıcı tarafından iptal edildi."); // DEBUG
            return;
        }
        
        setIsSubmitting(true); // isMatching yerine isSubmitting kullanabiliriz
        setMessage({ type: 'info', text: 'Eşleştirme işlemi başlatılıyor...' });
        console.log("4. API isteği gönderiliyor..."); // DEBUG

        try {
            const response = await api.post(`/api/admin/events/${eventId}/create-groups`);
            console.log("5. API'den başarılı yanıt alındı:", response.data); // DEBUG
            setMessage({ type: 'success', text: response.data.message });
        } catch (error) {
            console.error("5. API'den hata alındı:", error.response); // DEBUG
            const errorMessage = error.response?.data?.message || 'Eşleştirme sırasında bilinmeyen bir hata oluştu.';
            setMessage({ type: 'error', text: errorMessage });
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
                {message.text && <AlertMessage type={message.type} message={message.text} />}
                
                <Grid container spacing={4}>
                    <Grid item xs={12} md={4}>
                        <EventForm onFormSubmit={handleCreateEvent} isLoading={isSubmitting} />
                    </Grid>
                    <Grid item xs={12} md={8}>
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
        </>
    );
};