// src/App.jsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { HomePage } from './pages/HomePage';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Layout } from './components/Layout';
// 1. Oluşturduğumuz AuthProvider'ı import edelim
import { AuthProvider } from './context/AuthContext';
import { RegisterPage } from './pages/RegisterPage';

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
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;