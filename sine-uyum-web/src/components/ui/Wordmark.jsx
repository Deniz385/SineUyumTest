import React from 'react';
import { Box } from '@mui/material';
import { FONT_DISPLAY } from '../../theme';

// The SineUyum wordmark: "Sine" in the text colour, "Uyum" in marquee gold.
export const Wordmark = ({ size = 22, sx }) => (
    <Box
        component="span"
        sx={[
            {
                fontFamily: FONT_DISPLAY, fontWeight: 800, fontSize: size, lineHeight: 1, letterSpacing: '-0.03em',
                color: 'text.primary', whiteSpace: 'nowrap', userSelect: 'none',
            },
            ...(Array.isArray(sx) ? sx : [sx]),
        ]}
    >
        Sine<Box component="span" sx={{ color: 'primary.main' }}>Uyum</Box>
    </Box>
);
