import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { UserSearch } from './UserSearch';
import { MovieSearchBar } from './MovieSearchBar'; // Yeni film arama barını import et

const navStyle = {
  backgroundColor: '#212529',
  padding: '1rem 2rem',
  color: 'white',
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'center',
  boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
};

const linkStyle = {
  color: '#f8f9fa',
  textDecoration: 'none',
  margin: '0 15px',
  fontSize: '1rem',
  fontWeight: '500',
};

const brandStyle = {
  ...linkStyle,
  fontSize: '1.5rem',
  fontWeight: 'bold',
};

const buttonStyle = {
  ...linkStyle,
  background: 'transparent',
  border: 'none',
  cursor: 'pointer',
  padding: 0,
  fontFamily: 'inherit',
};

export const Navbar = () => {
  const { token, user, logoutAction } = useAuth();

  return (
    <nav style={navStyle}>
      <div>
        <Link to="/home" style={brandStyle}>
          SineUyum
        </Link>
      </div>

      <div style={{ display: 'flex', alignItems: 'center' }}>
        {token && user && (
          <>
            <MovieSearchBar /> {/* YENİ FİLM ARAMA BARI */}
            <UserSearch />
            <Link to={`/profile/${user.id}`} style={linkStyle}>
              Profilim
            </Link>
            <button onClick={logoutAction} style={buttonStyle}>
              Çıkış Yap
            </button>
          </>
        )}
      </div>
    </nav>
  );
};
