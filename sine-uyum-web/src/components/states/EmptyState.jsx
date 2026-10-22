import React from 'react';
import { Box, Typography } from '@mui/material';

// Shared "nothing here yet" message: an icon (a component, e.g. `icon={InboxIcon}`),
// a title, and optionally a description and an action (usually a button).
// `compact` suits a section, a menu or a dialog; the default suits a page.
export const EmptyState = ({ icon: Icon, title, description, action, compact = false, sx }) => (
    <Box
        data-testid="empty-state"
        sx={[
            {
                display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center',
                gap: 1, width: '100%', py: compact ? 2 : 6, px: 2,
            },
            ...(Array.isArray(sx) ? sx : [sx]),
        ]}
    >
        {Icon && <Icon aria-hidden="true" sx={{ fontSize: compact ? 36 : 56, color: 'text.disabled' }} />}
        <Typography variant={compact ? 'subtitle1' : 'h6'} component="p" sx={{ fontWeight: 600, maxWidth: '60ch' }}>{title}</Typography>
        {description && (
            <Typography variant="body2" color="text.secondary" sx={{ maxWidth: '60ch' }}>{description}</Typography>
        )}
        {action && <Box sx={{ mt: 1 }}>{action}</Box>}
    </Box>
);
