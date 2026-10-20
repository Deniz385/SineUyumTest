import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Avatar, Box, TextField, Button, Typography, CircularProgress, Stack } from '@mui/material';
import { useSnackbar } from '../context/SnackbarProvider';
import { PrivacySettings } from '../components/PrivacySettings';

// Büyük fotoğrafları yüklemeden önce tarayıcıda küçültür (sunucu sınırı 10 MB;
// sunucu zaten 512 px'e indiriyor, fazlasını göndermek boşa bant genişliği).
// EXIF yönü uygulanır. Tarayıcı çözemezse (ör. HEIC) dosya olduğu gibi gider;
// sunucu desteklemiyorsa anlaşılır bir hata döner.
const MAX_SIDE = 1600;
const downscaleForUpload = async (file) => {
    try {
        const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
        const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
        if (scale === 1 && file.size <= 2 * 1024 * 1024) return file;

        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bitmap.width * scale);
        canvas.height = Math.round(bitmap.height * scale);
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        const blob = await new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.9));
        return blob ? new File([blob], 'foto.jpg', { type: 'image/jpeg' }) : file;
    } catch {
        return file;
    }
};

const uploadErrorMessage = (error) => {
    const status = error.response?.status;
    if (status === 413) return 'Fotoğraf en fazla 10 MB olabilir.';
    if (status === 415) return 'Bu dosya desteklenmiyor. JPEG, PNG ya da WebP seçin.';
    if (status === 429) return 'Çok fazla deneme. Biraz sonra tekrar deneyin.';
    return error.response?.data?.title || 'Fotoğraf yüklenemedi.';
};

export const EditProfilePage = () => {
    const { user } = useAuth();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar();
    const fileInput = useRef(null);
    const [formData, setFormData] = useState({
        bio: '',
        // Sunucu yalnızca mevcut değeri (değişmedi) ya da boşu (kaldır) kabul
        // eder; fotoğraf ayrı uçla yüklenir. Bio kaydederken değer aynen döner.
        profileImageUrl: ''
    });
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isUploading, setIsUploading] = useState(false);

    const fetchProfileData = useCallback(async () => {
        if (!user) return;
        try {
            const response = await api.get(`/api/v1/profile/${user.id}`);
            const profile = response.data;
            setFormData({
                bio: profile.bio || '',
                profileImageUrl: profile.profileImageUrl || ''
            });
        } catch {
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

    const handleFileSelected = async (e) => {
        const selected = e.target.files?.[0];
        e.target.value = ''; // aynı dosya tekrar seçilebilsin
        if (!selected) return;

        setIsUploading(true);
        try {
            const body = new FormData();
            body.append('file', await downscaleForUpload(selected));
            const response = await api.post('/api/v1/profile/photo', body);
            setFormData((current) => ({ ...current, profileImageUrl: response.data.profileImageUrl || '' }));
            showSnackbar('Profil fotoğrafı güncellendi.', 'success');
        } catch (error) {
            showSnackbar(uploadErrorMessage(error), 'error');
        } finally {
            setIsUploading(false);
        }
    };

    const handleRemovePhoto = async () => {
        setIsUploading(true);
        try {
            await api.delete('/api/v1/profile/photo');
            setFormData((current) => ({ ...current, profileImageUrl: '' }));
            showSnackbar('Profil fotoğrafı kaldırıldı.', 'success');
        } catch {
            showSnackbar('Fotoğraf kaldırılamadı.', 'error');
        } finally {
            setIsUploading(false);
        }
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setIsSaving(true);
        try {
            await api.put(`/api/v1/profile`, formData);
            showSnackbar('Profil başarıyla güncellendi!', 'success');
            setTimeout(() => navigate(`/profile/${user.id}`), 1500);
        } catch {
            showSnackbar('Profil güncellenirken bir hata oluştu.', 'error');
            setIsSaving(false);
        }
    };

    if (isLoading) {
        return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;
    }

    return (
        <Box className="page-container" sx={{maxWidth: '700px'}}>
            <Box component="form" onSubmit={handleSubmit}>
                <Typography variant="h4" component="h1" gutterBottom>
                    Profili Düzenle
                </Typography>

                <Stack direction="row" spacing={3} alignItems="center" sx={{ my: 2 }}>
                    <Avatar src={mediaUrl(formData.profileImageUrl)} sx={{ width: 96, height: 96, fontSize: '2.5rem' }}>
                        {user?.userName?.charAt(0).toUpperCase()}
                    </Avatar>
                    <Stack spacing={1}>
                        <input
                            ref={fileInput}
                            type="file"
                            accept="image/jpeg,image/png,image/webp"
                            hidden
                            onChange={handleFileSelected}
                        />
                        <Button variant="outlined" onClick={() => fileInput.current?.click()} disabled={isUploading}>
                            {isUploading ? <CircularProgress size={20} /> : 'Fotoğraf yükle'}
                        </Button>
                        {formData.profileImageUrl && (
                            <Button color="error" onClick={handleRemovePhoto} disabled={isUploading}>
                                Fotoğrafı kaldır
                            </Button>
                        )}
                        <Typography variant="caption" color="text.secondary">
                            JPEG, PNG ya da WebP · en fazla 10 MB · kare kırpılır
                        </Typography>
                    </Stack>
                </Stack>

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
                <Button
                    type="submit"
                    variant="contained"
                    sx={{ mt: 3 }}
                    disabled={isSaving || isUploading}
                >
                    {isSaving ? <CircularProgress size={24} /> : 'Kaydet'}
                </Button>
            </Box>
            {/* Gizlilik ayarları kendi başına kaydedilir; formun parçası değil. */}
            <PrivacySettings />
        </Box>
    );
};
