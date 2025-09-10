// sine-uyum-web/src/pages/SubscriptionPage.jsx
import React, { useState } from 'react';
import { Box, Typography, Button, Paper, List, ListItem, ListItemIcon, ListItemText, CircularProgress } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import api from '../api/axiosConfig'; // Merkezi axios instance'ımızı kullanalım

export const SubscriptionPage = () => {
    const navigate = useNavigate();
    const { logoutAction } = useAuth(); // logoutAction'ı alıyoruz
    const [isLoading, setIsLoading] = useState(false);

    const handleSubscribeClick = async () => {
        setIsLoading(true);
        try {
            // Yeni oluşturduğumuz endpoint'e istek atıyoruz
            await api.post('/api/subscription/activate');
            
            alert('Aboneliğiniz başarıyla aktifleştirildi! Güncel durumun yansıması için yeniden giriş yapmanız gerekiyor.');

            // Kullanıcıyı sistemden çıkarıp login sayfasına yönlendiriyoruz
            // Böylece yeniden girdiğinde token'ı güncellenmiş ve abone durumu 'true' olacak.
            logoutAction();

        } catch (error) {
            alert('Abonelik sırasında bir hata oluştu.');
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