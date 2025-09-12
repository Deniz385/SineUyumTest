import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, TextField, Button, Typography, CircularProgress } from '@mui/material';
import { useSnackbar } from '../context/SnackbarProvider'; // <-- SNACKBAR KANCASINI İÇE AKTAR

export const EditProfilePage = () => {
    const { user } = useAuth();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar(); // <-- SNACKBAR FONKSİYONUNU AL
    const [formData, setFormData] = useState({
        bio: '',
        profileImageUrl: ''
    });
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);

    const fetchProfileData = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get(`/api/profile/${user.id}`);
            const profile = response.data.$values ? response.data.$values[0] : response.data; // $values kontrolü
            setFormData({
                bio: profile.bio || '',
                profileImageUrl: profile.profileImageUrl || ''
            });
        } catch (error) {
            showSnackbar('Profil bilgileri yüklenemedi.', 'error');
        } finally {
            setIsLoading(false);
        }
    }, [user, showSnackbar]);

    useEffect(() => {
        fetchProfileData();
    }, [fetchProfileData]);

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setIsSaving(true);
        try {
            await api.put(`/api/profile`, formData);
            showSnackbar('Profil başarıyla güncellendi!', 'success');
            setTimeout(() => navigate(`/profile/${user.id}`), 1500);
        } catch (error) {
            showSnackbar('Profil güncellenirken bir hata oluştu.', 'error');
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

