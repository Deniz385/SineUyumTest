import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { Snackbar, Alert } from '@mui/material';

const SnackbarContext = createContext(null);

export const useSnackbar = () => {
    const context = useContext(SnackbarContext);
    if (!context) {
        throw new Error('useSnackbar must be used within a SnackbarProvider');
    }
    return context;
};

export const SnackbarProvider = ({ children }) => {
    const [snackbar, setSnackbar] = useState({
        open: false,
        message: '',
        severity: 'info', // 'success', 'error', 'warning', 'info'
    });

    // Stable identity: consumers list showSnackbar in effect dependencies
    // (e.g. the SignalR connection), so a new function per render would
    // re-run those effects every time a snackbar opens.
    const showSnackbar = useCallback((message, severity = 'info') => {
        setSnackbar({ open: true, message, severity });
    }, []);

    const handleClose = (event, reason) => {
        if (reason === 'clickaway') {
            return;
        }
        setSnackbar((current) => ({ ...current, open: false }));
    };

    const value = useMemo(() => ({ showSnackbar }), [showSnackbar]);

    return (
        <SnackbarContext.Provider value={value}>
            {children}
            <Snackbar
                open={snackbar.open}
                autoHideDuration={6000}
                onClose={handleClose}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
                // Above the bottom navigation on phones.
                sx={{ bottom: { xs: 'calc(80px + env(safe-area-inset-bottom))', md: 24 } }}
            >
                {/* Errors interrupt (role alert); other messages are a polite live region. */}
                <Alert
                    onClose={handleClose}
                    severity={snackbar.severity}
                    role={snackbar.severity === 'error' ? 'alert' : 'status'}
                    aria-live={snackbar.severity === 'error' ? 'assertive' : 'polite'}
                    sx={{
                        width: '100%', bgcolor: 'surface.overlay', color: 'text.primary', border: 1, borderColor: 'divider',
                        boxShadow: '0 12px 32px rgba(0,0,0,.35)', maxWidth: 560,
                    }}
                >
                    {snackbar.message}
                </Alert>
            </Snackbar>
        </SnackbarContext.Provider>
    );
};
