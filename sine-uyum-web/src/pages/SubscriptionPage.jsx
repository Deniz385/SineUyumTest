// sine-uyum-web/src/pages/SubscriptionPage.jsx
import React from 'react';
import { Box, Typography, Button, Paper, List, ListItem, ListItemIcon, ListItemText } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';


const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const SubscriptionPage = () => {
    const navigate = useNavigate();
    const { token, user } = useAuth();
    // Bu fonksiyon, ödeme sistemi entegre edildiğinde kullanılacak.
    // Şimdilik, veritabanını manuel olarak güncelleyeceğiz.
    const handleSubscribeClick = async () => {
        alert('Bu özellik yakında ödeme sistemi entegrasyonu ile tamamlanacaktır. Şimdilik, test etmek için veritabanından IsSubscribed alanını manuel olarak güncelleyebilirsiniz.');

        // Örnek: Gerçek bir API çağrısı şöyle görünebilirdi:
        /*
        try {
            await axios.post(`${API_URL}/api/subscription/subscribe`, {}, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            // Başarılı olursa kullanıcıyı bilgilendir ve yönlendir.
            alert('Aboneliğiniz başarıyla başlatıldı!');
            // AuthContext'i güncelleyip yönlendirme yapmak gerekebilir.
            navigate('/my-event'); 
        } catch (error) {
            alert('Abonelik sırasında bir hata oluştu.');
        }
        */
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
                >
                    Aylık 99.99 TL'ye Abone Ol
                </Button>
            </Paper>
        </Box>
    );
};