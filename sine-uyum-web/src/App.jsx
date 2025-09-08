// src/App.jsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { HomePage } from './pages/HomePage';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Layout } from './components/Layout';
// 1. Oluşturduğumuz AuthProvider'ı import edelim
import { AuthProvider } from './context/AuthContext';
import { RegisterPage } from './pages/RegisterPage';
import { ProfilePage } from './pages/ProfilePage';
import { SearchResultsPage } from './pages/SearchResultsPage';
import { MovieDetailPage } from './pages/MovieDetailPage';
import { WatchlistPage } from './pages/WatchlistPage';
import { EditProfilePage } from './pages/EditProfilePage';
import { MessagesPage } from './pages/MessagesPage';
import { ConversationPage } from './pages/ConversationPage';

function App() {
  return (
    <BrowserRouter>
      {/* 2. Tüm rotaları AuthProvider ile sarmalayalım */}
      <AuthProvider>
        <Routes>
          {/* Rotalarımız aynı kalıyor */}
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
            <Route path="/profile/edit" element={<EditProfilePage />} />
            <Route path="/messages" element={<MessagesPage />} />
            <Route path="/messages/:otherUserId" element={<ConversationPage />} />
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;