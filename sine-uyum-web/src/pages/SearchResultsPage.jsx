import React, { useState, useEffect } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, Grid, Card, CardContent, Avatar, Button, Chip } from '@mui/material';

const UserResultCard = ({ user }) => {
    const getChipColor = (score) => {
        if (score > 75) return "success";
        if (score > 50) return "warning";
        return "default";
    };

    return (
        <Grid item xs={12} sm={6} md={4}>
            <Card sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                <CardContent sx={{ flexGrow: 1, textAlign: 'center' }}>
                    <Avatar
                        src={user.profileImageUrl}
                        sx={{ width: 80, height: 80, margin: '0 auto 16px', fontSize: '2.5rem' }}
                    >
                        {user.userName.charAt(0).toUpperCase()}
                    </Avatar>
                    <Typography variant="h6" component="h3">{user.userName}</Typography>
                    
                    {user.compatibilityScore > 0 && (
                        <Chip 
                            label={`Uyum: %${user.compatibilityScore}`} 
                            color={getChipColor(user.compatibilityScore)}
                            size="small"
                            sx={{ my: 1 }} 
                        />
                    )}

                    <Typography variant="body2" color="text.secondary" sx={{ mt: 1, minHeight: '40px' }}>
                        {user.bio ? `"${user.bio.substring(0, 70)}..."` : "Kullanıcının bir biyografisi yok."}
                    </Typography>
                </CardContent>
                <Box sx={{ p: 2, pt: 0 }}>
                    <Button component={Link} to={`/profile/${user.id}`} variant="contained" fullWidth>
                        Profile Git
                    </Button>
                </Box>
            </Card>
        </Grid>
    );
};

export const SearchResultsPage = () => {
    const [searchParams] = useSearchParams();
    const query = searchParams.get('query');
    const { user } = useAuth();

    const [users, setUsers] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [message, setMessage] = useState('');

    useEffect(() => {
        const fetchUsers = async () => {
            if (!query || !user) return;

            setIsLoading(true);
            setMessage('');
            setUsers([]);

            try {
                const response = await api.get(`/api/account/search`, {
                    params: { query }
                });
                setUsers(response.data.$values || response.data);
                if ((response.data.$values || response.data).length === 0) {
                    setMessage(`'${query}' için sonuç bulunamadı.`);
                }
            } catch (error) {
                setMessage('Kullanıcılar aranırken bir hata oluştu.');
            } finally {
                setIsLoading(false);
            }
        };

        fetchUsers();
    }, [query, user]);

    return (
        <div className="page-container">
            <Typography variant="h4" component="h1" gutterBottom>
                Arama Sonuçları: "{query}"
            </Typography>

            {isLoading && <CircularProgress sx={{ my: 4 }} />}
            {message && <Typography sx={{ my: 4 }}>{message}</Typography>}
            
            <Grid container spacing={3}>
                {users.map(user => (
                    <UserResultCard key={user.id} user={user} />
                ))}
            </Grid>
        </div>
    );
};
