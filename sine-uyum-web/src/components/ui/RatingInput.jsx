import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Typography } from '@mui/material';
import StarRoundedIcon from '@mui/icons-material/StarRounded';
import StarHalfRoundedIcon from '@mui/icons-material/StarHalfRounded';
import StarBorderRoundedIcon from '@mui/icons-material/StarBorderRounded';
import { srOnly } from './a11y';

const STAR_SIZES = { small: 18, medium: 28, large: 40 };

// Five stars for a 10-point value: each star is two points, a half star one.
const Stars = ({ value, size, emptyColor = 'text.muted' }) => (
    <>
        {[0, 1, 2, 3, 4].map((i) => {
            const fill = Math.max(0, Math.min(2, value - i * 2));
            const Icon = fill === 2 ? StarRoundedIcon : fill === 1 ? StarHalfRoundedIcon : StarBorderRoundedIcon;
            return (
                <Icon
                    key={i}
                    aria-hidden="true"
                    sx={{ fontSize: size, color: fill > 0 ? 'primary.main' : emptyColor, display: 'block', flexShrink: 0 }}
                />
            );
        })}
    </>
);

/** A read-only rating: five stars with halves and "8 out of 10" for screen readers. */
export const RatingStars = ({ value, size = 'small', showNumber = false, sx }) => {
    const { t } = useTranslation();
    const px = STAR_SIZES[size] ?? size;
    return (
        <Box component="span" sx={[{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }, ...(Array.isArray(sx) ? sx : [sx])]}>
            <Box component="span" sx={{ display: 'inline-flex' }}><Stars value={value ?? 0} size={px} /></Box>
            {showNumber && (
                <Typography component="span" variant="body2" aria-hidden="true" className="tabular-nums" sx={{ fontWeight: 700, color: 'text.primary' }}>
                    {value}
                </Typography>
            )}
            <Box component="span" sx={srOnly}>{t('rating.outOf', { value })}</Box>
        </Box>
    );
};

/**
 * The 10-point rating input, drawn as five stars with halves. Click, tap or
 * drag across the stars; with the keyboard, arrows change it by one point,
 * Home/End jump to 1/10 and the digits 1–9 (0 for 10) set it directly. The
 * number is always shown next to the stars. `value` 0 means "not rated yet".
 */
export const RatingInput = ({ value = 0, onChange, size = 'large', label, autoFocus = false, disabled = false, sx, id }) => {
    const { t } = useTranslation();
    const [hover, setHover] = useState(null);
    const dragging = useRef(false);
    const starsRef = useRef(null);
    const sliderRef = useRef(null);
    const px = STAR_SIZES[size] ?? size;
    const shown = hover ?? value;

    // Focus on open (e.g. in a rating dialog) so the arrows work right away.
    useEffect(() => {
        if (autoFocus) sliderRef.current?.focus();
    }, [autoFocus]);

    const set = (next) => {
        const clamped = Math.max(1, Math.min(10, next));
        if (clamped !== value) onChange?.(clamped);
    };

    const valueAt = (clientX) => {
        const rect = starsRef.current.getBoundingClientRect();
        const ratio = (clientX - rect.left) / rect.width;
        return Math.max(1, Math.min(10, Math.ceil(ratio * 10)));
    };

    const onKeyDown = (e) => {
        if (disabled) return;
        const current = value || 0;
        let next = null;
        switch (e.key) {
            case 'ArrowRight':
            case 'ArrowUp':
                next = current + 1;
                break;
            case 'ArrowLeft':
            case 'ArrowDown':
                next = current - 1;
                break;
            case 'PageUp':
                next = current + 2;
                break;
            case 'PageDown':
                next = current - 2;
                break;
            case 'Home':
                next = 1;
                break;
            case 'End':
                next = 10;
                break;
            default:
                if (/^[0-9]$/.test(e.key)) next = e.key === '0' ? 10 : Number(e.key);
        }
        if (next !== null) {
            e.preventDefault();
            set(next);
        }
    };

    const pointerHandlers = disabled ? {} : {
        onPointerDown: (e) => {
            dragging.current = true;
            e.currentTarget.setPointerCapture?.(e.pointerId);
            set(valueAt(e.clientX));
        },
        onPointerMove: (e) => {
            const at = valueAt(e.clientX);
            if (dragging.current) set(at);
            else if (e.pointerType === 'mouse') setHover(at);
        },
        onPointerUp: (e) => {
            dragging.current = false;
            e.currentTarget.releasePointerCapture?.(e.pointerId);
        },
        onPointerCancel: () => { dragging.current = false; },
        onPointerLeave: () => { if (!dragging.current) setHover(null); },
    };

    return (
        <Box sx={[{ display: 'inline-flex', alignItems: 'center', gap: 1.5 }, ...(Array.isArray(sx) ? sx : [sx])]}>
            <Box
                id={id}
                ref={sliderRef}
                role="slider"
                tabIndex={disabled ? -1 : 0}
                aria-label={label ?? t('rating.inputLabel')}
                aria-valuemin={1}
                aria-valuemax={10}
                aria-valuenow={value || undefined}
                aria-valuetext={value ? t('rating.outOf', { value }) : t('rating.none')}
                aria-disabled={disabled || undefined}
                onKeyDown={onKeyDown}
                data-testid="rating-input"
                sx={{
                    display: 'inline-flex', borderRadius: 1, p: 0.5, m: -0.5,
                    cursor: disabled ? 'default' : 'pointer', opacity: disabled ? 0.5 : 1,
                    touchAction: 'none', userSelect: 'none',
                }}
            >
                <Box ref={starsRef} sx={{ display: 'inline-flex' }} {...pointerHandlers}>
                    <Stars value={shown} size={px} />
                </Box>
            </Box>
            <Typography
                component="span"
                aria-hidden="true"
                data-testid="rating-number"
                sx={{ typography: 'number', fontSize: px * 0.75, minWidth: '3.2ch', color: shown ? 'text.primary' : 'text.muted' }}
            >
                {shown ? shown : '–'}
                <Box component="span" sx={{ fontSize: '0.6em', color: 'text.secondary', ml: 0.25 }}>/10</Box>
            </Typography>
        </Box>
    );
};
