import React, { useState, useEffect, useCallback } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, Grid, Card, CardContent, Avatar, Button, Chip } from '@mui/material';
import PersonSearchIcon from '@mui/icons-material/PersonSearch';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';

const UserResultCard = ({ user }) => {
    const { t } = useTranslation();
    const getChipColor = (score) => {
        if (score > 75) return "success";
        if (score > 50) return "warning";
        return "default";
    };

    return (
        <Grid size={{ xs: 12, sm: 6, md: 4 }}>
            <Card sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                <CardContent sx={{ flexGrow: 1, textAlign: 'center' }}>
                    <Avatar
                        src={mediaUrl(user.profileImageUrl)}
                        sx={{ width: 80, height: 80, margin: '0 auto 16px', fontSize: '2.5rem' }}
                    >
                        {user.userName.charAt(0).toUpperCase()}
                    </Avatar>
                    <Typography variant="h6" component="h3">{user.userName}</Typography>
                    
                    {user.compatibilityScore > 0 && (
                        <Chip 
                            label={t('search.compatibility', { score: user.compatibilityScore })} 
                            color={getChipColor(user.compatibilityScore)}
                            size="small"
                            sx={{ my: 1 }} 
                        />
                    )}

                    <Typography variant="body2" color="text.secondary" sx={{ mt: 1, minHeight: '40px' }}>
                        {user.bio ? `"${user.bio.substring(0, 70)}..."` : t('search.noBio')}
                    </Typography>
                </CardContent>
                <Box sx={{ p: 2, pt: 0 }}>
                    <Button component={Link} to={`/profile/${user.id}`} variant="contained" fullWidth>
                        {t('search.goToProfile')}
                    </Button>
                </Box>
            </Card>
        </Grid>
    );
};

export const SearchResultsPage = () => {
    const { t } = useTranslation();
    const [searchParams] = useSearchParams();
    const query = searchParams.get('query');
    const { user } = useAuth();

    // null until the first search finishes.
    const [users, setUsers] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState(false);

    const fetchUsers = useCallback(async () => {
        if (!query || !user) return;

        setIsLoading(true);
        setError(false);
        setUsers(null);

        try {
            const response = await api.get(`/api/v1/account/search`, {
                params: { query }
            });
            setUsers(response.data.$values || response.data);
        } catch {
            setError(true);
        } finally {
            setIsLoading(false);
        }
    }, [query, user]);

    useEffect(() => {
        fetchUsers();
    }, [fetchUsers]);

    return (
        <div className="page-container">
            <Typography variant="h4" component="h1" gutterBottom>
                {t('search.title', { query })}
            </Typography>

            {isLoading ? (
                <LoadingState />
            ) : error ? (
                <ErrorState message={t('search.error')} onRetry={fetchUsers} />
            ) : users?.length === 0 ? (
                <EmptyState icon={PersonSearchIcon} title={t('search.noResults', { query })} description={t('search.noResultsHint')} />
            ) : (
                <Grid container spacing={3}>
                    {(users ?? []).map(user => (
                        <UserResultCard key={user.id} user={user} />
                    ))}
                </Grid>
            )}
        </div>
    );
};
