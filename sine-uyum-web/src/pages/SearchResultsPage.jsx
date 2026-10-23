import React, { useState, useEffect, useCallback } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Button, Card } from '@mui/material';
import PersonSearchIcon from '@mui/icons-material/PersonSearchRounded';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { Page, PageHeader } from '../components/ui/Page';
import { UserRow } from '../components/ui/UserRow';
import { UserListSkeleton } from '../components/ui/Skeletons';
import { UserSearch } from '../components/UserSearch';

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
        <Page width="reading">
            <PageHeader title={t('search.title', { query })} />
            <UserSearch sx={{ mb: 3 }} />
            {isLoading ? (
                <UserListSkeleton />
            ) : error ? (
                <ErrorState message={t('search.error')} onRetry={fetchUsers} />
            ) : users?.length === 0 ? (
                <EmptyState icon={PersonSearchIcon} title={t('search.noResults', { query })} description={t('search.noResultsHint')} />
            ) : (
                <Card component="ul" data-testid="search-results" sx={{ listStyle: 'none', m: 0, px: 2, py: 0.5, '& > li + li': { borderTop: 1, borderColor: 'divider' } }}>
                    {(users ?? []).map((u) => (
                        <li key={u.id}>
                            <UserRow
                                user={u}
                                compatibility={u.compatibilityScore > 0 ? u.compatibilityScore : null}
                                detail={u.bio || t('search.noBio')}
                                action={<Button component={Link} to={`/profile/${u.id}`} variant="tonal" size="small">{t('search.goToProfile')}</Button>}
                            />
                        </li>
                    ))}
                </Card>
            )}
        </Page>
    );
};
