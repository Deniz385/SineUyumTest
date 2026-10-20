import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Box, Typography, Button, Paper, List, ListItem, ListItemIcon, ListItemText, CircularProgress } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { useAuth } from '../context/AuthContext';
import api from '../api/axiosConfig';

const formatDate = (iso) => new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });

// Abonelik durumu sunucudan okunur (tek kaynak). Ödemesiz "aboneliği test et"
// düğmesi kaldırıldı: abonelik yalnızca doğrulanmış ödeme ya da yönetici
// tanımlamasıyla açılır.
export const SubscriptionPage = () => {
    const { user, refreshSession } = useAuth();
    const [status, setStatus] = useState(null);
    const [error, setError] = useState('');

    const load = useCallback(async () => {
        try {
            const response = await api.get('/api/v1/subscription');
            setStatus(response.data);
            // Sunucudaki durum token'dakinden farklıysa (ör. yönetici az önce
            // tanımladı) token tazelenir ki etkinlik sayfası hemen açılsın.
            if (user && response.data.isActive !== user.isSubscribed) {
                await refreshSession();
            }
        } catch {
            setError('Abonelik bilgisi alınamadı.');
        }
    }, [user, refreshSession]);

    useEffect(() => {
        load();
    }, [load]);

    return (
        <Box className="page-container" sx={{ maxWidth: '700px' }}>
            <Paper elevation={3} sx={{ p: 4, textAlign: 'center' }}>
                <Typography variant="h4" component="h1" gutterBottom>
                    SineUyum Premium
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

                {error && <Alert severity="error">{error}</Alert>}
                {!status && !error && <CircularProgress />}

                {status?.isActive && (
                    <>
                        <Alert severity="success" sx={{ textAlign: 'left', mb: 2 }}>
                            Aboneliğin aktif — {status.willRenew ? 'yenilenme' : 'bitiş'} tarihi: {formatDate(status.expiresAt)}
                        </Alert>
                        <Button component={Link} to="/my-event" variant="contained" size="large">
                            Etkinliklere git
                        </Button>
                    </>
                )}

                {status && !status.isActive && (
                    <Alert severity="info" sx={{ textAlign: 'left' }}>
                        {status.availableProviders.length === 0
                            ? 'Online ödeme çok yakında. Şu an abonelik satın alınamıyor.'
                            : 'Ödeme seçenekleri hazırlanıyor.'}
                    </Alert>
                )}
            </Paper>
        </Box>
    );
};
