import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Typography, TextField, Button, CircularProgress, Paper, Grid, List, ListItem, ListItemText, Divider, IconButton, Modal } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import EventBusyIcon from '@mui/icons-material/EventBusy';
import api from '../api/axiosConfig';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { useSnackbar } from '../context/SnackbarProvider';

const EventForm = ({ onFormSubmit, initialData, onCancel, isLoading }) => {
    const { t } = useTranslation();
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
                {initialData ? t('admin.editEvent') : t('admin.newEvent')}
            </Typography>
            <TextField label={t('admin.locationName')} name="locationName" value={eventData.locationName} onChange={handleChange} fullWidth required margin="normal" />
            <TextField label={t('admin.address')} name="address" value={eventData.address} onChange={handleChange} fullWidth margin="normal" />
            <TextField label={t('admin.eventDate')} name="eventDate" type="datetime-local" value={eventData.eventDate} onChange={handleChange} fullWidth required margin="normal" InputLabelProps={{ shrink: true }} />
            <TextField label={t('admin.groupSize')} name="groupSize" type="number" value={eventData.groupSize} onChange={handleChange} fullWidth required margin="normal" InputProps={{ inputProps: { min: 2, max: 10 } }} />
            <Box sx={{ mt: 2, display: 'flex', gap: 1 }}>
                 <Button type="submit" variant="contained" disabled={isLoading}>
                    {isLoading ? <CircularProgress size={24} /> : (initialData ? t('common.update') : t('common.create'))}
                </Button>
                {onCancel && <Button variant="outlined" onClick={onCancel}>{t('common.cancel')}</Button>}
            </Box>
        </Paper>
    );
};

export const AdminPage = () => {
    const { t, i18n } = useTranslation();
    const [events, setEvents] = useState([]);
    const [isLoading, setIsLoading] = useState(true);
    const [loadError, setLoadError] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [editingEvent, setEditingEvent] = useState(null);
    const [dialog, setDialog] = useState({ isOpen: false, title: '', description: '', onConfirm: null });
    const { showSnackbar } = useSnackbar();

    const fetchEvents = useCallback(async () => {
        setIsLoading(true);
        setLoadError(false);
        try {
            const response = await api.get('/api/v1/event');
            setEvents(response.data.$values || response.data);
        } catch {
            setLoadError(true);
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        fetchEvents();
    }, [fetchEvents]);

    const handleCreateEvent = async (formData) => {
        setIsSubmitting(true);
        try {
            await api.post('/api/v1/event', formData);
            showSnackbar(t('admin.created'), 'success');
            fetchEvents();
        } catch (error) {
            showSnackbar(error.response?.data?.message || t('admin.createFailed'), 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleDeleteEvent = (eventId) => {
        setDialog({
            isOpen: true,
            title: t('admin.deleteTitle'),
            description: t('admin.deleteBody'),
            onConfirm: () => {
                setDialog({ isOpen: false });
                performDelete(eventId);
            }
        });
    };

    const performDelete = async (eventId) => {
        showSnackbar(t('admin.deleting'), 'info');
        try {
            await api.delete(`/api/v1/event/${eventId}`);
            showSnackbar(t('admin.deleted'), 'success');
            fetchEvents();
        } catch (error) {
            showSnackbar(error.response?.data?.message || t('admin.deleteFailed'), 'error');
        }
    };
    
    const handleTriggerMatch = (eventId) => {
        setDialog({
            isOpen: true,
            title: t('admin.matchTitle'),
            description: t('admin.matchBody'),
            onConfirm: () => {
                setDialog({ isOpen: false });
                performMatch(eventId);
            }
        });
    };

    const performMatch = async (eventId) => {
        setIsSubmitting(true);
        showSnackbar(t('admin.matchStarting'), 'info');
        try {
            // The server queues the job and returns at once (202); poll until it finishes.
            let run = (await api.post(`/api/v1/admin/events/${eventId}/create-groups`)).data;
            const deadline = Date.now() + 2 * 60 * 1000;
            while ((run.status === 'Queued' || run.status === 'Running') && Date.now() < deadline) {
                await new Promise((resolve) => setTimeout(resolve, 1500));
                run = (await api.get(`/api/v1/admin/events/${eventId}/matching`)).data;
            }
            if (run.status === 'Succeeded') {
                showSnackbar(run.message, 'success');
            } else if (run.status === 'Failed') {
                showSnackbar(run.error || run.message, 'error');
            } else {
                showSnackbar(t('admin.matchInProgress'), 'info');
            }
        } catch (error) {
            const errorMessage = error.response?.data?.title || error.response?.data?.message || t('admin.matchFailed');
            showSnackbar(errorMessage, 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleUpdateEvent = async (formData) => {
        if (!editingEvent) return;
        setIsSubmitting(true);
        try {
            await api.put(`/api/v1/event/${editingEvent.id}`, formData);
            showSnackbar(t('admin.updated'), 'success');
            setEditingEvent(null);
            fetchEvents();
        } catch (error) {
            showSnackbar(error.response?.data?.message || t('admin.updateFailed'), 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <>
            <Box className="page-container" sx={{ maxWidth: '1000px', textAlign: 'left' }}>
                <Typography variant="h4" component="h1" gutterBottom>
                    {t('admin.title')}
                </Typography>
                
                <Grid container spacing={4}>
                    <Grid size={{ xs: 12, md: 4 }}>
                        <EventForm onFormSubmit={handleCreateEvent} isLoading={isSubmitting} />
                    </Grid>
                    <Grid size={{ xs: 12, md: 8 }}>
                        <Paper sx={{ p: 2 }}>
                            <Typography variant="h5" component="h2" gutterBottom>{t('admin.existingEvents')}</Typography>
                            {isLoading ? <LoadingState compact /> : loadError ? (
                                <ErrorState compact message={t('admin.loadFailed')} onRetry={fetchEvents} />
                            ) : events.length === 0 ? (
                                <EmptyState compact icon={EventBusyIcon} title={t('admin.noEvents')} />
                            ) : (
                                <List>
                                    {events.map((event, index) => (
                                        <React.Fragment key={event.id}>
                                            <ListItem>
                                                <ListItemText
                                                    primary={event.locationName}
                                                    secondary={t('admin.eventSummary', { date: new Date(event.eventDate).toLocaleString(i18n.language), participants: event.participantCount, groupSize: event.groupSize })}
                                                />
                                                <Box sx={{ display: 'flex', gap: 0.5 }}>
                                                    <Button size="small" variant="outlined" onClick={() => handleTriggerMatch(event.id)} disabled={isSubmitting}>{t('admin.match')}</Button>
                                                    <IconButton onClick={() => setEditingEvent(event)} color="primary" disabled={isSubmitting} aria-label={t('common.edit')}><EditIcon /></IconButton>
                                                    <IconButton onClick={() => handleDeleteEvent(event.id)} color="error" disabled={isSubmitting} aria-label={t('common.delete')}><DeleteIcon /></IconButton>
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
