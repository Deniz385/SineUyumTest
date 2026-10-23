import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box, CircularProgress, Typography } from '@mui/material';

// Shared "loading" placeholder for whole-page waits (route changes, session
// restore); content areas use the skeletons in components/ui/Skeletons.
// `compact` suits a section, a menu or a dialog. A `label` is shown as text.
export const LoadingState = ({ label, compact = false, sx }) => {
    const { t } = useTranslation();
    return (
        <Box
            role="status"
            data-testid="loading-state"
            sx={[
                {
                    display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
                    gap: 1.5, width: '100%', py: compact ? 2.5 : 8,
                },
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
        >
            <CircularProgress size={compact ? 24 : 36} thickness={4} aria-label={label ?? t('common.loading')} />
            {label && <Typography variant="body2" color="text.secondary">{label}</Typography>}
        </Box>
    );
};
