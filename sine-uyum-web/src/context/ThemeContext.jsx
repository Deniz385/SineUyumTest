// src/context/ThemeContext.jsx
import React, { createContext, useState, useMemo, useContext, useEffect, useCallback } from 'react';
import { ThemeProvider as MuiThemeProvider, CssBaseline } from '@mui/material';
import { lightTheme, darkTheme, tokens } from '../theme';

const ThemeContext = createContext();

const STORAGE_KEY = 'themeMode';

// Dark is the default (the design is cinematic); a choice made with the
// toggle is remembered. Storage can throw (private mode), so it is optional.
const readSavedMode = () => {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    return saved === 'light' || saved === 'dark' ? saved : null;
  } catch {
    return null;
  }
};

const saveMode = (mode) => {
  try {
    localStorage.setItem(STORAGE_KEY, mode);
  } catch {
    // Ignore: the theme still changes for this visit.
  }
};

export const CustomThemeProvider = ({ children }) => {
  const [mode, setMode] = useState(() => readSavedMode() ?? 'dark');

  const toggleTheme = useCallback(() => {
    setMode((prevMode) => {
      const next = prevMode === 'light' ? 'dark' : 'light';
      saveMode(next);
      return next;
    });
  }, []);

  // The browser chrome (address bar on phones) follows the theme.
  useEffect(() => {
    const root = document.documentElement;
    root.dataset.theme = mode;
    // index.html paints the first frame inline; keep it in step.
    root.style.backgroundColor = tokens[mode].bg;
    root.style.colorScheme = mode;
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute('content', tokens[mode].bg);
  }, [mode]);

  const theme = useMemo(() => (mode === 'light' ? lightTheme : darkTheme), [mode]);
  const value = useMemo(() => ({ mode, toggleTheme }), [mode, toggleTheme]);

  return (
    <ThemeContext.Provider value={value}>
      <MuiThemeProvider theme={theme}>
        {/* CssBaseline applies the base CSS reset and the theme's background colour */}
        <CssBaseline enableColorScheme />
        {children}
      </MuiThemeProvider>
    </ThemeContext.Provider>
  );
};

// Hook for reading the theme context
export const useThemeContext = () => {
    return useContext(ThemeContext);
};
