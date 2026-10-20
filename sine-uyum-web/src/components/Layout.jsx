
// 1. Outlet'i react-router-dom'dan import ediyoruz
import { Suspense } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { Navbar } from './Navbar';
import { ErrorBoundary } from './ErrorBoundary';

const layoutStyle = {
  display: 'flex',
  flexDirection: 'column',
  minHeight: '100vh',
};

const mainStyle = {
  flex: 1,
  padding: '2rem',
  // Sayfa içeriğini ortalamak için ekleyebiliriz
  display: 'flex',
  justifyContent: 'center',
};

export const PageLoader = () => (
  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 6 }}>
    <CircularProgress aria-label="Yükleniyor" />
  </Box>
);

export const Layout = () => {
  const location = useLocation();
  return (
    <div style={layoutStyle}>
      <Navbar />
      <main style={mainStyle}>
        {/* App.jsx'teki çocuk rotalar buraya çizilir. Sayfa hatası yalnızca bu
            alanı etkiler (gezinti çubuğu kalır); başka sayfaya geçince sıfırlanır. */}
        <ErrorBoundary resetKey={location.pathname}>
          <Suspense fallback={<PageLoader />}>
            <Outlet />
          </Suspense>
        </ErrorBoundary>
      </main>
    </div>
  );
};
