import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { BottomNavigation, BottomNavigationAction, Paper } from '@mui/material';
import HomeRoundedIcon from '@mui/icons-material/HomeRounded';
import ExploreRoundedIcon from '@mui/icons-material/ExploreRounded';
import EventRoundedIcon from '@mui/icons-material/EventRounded';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';
import PersonRoundedIcon from '@mui/icons-material/PersonRounded';
import { useAuth } from '../context/AuthContext';
import { isNavActive } from './navigation';

// Below 900px: the bottom navigation mirroring the mobile app (Home,
// Discover, Event, Messages, Profile).
export const BottomNav = () => {
    const { t } = useTranslation();
    const { user } = useAuth();
    const { pathname } = useLocation();
    if (!user) return null;

    const items = [
        { value: 'home', to: '/home', label: t('nav.home'), icon: <HomeRoundedIcon />, match: ['/home', '/feed'] },
        { value: 'discover', to: '/discover', label: t('nav.discover'), icon: <ExploreRoundedIcon />, match: ['/discover', '/lists', '/search'] },
        { value: 'event', to: '/my-event', label: t('nav.event'), icon: <EventRoundedIcon />, match: ['/my-event', '/subscription'] },
        { value: 'messages', to: '/messages', label: t('nav.messages'), icon: <ChatBubbleRoundedIcon />, match: ['/messages'] },
        {
            value: 'profile', to: `/profile/${user.id}`, label: t('nav.profile'), icon: <PersonRoundedIcon />,
            match: [`/profile/${user.id}`, '/profile/edit', '/watchlist'],
        },
    ];
    const active = items.find((item) => isNavActive(item, pathname))?.value ?? false;

    return (
        <Paper
            component="nav"
            aria-label={t('nav.mainNav')}
            data-testid="bottom-nav"
            square
            sx={{
                display: { xs: 'block', md: 'none' },
                position: 'fixed', left: 0, right: 0, bottom: 0, zIndex: (theme) => theme.zIndex.appBar,
                borderTop: 1, borderColor: 'divider', bgcolor: 'background.paper',
                pb: 'env(safe-area-inset-bottom)',
            }}
        >
            <BottomNavigation value={active} showLabels sx={{ bgcolor: "transparent" }}>
                {items.map(({ value, to, label, icon }) => (
                    <BottomNavigationAction
                        key={value}
                        value={value}
                        label={label}
                        icon={icon}
                        component={Link}
                        to={to}
                        aria-current={active === value ? 'page' : undefined}
                        sx={{ flex: 1, '& .MuiBottomNavigationAction-label': { whiteSpace: 'nowrap' } }}
                    />
                ))}
            </BottomNavigation>
        </Paper>
    );
};
