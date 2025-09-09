// src/theme.js
import { createTheme } from '@mui/material/styles';

// Açık mod için renk paleti ve ayarlar
export const lightTheme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#1976d2', // Klasik Mavi
    },
    secondary: {
      main: '#dc004e', // Canlı Pembe
    },
    background: {
      default: '#f4f6f8', // Çok hafif gri arka plan
      paper: '#ffffff', // Kartlar ve diğer yüzeyler için beyaz
    },
  },
});

// Koyu mod için (daha iyi kontrastlı) renk paleti ve ayarlar
export const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#64b5f6', // Daha açık ve canlı mavi
    },
    secondary: {
      main: '#f48fb1', // Daha açık pembe/fuşya
    },
    background: {
      default: '#121212', // Saf siyaha yakın ana arka plan
      paper: '#1e1e1e', // Kartlar ve diğer yüzeyler için biraz daha açık gri
    },
    text: {
        primary: '#e0e0e0',
        secondary: '#b0b0b0',
    }
  },
});