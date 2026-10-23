import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box } from '@mui/material';
import { alpha, useTheme } from '@mui/material/styles';
import { compatibilityTone } from '../../theme';
import { srOnly } from './a11y';

const RING_SIZES = { sm: 40, md: 56, lg: 76 };

/**
 * Compatibility as a pill ("92%") or a ring, in the compatibility scale
 * colour (< 50 accent, 50–74 primary, ≥ 75 success). The number is always
 * shown; screen readers hear "92% compatible". `label` adds visible text
 * after the number (e.g. "compatible"); `text` replaces the whole visible
 * text with a self-describing phrase ("78% compatible").
 */
export const CompatibilityBadge = ({ value, variant = 'pill', size = 'md', label, text, sx, ...rest }) => {
    const { t } = useTranslation();
    const theme = useTheme();
    if (value == null || Number.isNaN(Number(value))) return null;
    const score = Math.round(Number(value));
    const tone = theme.palette.tokens[compatibilityTone(score)];
    const isDark = theme.palette.mode === 'dark';
    const spoken = t('compatibility.badge', { score });

    if (variant === 'ring') {
        const box = RING_SIZES[size] ?? RING_SIZES.md;
        const stroke = size === 'sm' ? 3 : 4;
        const r = (box - stroke) / 2;
        const circumference = 2 * Math.PI * r;
        return (
            <Box
                data-testid="compat-badge"
                sx={[{ position: 'relative', width: box, height: box, flexShrink: 0, display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }, ...(Array.isArray(sx) ? sx : [sx])]}
                {...rest}
            >
                <svg width={box} height={box} viewBox={`0 0 ${box} ${box}`} aria-hidden="true" style={{ position: 'absolute', inset: 0, transform: 'rotate(-90deg)' }}>
                    <circle cx={box / 2} cy={box / 2} r={r} fill="none" stroke={theme.palette.tokens.border} strokeWidth={stroke} />
                    <circle
                        cx={box / 2} cy={box / 2} r={r} fill="none" stroke={tone} strokeWidth={stroke} strokeLinecap="round"
                        strokeDasharray={circumference} strokeDashoffset={circumference * (1 - Math.max(0, Math.min(100, score)) / 100)}
                    />
                </svg>
                <Box
                    component="span"
                    aria-hidden="true"
                    sx={{ typography: 'number', fontSize: box * 0.27, color: 'text.primary', position: 'relative' }}
                >
                    {t('compatibility.percent', { score })}
                </Box>
                <Box component="span" sx={srOnly}>{spoken}</Box>
            </Box>
        );
    }

    const small = size === 'sm';
    return (
        <Box
            component="span"
            data-testid="compat-badge"
            sx={[
                {
                    display: 'inline-flex', alignItems: 'center', gap: 0.5, flexShrink: 0,
                    px: small ? 0.75 : 1.25, height: small ? 20 : 26, borderRadius: 999,
                    border: `1px solid ${alpha(tone, 0.45)}`,
                    bgcolor: alpha(tone, isDark ? 0.14 : 0.08),
                    color: isDark ? tone : theme.palette.tokens.text,
                    fontSize: small ? '0.75rem' : '0.8125rem',
                    fontWeight: 700,
                    lineHeight: 1,
                    whiteSpace: 'nowrap',
                },
                ...(Array.isArray(sx) ? sx : [sx]),
            ]}
            {...rest}
        >
            {!isDark && <Box component="span" aria-hidden="true" sx={{ width: 6, height: 6, borderRadius: '50%', bgcolor: tone }} />}
            {text ? (
                <Box component="span" className="tabular-nums">{text}</Box>
            ) : (
                <>
                    <Box component="span" aria-hidden="true" className="tabular-nums" sx={{ fontFamily: theme.typography.number.fontFamily }}>
                        {t('compatibility.percent', { score })}
                    </Box>
                    {label && <Box component="span" aria-hidden="true" sx={{ fontWeight: 600 }}>{label}</Box>}
                    <Box component="span" sx={srOnly}>{spoken}</Box>
                </>
            )}
        </Box>
    );
};
