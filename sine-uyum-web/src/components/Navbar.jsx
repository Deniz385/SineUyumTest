import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

// 1. Stil objelerinin içini dolduruyoruz
const navStyle = {
  backgroundColor: '#212529', // Koyu bir arkaplan
  padding: '1rem 2rem',
  color: 'white',
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'center',
  boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
};

const linkStyle = {
  color: '#f8f9fa', // Hafif kırık beyaz
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
  const { token, logoutAction } = useAuth();

  return (
    <nav style={navStyle}>
      <div>
        <Link to="/home" style={brandStyle}>
          SineUyum
        </Link>
      </div>
      <div>
        {/* Token varsa, Anasayfa ve Çıkış Yap butonunu göster */}
        {token && (
          <>
            <Link to="/home" style={linkStyle}>
              Anasayfa
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