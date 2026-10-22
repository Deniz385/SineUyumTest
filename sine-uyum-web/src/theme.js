// src/theme.js
import { createTheme } from '@mui/material/styles';

// Colour palette and settings for light mode
export const lightTheme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#1976d2', // Classic blue
    },
    secondary: {
      main: '#dc004e', // Vivid pink
    },
    background: {
      default: '#f4f6f8', // Very light grey background
      paper: '#ffffff', // White for cards and other surfaces
    },
  },
});

// Colour palette and settings for dark mode (with better contrast)
export const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#64b5f6', // Lighter, more vivid blue
    },
    secondary: {
      main: '#f48fb1', // Lighter pink/fuchsia
    },
    background: {
      default: '#121212', // Near-black main background
      paper: '#1e1e1e', // Slightly lighter grey for cards and other surfaces
    },
    text: {
        primary: '#e0e0e0',
        secondary: '#b0b0b0',
    }
  },
});