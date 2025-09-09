// src/context/ThemeContext.jsx
import React, { createContext, useState, useMemo, useContext } from 'react';
import { ThemeProvider as MuiThemeProvider, CssBaseline } from '@mui/material';
import { lightTheme, darkTheme } from '../theme';

const ThemeContext = createContext();

export const CustomThemeProvider = ({ children }) => {
  // Başlangıç temasını kullanıcının sistem tercihine göre ayarla
  const prefersDarkMode = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
  const [mode, setMode] = useState(prefersDarkMode ? 'dark' : 'light');

  const toggleTheme = () => {
    setMode((prevMode) => (prevMode === 'light' ? 'dark' : 'light'));
  };

  // Mod değiştiğinde doğru temayı seç
  const theme = useMemo(() => (mode === 'light' ? lightTheme : darkTheme), [mode]);

  return (
    <ThemeContext.Provider value={{ mode, toggleTheme }}>
      <MuiThemeProvider theme={theme}>
        {/* CssBaseline, temel CSS sıfırlamaları yapar ve arka plan rengini temaya göre ayarlar */}
        <CssBaseline /> 
        {children}
      </MuiThemeProvider>
    </ThemeContext.Provider>
  );
};

// Kendi hook'umuzu oluşturalım
export const useThemeContext = () => {
    return useContext(ThemeContext);
};