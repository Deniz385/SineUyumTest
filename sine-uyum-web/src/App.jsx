// src/App.jsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Ana yol (/) istendiğinde LoginPage component'ini göster */}
        <Route path="/" element={<LoginPage />} />

        {/* Gelecekte buraya başka sayfalar ekleyeceğiz */}
        {/* <Route path="/register" element={<RegisterPage />} /> */}
        {/* <Route path="/home" element={<HomePage />} /> */}
      </Routes>
    </BrowserRouter>
  );
}

export default App;