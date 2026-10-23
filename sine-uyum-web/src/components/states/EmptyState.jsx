import React from 'react';
import { Box, Typography } from '@mui/material';

// Shared "nothing here yet" message (design system: icon, one-line title,
// one-line hint, optional action). `icon` is a component, e.g.
// `icon={InboxIcon}`. `compact` suits a section, a menu or a dialog.
export const EmptyState = ({ icon: Icon, title, description, action, compact = false, sx }) => (
    <Box
        data-testid="empty-state"
        sx={[
            {
                display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center',
                gap: compact ? 0.75 : 1, width: '100%', py: compact ? 2.5 : 7, px: 2,
            },
            ...(Array.isArray(sx) ? sx : [sx]),
        ]}
    >
        {Icon && (
            <Box
                aria-hidden="true"
                sx={{
                    width: compact ? 48 : 72, height: compact ? 48 : 72, borderRadius: '50%', mb: compact ? 0.5 : 1,
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    bgcolor: 'surface.raised', color: 'text.secondary', border: 1, borderColor: 'divider',
                }}
            >
                <Icon sx={{ fontSize: compact ? 24 : 34 }} />
            </Box>
        )}
        <Typography variant={compact ? 'subtitle1' : 'h2'} component="p" sx={{ maxWidth: '48ch' }}>{title}</Typography>
        {description && (
            <Typography variant="body1" color="text.secondary" sx={{ maxWidth: '56ch' }}>{description}</Typography>
        )}
        {action && <Box sx={{ mt: compact ? 1 : 1.5 }}>{action}</Box>}
    </Box>
);
