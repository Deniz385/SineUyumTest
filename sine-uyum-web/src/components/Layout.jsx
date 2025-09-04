
// 1. Outlet'i react-router-dom'dan import ediyoruz
import { Outlet } from 'react-router-dom';
import { Navbar } from './Navbar';

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

export const Layout = () => {
  return (
    <div style={layoutStyle}>
      <Navbar />
      <main style={mainStyle}>
        {/* 2. BU SATIR ÇOK ÖNEMLİ */}
        {/* App.jsx'teki çocuk rotalar (HomePage vb.) buraya render edilecek */}
        <Outlet />
      </main>
    </div>
  );
};
