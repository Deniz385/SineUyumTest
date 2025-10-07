import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { HomePage } from './pages/HomePage';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Layout } from './components/Layout';
import { AuthProvider } from './context/AuthContext';
import { CustomThemeProvider } from './context/ThemeContext';
import { RegisterPage } from './pages/RegisterPage';
import { ProfilePage } from './pages/ProfilePage';
import { SearchResultsPage } from './pages/SearchResultsPage';
import { MovieDetailPage } from './pages/MovieDetailPage';
import { WatchlistPage } from './pages/WatchlistPage';
import { WatchlistDetailPage } from './pages/WatchlistDetailPage';
import { EditProfilePage } from './pages/EditProfilePage';
import { MessagesPage } from './pages/MessagesPage';
import { ConversationPage } from './pages/ConversationPage';
import { MyEventPage } from './pages/MyEventPage';
import { SubscriptionPage } from './pages/SubscriptionPage';
import { AdminPage } from './pages/AdminPage';
import { AdminRoute } from './components/AdminRoute';
import { SnackbarProvider } from './context/SnackbarProvider';
import { NotificationProvider } from './context/NotificationContext';

function App() {
  return (
    <BrowserRouter>
      <CustomThemeProvider>
        <AuthProvider>
          <SnackbarProvider>
            <NotificationProvider>
              <Routes>
                {/* Herkesin erişebileceği rotalar */}
                <Route path="/login" element={<LoginPage />} />
                <Route path="/register" element={<RegisterPage />} />
                <Route path="/" element={<LoginPage />} />
                <Route
                  element={
                    <ProtectedRoute>
                      <Layout />
                    </ProtectedRoute>
                  }
                >                
                  <Route path="/home" element={<HomePage />} />
                  <Route path="/profile/:userId" element={<ProfilePage />} />
                  <Route path="/search" element={<SearchResultsPage />} />
                  <Route path="/movie/:movieId" element={<MovieDetailPage />} />
                  <Route path="/watchlist" element={<WatchlistPage />} />
                  <Route path="/watchlist/:listId" element={<WatchlistDetailPage />} />
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
            </NotificationProvider>
          </SnackbarProvider>
        </AuthProvider>
      </CustomThemeProvider>
    </BrowserRouter>
  );
}

export default App;

