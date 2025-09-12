import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, TextField, Button, Typography, CircularProgress } from '@mui/material';
import { AlertMessage } from '../components/AlertMessage';

export const EditProfilePage = () => {
    const { user } = useAuth(); // token kaldırıldı, sadece user kullanılıyor
    const navigate = useNavigate();
    const [formData, setFormData] = useState({
        bio: '',
        profileImageUrl: ''
    });
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState({ type: '', text: '' });

    const fetchProfileData = useCallback(async () => {
        if (!user) return; // user kontrolü yapılıyor
        try {
            // api kullanılıyor ve header kaldırıldı
            const response = await api.get(`/api/profile/${user.id}`);
            setFormData({
                bio: response.data.bio || '',
                profileImageUrl: response.data.profileImageUrl || ''
            });
        } catch (error) {
            setMessage({ type: 'error', text: 'Profil bilgileri yüklenemedi.' });
        } finally {
            setIsLoading(false);
        }
    }, [user]);

    useEffect(() => {
        fetchProfileData();
    }, [fetchProfileData]);

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setIsSaving(true);
        setMessage({ type: '', text: '' });
        try {
            // api kullanılıyor ve header kaldırıldı
            await api.put(`/api/profile`, formData);
            setMessage({ type: 'success', text: 'Profil başarıyla güncellendi! Yönlendiriliyorsunuz...' });
            setTimeout(() => navigate(`/profile/${user.id}`), 2000);
        } catch (error) {
            setMessage({ type: 'error', text: 'Profil güncellenirken bir hata oluştu.' });
            setIsSaving(false);
        }
    };

    if (isLoading) {
        return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;
    }

    return (
        <Box className="page-container" component="form" onSubmit={handleSubmit} sx={{maxWidth: '700px'}}>
            <Typography variant="h4" component="h1" gutterBottom>
                Profili Düzenle
            </Typography>
            {message.text && <AlertMessage type={message.type} message={message.text} />}
            <TextField
                label="Hakkında (Bio)"
                name="bio"
                multiline
                rows={4}
                value={formData.bio}
                onChange={handleChange}
                fullWidth
                margin="normal"
            />
            <TextField
                label="Profil Fotoğrafı URL'si"
                name="profileImageUrl"
                value={formData.profileImageUrl}
                onChange={handleChange}
                fullWidth
                margin="normal"
                placeholder="https://example.com/resim.jpg"
            />
            <Button
                type="submit"
                variant="contained"
                sx={{ mt: 3 }}
                disabled={isSaving}
            >
                {isSaving ? <CircularProgress size={24} /> : 'Kaydet'}
            </Button>
        </Box>
    );
};