import { Suspense } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Navbar } from './Navbar';
import { ErrorBoundary } from './ErrorBoundary';
import { LoadingState } from './states/LoadingState';

const layoutStyle = {
  display: 'flex',
  flexDirection: 'column',
  minHeight: '100vh',
};

const mainStyle = {
  flex: 1,
  padding: '2rem',
  // Centre the page content
  display: 'flex',
  justifyContent: 'center',
};

export const PageLoader = () => <LoadingState />;

export const Layout = () => {
  const location = useLocation();
  const { i18n } = useTranslation();
  return (
    <div style={layoutStyle}>
      <Navbar />
      <main style={mainStyle}>
        {/* Child routes from App.jsx render here. A page error only affects this
            area (the navbar stays) and resets on navigation. The page is keyed by
            language so that switching languages remounts it and its data is
            refetched in the new language. */}
        <ErrorBoundary resetKey={location.pathname}>
          <Suspense fallback={<PageLoader />}>
            <Outlet key={i18n.language} />
          </Suspense>
        </ErrorBoundary>
      </main>
    </div>
  );
};
