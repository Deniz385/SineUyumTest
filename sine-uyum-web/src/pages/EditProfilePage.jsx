// sine-uyum-web/src/pages/EditProfilePage.jsx
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, TextField, Button, Typography, CircularProgress } from '@mui/material';
import { AlertMessage } from '../components/AlertMessage';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const EditProfilePage = () => {
    const { token, user } = useAuth();
    const navigate = useNavigate();
    const [formData, setFormData] = useState({
        bio: '',
        profileImageUrl: ''
    });
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState({ type: '', text: '' });

    useEffect(() => {
        const fetchProfileData = async () => {
            if (!token || !user) return;
            try {
                const response = await axios.get(`${API_URL}/api/profile/${user.id}`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                setFormData({
                    bio: response.data.bio || '',
                    profileImageUrl: response.data.profileImageUrl || ''
                });
            } catch (error) {
                setMessage({ type: 'error', text: 'Profil bilgileri yüklenemedi.' });
            } finally {
                setIsLoading(false);
            }
        };
        fetchProfileData();
    }, [token, user]);

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setIsSaving(true);
        setMessage({ type: '', text: '' });
        try {
            await axios.put(`${API_URL}/api/profile`, formData, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
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
        <Box className="page-container" component="form" onSubmit={handleSubmit}>
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