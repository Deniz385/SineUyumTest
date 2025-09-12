import React, { useState } from 'react';
import { Box, Typography, Button, Paper, List, ListItem, ListItemIcon, ListItemText, CircularProgress } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { useAuth } from '../context/AuthContext';
import api from '../api/axiosConfig';
import { useSnackbar } from '../context/SnackbarProvider'; // <-- SNACKBAR KANCASINI İÇE AKTAR

export const SubscriptionPage = () => {
    const { loginAction } = useAuth(); 
    const [isLoading, setIsLoading] = useState(false);
    const { showSnackbar } = useSnackbar(); // <-- SNACKBAR FONKSİYONUNU AL

    const handleSubscribeClick = async () => {
        setIsLoading(true);
        try {
            const response = await api.post('/api/subscription/activate');
            const newToken = response.data.token;
            loginAction(newToken);
            // --- DEĞİŞİKLİK: alert() yerine showSnackbar() ---
            showSnackbar('Aboneliğiniz başarıyla aktifleştirildi!', 'success');
        } catch (error) {
            // --- DEĞİŞİKLİK: alert() yerine showSnackbar() ---
            showSnackbar('Abonelik sırasında bir hata oluştu.', 'error');
            setIsLoading(false);
        }
    };

    return (
        <Box className="page-container" sx={{ maxWidth: '700px' }}>
            <Paper elevation={3} sx={{ p: 4, textAlign: 'center' }}>
                <Typography variant="h4" component="h1" gutterBottom>
                    SineUyum Premium'a Geçin
                </Typography>
                <Typography variant="h6" color="text.secondary" sx={{ mb: 3 }}>
                    Film zevkine en uygun kişilerle tanış ve açık hava sineması etkinliklerine katıl!
                </Typography>
                <List sx={{ textAlign: 'left', display: 'inline-block', mb: 3 }}>
                    <ListItem>
                        <ListItemIcon><CheckCircleIcon color="success" /></ListItemIcon>
                        <ListItemText primary="Otomatik Grup Eşleştirmeleri" />
                    </ListItem>
                    <ListItem>
                        <ListItemIcon><CheckCircleIcon color="success" /></ListItemIcon>
                        <ListItemText primary="Grubunla Birlikte Film Oylaması" />
                    </ListItem>
                    <ListItem>
                        <ListItemIcon><CheckCircleIcon color="success" /></ListItemIcon>
                        <ListItemText primary="Özel Açık Hava Sineması Etkinlikleri" />
                    </ListItem>
                </List>
                <Button 
                    variant="contained" 
                    size="large" 
                    onClick={handleSubscribeClick}
                    disabled={isLoading}
                >
                    {isLoading ? <CircularProgress size={24} /> : "Aboneliği Test Et"}
                </Button>
            </Paper>
        </Box>
    );
};

