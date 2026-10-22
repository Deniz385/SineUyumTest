import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box, CircularProgress, Typography } from '@mui/material';

// Shared "loading" placeholder: a centred spinner with an accessible name.
// `compact` suits a section, a menu or a dialog; the default suits a page.
// A `label` is also shown as text under the spinner.
export const LoadingState = ({ label, compact = false, sx }) => {
    const { t } = useTranslation();
    return (
        <Box
            role="status"
            data-testid="loading-state"
            sx={[
                {
                    display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
                    gap: 1.5, width: '100%', py: compact ? 2 : 6,
                },
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
        >
            <CircularProgress size={compact ? 28 : 40} aria-label={label ?? t('common.loading')} />
            {label && <Typography variant="body2" color="text.secondary">{label}</Typography>}
        </Box>
    );
};
