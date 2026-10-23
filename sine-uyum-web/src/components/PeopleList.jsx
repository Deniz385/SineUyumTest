import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box } from '@mui/material';
import GroupRoundedIcon from '@mui/icons-material/GroupRounded';
import { FollowButton, UserRow } from './ui/UserRow';
import { UserListSkeleton } from './ui/Skeletons';
import { ErrorState } from './states/ErrorState';
import { EmptyState } from './states/EmptyState';

// A suggested person (GET /api/v1/people/suggestions item) with a follow button.
export const PersonSuggestionRow = ({ person }) => {
    const { t } = useTranslation();
    const parts = [];
    if (person.commonMovieCount) parts.push(t('people.commonFilms', { count: person.commonMovieCount }));
    parts.push(t('people.followers', { count: person.followerCount ?? 0 }));
    return (
        <UserRow
            user={person.user}
            compatibility={person.compatibility}
            subtitle={parts.join(' · ')}
            detail={person.bio}
            action={<FollowButton user={person.user} />}
        />
    );
};

export const PeopleList = ({ status, items, reload }) => {
    const { t } = useTranslation();
    if (status === 'loading') return <UserListSkeleton />;
    if (status === 'error') return <ErrorState compact message={t('people.loadError')} onRetry={reload} />;
    if (!items.length) return <EmptyState compact icon={GroupRoundedIcon} title={t('people.emptyTitle')} description={t('people.emptyBody')} />;
    return (
        <Box component="ul" data-testid="people-list" sx={{ listStyle: 'none', m: 0, p: 0, '& > li + li': { borderTop: 1, borderColor: 'divider' } }}>
            {items.map((person) => <li key={person.user.id}><PersonSuggestionRow person={person} /></li>)}
        </Box>
    );
};

