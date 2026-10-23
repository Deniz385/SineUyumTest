import React from 'react';
import { Box, Typography } from '@mui/material';
import { layout } from '../../theme';

const WIDTHS = { default: layout.maxWidth, reading: layout.readingWidth, narrow: 480, wide: 1440 };

/**
 * The page column: content max width 1200 (reading pages such as the feed
 * 680), 16px side padding on phones and 24px from tablets up.
 */
export const Page = ({ width = 'default', children, sx, component = 'div', ...rest }) => (
    <Box
        component={component}
        sx={[
            {
                width: '100%',
                maxWidth: (WIDTHS[width] ?? width) + 48,
                mx: 'auto',
                px: { xs: 2, sm: 3 },
                py: { xs: 2, md: 4 },
                textAlign: 'left',
                minWidth: 0,
            },
            ...(Array.isArray(sx) ? sx : [sx]),
        ]}
        {...rest}
    >
        {children}
    </Box>
);

/** The consistent page header: optional overline, title, subtitle and actions. */
export const PageHeader = ({ title, subtitle, overline, actions, children, sx }) => (
    <Box
        component="header"
        sx={[
            { display: 'flex', alignItems: { xs: 'flex-start', sm: 'flex-end' }, justifyContent: 'space-between', flexWrap: 'wrap', gap: 2, mb: { xs: 2.5, md: 3 } },
            ...(Array.isArray(sx) ? sx : [sx]),
        ]}
    >
        <Box sx={{ minWidth: 0, flex: '1 1 320px' }}>
            {overline && <Typography variant="overline" component="p" color="primary" sx={{ mb: 0.5 }}>{overline}</Typography>}
            <Typography variant="h1" component="h1" sx={{ overflowWrap: 'anywhere' }}>{title}</Typography>
            {subtitle && <Typography variant="body1" color="text.secondary" sx={{ mt: 0.75, maxWidth: '65ch' }}>{subtitle}</Typography>}
            {children}
        </Box>
        {actions && <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>{actions}</Box>}
    </Box>
);
