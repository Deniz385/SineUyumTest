import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Avatar, Box, TextField, Button, Typography, CircularProgress, Stack } from '@mui/material';
import { useSnackbar } from '../context/SnackbarProvider';
import { PrivacySettings } from '../components/PrivacySettings';
import { LanguageSwitcher } from '../components/LanguageSwitcher';

// Shrinks large photos in the browser before uploading (the server limit is
// 10 MB and the server scales down to 512 px anyway, so sending more wastes
// bandwidth). EXIF orientation is applied. If the browser cannot decode the
// file (e.g. HEIC) it is sent as is; the server returns a clear error if it
// does not support it either.
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
        return blob ? new File([blob], 'photo.jpg', { type: 'image/jpeg' }) : file;
    } catch {
        return file;
    }
};

const uploadErrorMessage = (error, t) => {
    const status = error.response?.status;
    if (status === 413) return t('editProfile.photoTooLarge');
    if (status === 415) return t('editProfile.photoUnsupported');
    if (status === 429) return t('editProfile.tooManyAttempts');
    return error.response?.data?.title || t('editProfile.photoUploadFailed');
};

export const EditProfilePage = () => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar();
    const fileInput = useRef(null);
    const [formData, setFormData] = useState({
        bio: '',
        // The server only accepts the current value (unchanged) or empty
        // (remove); photos are uploaded through a separate endpoint. Saving the
        // bio sends this value back unchanged.
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
            showSnackbar(t('editProfile.loadFailed'), 'error');
        } finally {
            setIsLoading(false);
        }
    }, [user, showSnackbar, t]);

    useEffect(() => {
        fetchProfileData();
    }, [fetchProfileData]);

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleFileSelected = async (e) => {
        const selected = e.target.files?.[0];
        e.target.value = ''; // allow picking the same file again
        if (!selected) return;

        setIsUploading(true);
        try {
            const body = new FormData();
            body.append('file', await downscaleForUpload(selected));
            const response = await api.post('/api/v1/profile/photo', body);
            setFormData((current) => ({ ...current, profileImageUrl: response.data.profileImageUrl || '' }));
            showSnackbar(t('editProfile.photoUpdated'), 'success');
        } catch (error) {
            showSnackbar(uploadErrorMessage(error, t), 'error');
        } finally {
            setIsUploading(false);
        }
    };

    const handleRemovePhoto = async () => {
        setIsUploading(true);
        try {
            await api.delete('/api/v1/profile/photo');
            setFormData((current) => ({ ...current, profileImageUrl: '' }));
            showSnackbar(t('editProfile.photoRemoved'), 'success');
        } catch {
            showSnackbar(t('editProfile.photoRemoveFailed'), 'error');
        } finally {
            setIsUploading(false);
        }
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setIsSaving(true);
        try {
            await api.put(`/api/v1/profile`, formData);
            showSnackbar(t('editProfile.saved'), 'success');
            setTimeout(() => navigate(`/profile/${user.id}`), 1500);
        } catch {
            showSnackbar(t('editProfile.saveFailed'), 'error');
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
                    {t('editProfile.title')}
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
                            {isUploading ? <CircularProgress size={20} /> : t('editProfile.uploadPhoto')}
                        </Button>
                        {formData.profileImageUrl && (
                            <Button color="error" onClick={handleRemovePhoto} disabled={isUploading}>
                                {t('editProfile.removePhoto')}
                            </Button>
                        )}
                        <Typography variant="caption" color="text.secondary">
                            {t('editProfile.photoHint')}
                        </Typography>
                    </Stack>
                </Stack>

                <TextField
                    label={t('editProfile.bio')}
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
                    {isSaving ? <CircularProgress size={24} /> : t('common.save')}
                </Button>
            </Box>
            {/* The language applies (and is saved) immediately; it is not part of the form. */}
            <LanguageSwitcher variant="select" sx={{ mt: 4, textAlign: 'left' }} />
            {/* Privacy settings save themselves; they are not part of the form. */}
            <PrivacySettings />
        </Box>
    );
};
