import { Suspense } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box } from '@mui/material';
import { Navbar } from './Navbar';
import { BottomNav } from './BottomNav';
import { ErrorBoundary } from './ErrorBoundary';
import { LoadingState } from './states/LoadingState';
import { layout } from '../theme';

export const PageLoader = () => <LoadingState />;

export const Layout = () => {
  const location = useLocation();
  const { i18n } = useTranslation();
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh', bgcolor: 'background.default' }}>
      <Navbar />
      <Box
        component="main"
        id="main"
        tabIndex={-1}
        sx={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'stretch',
          minWidth: 0,
          outline: 'none',
          // Room for the bottom navigation on phones.
          pb: { xs: `calc(${layout.bottomNavHeight}px + env(safe-area-inset-bottom))`, md: 0 },
        }}
      >
        {/* Child routes from App.jsx render here. A page error only affects this
            area (the navbar stays) and resets on navigation. The page is keyed by
            language so that switching languages remounts it and its data is
            refetched in the new language. */}
        <ErrorBoundary resetKey={location.pathname}>
          <Suspense fallback={<PageLoader />}>
            <Outlet key={i18n.language} />
          </Suspense>
        </ErrorBoundary>
      </Box>
      <BottomNav />
    </Box>
  );
};
