import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Button, Typography } from '@mui/material';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import RefreshIcon from '@mui/icons-material/Refresh';

// Shared "could not load" message. With `onRetry` it offers a "Try again"
// button that calls it. `compact` suits a section, a menu or a dialog.
export const ErrorState = ({ message, onRetry, compact = false, sx }) => {
    const { t } = useTranslation();
    return (
        <Box
            role="alert"
            data-testid="error-state"
            sx={[
                {
                    display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center',
                    gap: 1, width: '100%', py: compact ? 2 : 6, px: 2,
                },
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
        >
            <ErrorOutlineIcon aria-hidden="true" sx={{ fontSize: compact ? 36 : 56, color: 'error.main' }} />
            <Typography variant={compact ? 'body2' : 'body1'} sx={{ maxWidth: '60ch' }}>
                {message || t('states.loadFailed')}
            </Typography>
            {onRetry && (
                <Button variant="outlined" size={compact ? 'small' : 'medium'} startIcon={<RefreshIcon />} onClick={onRetry} sx={{ mt: 1 }}>
                    {t('common.retry')}
                </Button>
            )}
        </Box>
    );
};
