import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Button, Typography } from '@mui/material';
import ErrorOutlineRoundedIcon from '@mui/icons-material/ErrorOutlineRounded';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';

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
                    gap: compact ? 0.75 : 1, width: '100%', py: compact ? 2.5 : 7, px: 2,
                },
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
        >
            <Box
                aria-hidden="true"
                sx={{
                    width: compact ? 48 : 72, height: compact ? 48 : 72, borderRadius: '50%', mb: compact ? 0.5 : 1,
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    bgcolor: 'surface.raised', color: 'error.main', border: 1, borderColor: 'divider',
                }}
            >
                <ErrorOutlineRoundedIcon sx={{ fontSize: compact ? 24 : 34 }} />
            </Box>
            <Typography variant={compact ? 'body2' : 'body1'} sx={{ maxWidth: '56ch', fontWeight: 600 }}>
                {message || t('states.loadFailed')}
            </Typography>
            {onRetry && (
                <Button variant="tonal" size={compact ? 'small' : 'medium'} startIcon={<RefreshRoundedIcon />} onClick={onRetry} sx={{ mt: 1 }}>
                    {t('common.retry')}
                </Button>
            )}
        </Box>
    );
};
