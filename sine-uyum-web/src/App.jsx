import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Layout } from './components/Layout';
import { AuthProvider } from './context/AuthContext';
import { CustomThemeProvider } from './context/ThemeContext';
import { RegisterPage } from './pages/RegisterPage';
import { AdminRoute } from './components/AdminRoute';
import { SnackbarProvider } from './context/SnackbarProvider';
import { NotificationProvider } from './context/NotificationContext';
import { ErrorBoundary } from './components/ErrorBoundary';
import { PageLoader } from './components/Layout';

// Pages are loaded on demand (M13): the login screen opens without downloading
// the whole app (admin panel, chat, SignalR...).
const page = (load, name) => lazy(() => load().then((m) => ({ default: m[name] })));
const HomePage = page(() => import('./pages/HomePage'), 'HomePage');
const ProfilePage = page(() => import('./pages/ProfilePage'), 'ProfilePage');
const SearchResultsPage = page(() => import('./pages/SearchResultsPage'), 'SearchResultsPage');
const MovieDetailPage = page(() => import('./pages/MovieDetailPage'), 'MovieDetailPage');
const WatchlistPage = page(() => import('./pages/WatchlistPage'), 'WatchlistPage');
const WatchlistDetailPage = page(() => import('./pages/WatchlistDetailPage'), 'WatchlistDetailPage');
const DiscoverPage = page(() => import('./pages/DiscoverPage'), 'DiscoverPage');
const EditProfilePage = page(() => import('./pages/EditProfilePage'), 'EditProfilePage');
const MessagesPage = page(() => import('./pages/MessagesPage'), 'MessagesPage');
const ConversationPage = page(() => import('./pages/ConversationPage'), 'ConversationPage');
const MyEventPage = page(() => import('./pages/MyEventPage'), 'MyEventPage');
const SubscriptionPage = page(() => import('./pages/SubscriptionPage'), 'SubscriptionPage');
const AdminPage = page(() => import('./pages/AdminPage'), 'AdminPage');
const FeedPage = page(() => import('./pages/FeedPage'), 'FeedPage');
const YearInReviewPage = page(() => import('./pages/YearInReviewPage'), 'YearInReviewPage');
const WelcomePage = page(() => import('./pages/WelcomePage'), 'WelcomePage');

// The old list discovery address opens Discover › Lists (sort and search kept).
const ListsRedirect = () => {
  const { search } = useLocation();
  const params = new URLSearchParams(search);
  params.set('tab', 'lists');
  return <Navigate to={`/discover?${params}`} replace />;
};

function App() {
  return (
    <BrowserRouter>
      <CustomThemeProvider>
        <AuthProvider>
          <SnackbarProvider>
            <NotificationProvider>
              {/* Last resort: if a provider or the login screen crashes, show an error card instead of a blank page. */}
              <ErrorBoundary>
              <Suspense fallback={<PageLoader />}>
              <Routes>
                {/* Public routes */}
                <Route path="/login" element={<LoginPage />} />
                <Route path="/register" element={<RegisterPage />} />
                <Route path="/" element={<LoginPage />} />
                {/* The first-run welcome is a full-screen flow outside the layout. */}
                <Route
                  path="/welcome"
                  element={
                    <ProtectedRoute>
                      <WelcomePage />
                    </ProtectedRoute>
                  }
                />
                {/* The yearly recap is a full-screen story, so it has no navbar. */}
                <Route
                  path="/profile/:userId/year/:year?"
                  element={
                    <ProtectedRoute>
                      <YearInReviewPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  element={
                    <ProtectedRoute onboarding>
                      <Layout />
                    </ProtectedRoute>
                  }
                >
                  <Route path="/home" element={<HomePage />} />
                  <Route path="/feed" element={<FeedPage />} />
                  <Route path="/profile/:userId" element={<ProfilePage />} />
                  <Route path="/search" element={<SearchResultsPage />} />
                  <Route path="/movie/:movieId" element={<MovieDetailPage />} />
                  <Route path="/watchlist" element={<WatchlistPage />} />
                  <Route path="/watchlist/:listId" element={<WatchlistDetailPage />} />
                  <Route path="/discover" element={<DiscoverPage />} />
                  <Route path="/lists" element={<ListsRedirect />} />
                  <Route path="/profile/edit" element={<EditProfilePage />} />
                  <Route path="/messages" element={<MessagesPage />} />
                  <Route path="/messages/:otherUserId" element={<ConversationPage />} />
                  <Route path="/my-event" element={<MyEventPage />} />
                  <Route path="/subscription" element={<SubscriptionPage />} />
                  <Route element={<AdminRoute />}>
                    <Route path="/admin" element={<AdminPage />} />
                  </Route>
                </Route>
              </Routes>
              </Suspense>
              </ErrorBoundary>
            </NotificationProvider>
          </SnackbarProvider>
        </AuthProvider>
      </CustomThemeProvider>
    </BrowserRouter>
  );
}

export default App;

