// The SineUyum design system for the web (docs/DESIGN-SYSTEM.md): colour
// tokens for both themes, typography, shape, motion and the MUI component
// overrides built on them. Dark is the default theme.
import { alpha, createTheme } from '@mui/material/styles';

export const FONT_BODY = '"Inter Variable", Inter, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
export const FONT_DISPLAY = '"Bricolage Grotesque Variable", "Inter Variable", system-ui, sans-serif';

// §1 Colour. Every text/background pair is WCAG 2.1 AA.
export const tokens = {
    dark: {
        bg: '#0B0C10',
        surface: '#14161C',
        surfaceRaised: '#1C1F27',
        surfaceOverlay: '#262A34',
        border: '#2E3340',
        text: '#F2F2F5',
        textSecondary: '#A9ADBA',
        textMuted: '#868B99',
        primary: '#F5B841',
        onPrimary: '#1A1300',
        accent: '#FF5A5F',
        accentStrong: '#D93A40',
        success: '#3DD68C',
        error: '#FF6B70',
        info: '#6CB4FF',
        hairline: 'rgba(255,255,255,.06)',
    },
    light: {
        bg: '#F7F6F3',
        surface: '#FFFFFF',
        surfaceRaised: '#F0EEE9',
        surfaceOverlay: '#FFFFFF',
        border: '#E4E1DA',
        text: '#16171B',
        textSecondary: '#545866',
        textMuted: '#6B6F7B',
        primary: '#9A6200',
        onPrimary: '#FFFFFF',
        accent: '#C9252C',
        accentStrong: '#C9252C',
        success: '#137A4B',
        error: '#C42B31',
        info: '#1F63B8',
        hairline: 'rgba(0,0,0,.06)',
    },
};

export const gradients = {
    marquee: 'linear-gradient(120deg, #FFD54F 0%, #FF8A65 50%, #FF5A5F 100%)',
    // The same hues darkened for text on the light theme (AA on paper).
    marqueeOnLight: 'linear-gradient(120deg, #8A5600 0%, #B4400F 50%, #B3202A 100%)',
};

// Marquee-gradient text for headlines in either theme.
export const marqueeText = (mode) => ({
    background: mode === 'light' ? gradients.marqueeOnLight : gradients.marquee,
    WebkitBackgroundClip: 'text',
    backgroundClip: 'text',
    color: 'transparent',
});

// Text over artwork: bg from transparent at the top to opaque at the bottom,
// on top of a 40% bg overlay.
export const backdropScrim = (bg) =>
    `linear-gradient(180deg, ${alpha(bg, 0)} 0%, ${alpha(bg, 0.55)} 55%, ${bg} 100%), linear-gradient(${alpha(bg, 0.4)}, ${alpha(bg, 0.4)})`;

// §3 Shape and space.
export const radii = { sm: 8, md: 12, lg: 20, pill: 999 };
export const layout = { maxWidth: 1200, readingWidth: 680, topBarHeight: 64, bottomNavHeight: 64 };

// §4 Motion.
export const motion = {
    fast: 120,
    standard: 200,
    emphasized: 320,
    easing: 'cubic-bezier(0.2, 0, 0, 1)',
    easingEnter: 'cubic-bezier(0.05, 0.7, 0.1, 1)',
};

// The compatibility scale (always shown with the number).
export const compatibilityTone = (score) => (score >= 75 ? 'success' : score >= 50 ? 'primary' : 'accent');

const lightShadow = '0 1px 2px rgba(0,0,0,.06), 0 4px 16px rgba(0,0,0,.06)';

const buildTheme = (mode) => {
    const c = tokens[mode];
    const isDark = mode === 'dark';
    const focusRing = { outline: `2px solid ${c.primary}`, outlineOffset: 2 };

    return createTheme({
        palette: {
            mode,
            primary: { main: c.primary, contrastText: c.onPrimary },
            secondary: { main: c.accent, contrastText: isDark ? '#1A0001' : '#FFFFFF' },
            error: { main: c.error, contrastText: isDark ? '#1A0001' : '#FFFFFF' },
            warning: { main: c.primary, contrastText: c.onPrimary },
            info: { main: c.info, contrastText: isDark ? '#00172E' : '#FFFFFF' },
            success: { main: c.success, contrastText: isDark ? '#00180B' : '#FFFFFF' },
            accent: { main: c.accent, strong: c.accentStrong, contrastText: '#FFFFFF' },
            background: { default: c.bg, paper: c.surface },
            surface: { main: c.surface, raised: c.surfaceRaised, overlay: c.surfaceOverlay },
            text: { primary: c.text, secondary: c.textSecondary, disabled: c.textMuted, muted: c.textMuted },
            divider: c.border,
            border: { main: c.border },
            action: {
                hover: alpha(c.text, isDark ? 0.06 : 0.05),
                selected: alpha(c.primary, isDark ? 0.14 : 0.1),
                focus: alpha(c.primary, 0.2),
                disabled: alpha(c.text, 0.38),
                disabledBackground: alpha(c.text, 0.1),
            },
            tokens: c,
        },
        shape: { borderRadius: radii.sm },
        radii,
        motion,
        typography: {
            fontFamily: FONT_BODY,
            htmlFontSize: 16,
            // §2 Typography. h1–h3 follow the spec; h4–h6 are smaller display
            // steps for older page markup.
            display: { fontFamily: FONT_DISPLAY, fontSize: '2.5rem', lineHeight: '2.75rem', fontWeight: 800, letterSpacing: '-0.02em' },
            h1: { fontFamily: FONT_DISPLAY, fontSize: '1.875rem', lineHeight: '2.25rem', fontWeight: 700, letterSpacing: '-0.01em' },
            h2: { fontFamily: FONT_DISPLAY, fontSize: '1.375rem', lineHeight: '1.75rem', fontWeight: 700 },
            h3: { fontFamily: FONT_BODY, fontSize: '1.125rem', lineHeight: '1.5rem', fontWeight: 600 },
            h4: { fontFamily: FONT_DISPLAY, fontSize: '1.875rem', lineHeight: '2.25rem', fontWeight: 700, letterSpacing: '-0.01em' },
            h5: { fontFamily: FONT_DISPLAY, fontSize: '1.375rem', lineHeight: '1.75rem', fontWeight: 700 },
            h6: { fontFamily: FONT_BODY, fontSize: '1.125rem', lineHeight: '1.5rem', fontWeight: 600 },
            title: { fontFamily: FONT_BODY, fontSize: '1rem', lineHeight: '1.375rem', fontWeight: 600 },
            subtitle1: { fontSize: '1rem', lineHeight: '1.375rem', fontWeight: 600 },
            subtitle2: { fontSize: '0.875rem', lineHeight: '1.25rem', fontWeight: 600 },
            body1: { fontSize: '0.9375rem', lineHeight: '1.375rem' },
            body2: { fontSize: '0.8125rem', lineHeight: '1.125rem' },
            caption: { fontSize: '0.75rem', lineHeight: '1rem', fontWeight: 600, letterSpacing: '0.04em' },
            overline: { fontSize: '0.75rem', lineHeight: '1rem', fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase' },
            number: { fontFamily: FONT_DISPLAY, fontWeight: 700, fontVariantNumeric: 'tabular-nums', lineHeight: 1 },
            button: { fontSize: '0.875rem', lineHeight: '1.25rem', fontWeight: 600, textTransform: 'none', letterSpacing: 0 },
        },
        transitions: {
            easing: { easeInOut: motion.easing, easeOut: motion.easingEnter, easeIn: motion.easing, sharp: motion.easing },
            duration: {
                shortest: motion.fast, shorter: motion.fast, short: motion.standard, standard: motion.standard,
                complex: motion.emphasized, enteringScreen: motion.emphasized, leavingScreen: Math.round(motion.emphasized * 0.7),
            },
        },
        components: {
            MuiCssBaseline: {
                styleOverrides: {
                    ':root': { colorScheme: mode },
                    html: { backgroundColor: c.bg },
                    body: {
                        backgroundColor: c.bg,
                        color: c.text,
                        WebkitFontSmoothing: 'antialiased',
                        MozOsxFontSmoothing: 'grayscale',
                        fontFeatureSettings: '"cv11", "ss01"',
                    },
                    '::selection': { backgroundColor: alpha(c.primary, 0.35), color: c.text },
                    '*': { scrollbarColor: `${c.border} transparent` },
                    ':focus-visible': focusRing,
                    // MUI keeps its own focus outlines off; the ring is the
                    // single, consistent focus style.
                    '.Mui-focusVisible': focusRing,
                    // Text fields show focus with their own 2px primary border.
                    '.MuiInputBase-input:focus-visible': { outline: 'none' },
                    // Inside scrolling or clipped containers the ring sits inside.
                    '.MuiTab-root.Mui-focusVisible, .MuiMenuItem-root.Mui-focusVisible, .MuiListItemButton-root.Mui-focusVisible, .MuiBottomNavigationAction-root.Mui-focusVisible, .MuiTab-root:focus-visible': { outlineOffset: -2 },
                    '@media (prefers-reduced-motion: reduce)': {
                        '*, *::before, *::after': {
                            animationDuration: '0.01ms !important',
                            animationIterationCount: '1 !important',
                            transitionDuration: '0.01ms !important',
                            scrollBehavior: 'auto !important',
                        },
                    },
                },
            },
            MuiTypography: {
                defaultProps: {
                    variantMapping: { display: 'h1', title: 'h3', number: 'span' },
                },
            },
            MuiButtonBase: { defaultProps: { disableRipple: false } },
            MuiButton: {
                defaultProps: { disableElevation: true },
                styleOverrides: {
                    root: {
                        borderRadius: radii.pill,
                        minHeight: 44,
                        paddingInline: 20,
                        whiteSpace: 'nowrap',
                        transition: `background-color ${motion.fast}ms ${motion.easing}, color ${motion.fast}ms ${motion.easing}, border-color ${motion.fast}ms ${motion.easing}, transform ${motion.fast}ms ${motion.easing}`,
                        '&:active': { transform: 'scale(0.98)' },
                        variants: [
                            // Tonal: the quiet secondary action.
                            {
                                props: { variant: 'tonal' },
                                style: {
                                    backgroundColor: c.surfaceRaised,
                                    color: c.text,
                                    border: `1px solid ${c.border}`,
                                    '&:hover': { backgroundColor: isDark ? c.surfaceOverlay : '#E8E5DE' },
                                    '&.Mui-disabled': { color: alpha(c.text, 0.38), backgroundColor: alpha(c.text, 0.06) },
                                },
                            },
                            // Danger: filled destructive actions.
                            {
                                props: { variant: 'contained', color: 'error' },
                                style: {
                                    backgroundColor: c.accentStrong,
                                    color: '#FFFFFF',
                                    '&:hover': { backgroundColor: isDark ? '#C3343A' : '#A81E24' },
                                },
                            },
                            {
                                props: { variant: 'outlined', color: 'inherit' },
                                style: { borderColor: c.border, '&:hover': { borderColor: c.textSecondary, backgroundColor: alpha(c.text, 0.05) } },
                            },
                            {
                                props: { variant: 'outlined', color: 'primary' },
                                style: { borderColor: alpha(c.primary, 0.5) },
                            },
                        ],
                    },
                    sizeSmall: { minHeight: 36, paddingInline: 14, fontSize: '0.8125rem' },
                    sizeLarge: { minHeight: 52, paddingInline: 28, fontSize: '0.9375rem' },
                    text: { paddingInline: 14 },
                    containedPrimary: { '&:hover': { backgroundColor: isDark ? '#FFC65A' : '#7F5100' } },
                },
            },
            MuiIconButton: {
                styleOverrides: {
                    root: {
                        transition: `background-color ${motion.fast}ms ${motion.easing}, transform ${motion.fast}ms ${motion.easing}`,
                        '&:active': { transform: 'scale(0.94)' },
                    },
                    sizeMedium: { width: 44, height: 44 },
                },
            },
            MuiFab: { styleOverrides: { root: { boxShadow: 'none' } } },
            MuiPaper: {
                defaultProps: { elevation: 0 },
                styleOverrides: {
                    root: { backgroundImage: 'none' },
                    rounded: { borderRadius: radii.md },
                    outlined: { borderColor: c.border },
                },
            },
            MuiCard: {
                defaultProps: { elevation: 0 },
                styleOverrides: {
                    root: {
                        backgroundColor: c.surface,
                        backgroundImage: 'none',
                        borderRadius: radii.md,
                        border: `1px solid ${isDark ? c.border : 'transparent'}`,
                        boxShadow: isDark ? 'none' : lightShadow,
                    },
                },
            },
            MuiCardActionArea: {
                styleOverrides: { focusHighlight: { backgroundColor: 'transparent' } },
            },
            MuiAppBar: {
                defaultProps: { elevation: 0, color: 'default' },
                styleOverrides: {
                    root: {
                        backgroundColor: alpha(c.surface, isDark ? 0.86 : 0.9),
                        backdropFilter: 'saturate(140%) blur(12px)',
                        color: c.text,
                        borderBottom: `1px solid ${c.border}`,
                        backgroundImage: 'none',
                    },
                },
            },
            MuiToolbar: { styleOverrides: { root: { minHeight: layout.topBarHeight, '@media (min-width:600px)': { minHeight: layout.topBarHeight } } } },
            MuiTabs: {
                styleOverrides: {
                    root: { minHeight: 48 },
                    indicator: { height: 3, borderRadius: '3px 3px 0 0', backgroundColor: c.primary },
                    scrollButtons: { '&.Mui-disabled': { opacity: 0.3 } },
                },
            },
            MuiTab: {
                styleOverrides: {
                    root: {
                        textTransform: 'none',
                        fontWeight: 600,
                        fontSize: '0.9375rem',
                        minHeight: 48,
                        minWidth: 0,
                        paddingInline: 16,
                        color: c.textSecondary,
                        '&.Mui-selected': { color: c.text },
                        '&:hover': { color: c.text },
                    },
                },
            },
            MuiChip: {
                styleOverrides: {
                    root: { borderRadius: radii.sm, fontWeight: 600 },
                    filled: { backgroundColor: c.surfaceRaised, color: c.text, '&.MuiChip-clickable:hover': { backgroundColor: c.surfaceOverlay } },
                    outlined: { borderColor: c.border },
                    label: { paddingInline: 10 },
                },
            },
            MuiDialog: {
                styleOverrides: {
                    paper: {
                        backgroundColor: c.surfaceOverlay,
                        backgroundImage: 'none',
                        borderRadius: radii.lg,
                        border: isDark ? `1px solid ${c.border}` : 'none',
                        boxShadow: isDark ? '0 24px 64px rgba(0,0,0,.5)' : lightShadow,
                    },
                },
            },
            MuiDialogTitle: { styleOverrides: { root: { fontFamily: FONT_DISPLAY, fontSize: '1.375rem', lineHeight: '1.75rem', fontWeight: 700, paddingTop: 20 } } },
            MuiDialogActions: { styleOverrides: { root: { padding: '12px 20px 20px', gap: 8 } } },
            MuiBackdrop: {
                styleOverrides: { root: { '&:not(.MuiBackdrop-invisible)': { backgroundColor: alpha('#05060A', isDark ? 0.72 : 0.45), backdropFilter: 'blur(2px)' } } },
            },
            MuiPopover: {
                styleOverrides: {
                    paper: { backgroundColor: c.surfaceRaised, border: `1px solid ${c.border}`, borderRadius: radii.md, boxShadow: isDark ? '0 16px 48px rgba(0,0,0,.45)' : lightShadow },
                },
            },
            MuiMenu: {
                styleOverrides: {
                    paper: { backgroundColor: c.surfaceRaised, border: `1px solid ${c.border}`, borderRadius: radii.md },
                    list: { padding: 6 },
                },
            },
            MuiMenuItem: {
                styleOverrides: {
                    root: {
                        borderRadius: radii.sm,
                        minHeight: 40,
                        fontSize: '0.9375rem',
                        '&.Mui-selected': { backgroundColor: alpha(c.primary, isDark ? 0.14 : 0.1) },
                    },
                },
            },
            MuiListItemIcon: { styleOverrides: { root: { color: c.textSecondary, minWidth: 36 } } },
            MuiListSubheader: {
                styleOverrides: { root: { backgroundColor: 'transparent', color: c.textMuted, fontSize: '0.75rem', fontWeight: 600, letterSpacing: '0.04em', lineHeight: '2rem' } },
            },
            MuiDivider: { styleOverrides: { root: { borderColor: c.border } } },
            MuiOutlinedInput: {
                styleOverrides: {
                    root: {
                        backgroundColor: c.surfaceRaised,
                        borderRadius: radii.sm,
                        '& .MuiOutlinedInput-notchedOutline': { borderColor: c.border },
                        '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: c.textMuted },
                        '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: c.primary, borderWidth: 2 },
                    },
                    input: { '&::placeholder': { color: c.textMuted, opacity: 1 } },
                },
            },
            MuiInputLabel: { styleOverrides: { root: { color: c.textSecondary, '&.Mui-focused': { color: c.primary } } } },
            MuiFormHelperText: { styleOverrides: { root: { color: c.textSecondary } } },
            MuiTooltip: {
                defaultProps: { arrow: false, enterDelay: 300 },
                styleOverrides: {
                    tooltip: {
                        backgroundColor: c.surfaceOverlay,
                        color: c.text,
                        border: `1px solid ${c.border}`,
                        fontSize: '0.75rem',
                        lineHeight: '1rem',
                        fontWeight: 500,
                        padding: '6px 10px',
                        borderRadius: radii.sm,
                        boxShadow: isDark ? '0 8px 24px rgba(0,0,0,.4)' : lightShadow,
                    },
                },
            },
            MuiSnackbarContent: {
                styleOverrides: { root: { backgroundColor: c.surfaceOverlay, color: c.text, borderRadius: radii.md, border: `1px solid ${c.border}` } },
            },
            MuiAlert: {
                styleOverrides: {
                    root: { borderRadius: radii.md, alignItems: 'center' },
                    standard: { backgroundColor: c.surfaceRaised, color: c.text, border: `1px solid ${c.border}` },
                    standardInfo: { '& .MuiAlert-icon': { color: c.info } },
                    standardSuccess: { '& .MuiAlert-icon': { color: c.success } },
                    standardWarning: { '& .MuiAlert-icon': { color: c.primary } },
                    standardError: { '& .MuiAlert-icon': { color: c.error } },
                },
            },
            MuiSkeleton: {
                defaultProps: { animation: 'wave' },
                styleOverrides: {
                    root: {
                        backgroundColor: c.surfaceRaised,
                        '@media (prefers-reduced-motion: reduce)': { '&::after': { display: 'none' } },
                    },
                    rounded: { borderRadius: radii.md },
                    wave: { '&::after': { background: `linear-gradient(90deg, transparent, ${alpha(c.text, isDark ? 0.05 : 0.5)}, transparent)` } },
                },
            },
            MuiAvatar: {
                styleOverrides: {
                    colorDefault: { backgroundColor: isDark ? c.surfaceOverlay : c.surfaceRaised, color: c.text, fontWeight: 600, border: `1px solid ${c.border}` },
                },
            },
            MuiBadge: { styleOverrides: { badge: { fontWeight: 700, fontVariantNumeric: 'tabular-nums' } } },
            MuiLink: { defaultProps: { underline: 'hover' } },
            MuiLinearProgress: { styleOverrides: { root: { borderRadius: radii.pill, backgroundColor: c.surfaceRaised }, bar: { borderRadius: radii.pill } } },
            MuiBottomNavigation: {
                styleOverrides: { root: { backgroundColor: c.surface, height: layout.bottomNavHeight } },
            },
            MuiBottomNavigationAction: {
                styleOverrides: {
                    root: {
                        color: c.textSecondary,
                        minWidth: 0,
                        paddingInline: 4,
                        '&.Mui-selected': { color: c.primary },
                    },
                    label: { fontSize: '0.75rem', fontWeight: 600, '&.Mui-selected': { fontSize: '0.75rem' } },
                },
            },
            MuiSwitch: { styleOverrides: { switchBase: { '&.Mui-checked + .MuiSwitch-track': { opacity: 0.7 } } } },
            MuiToggleButton: {
                styleOverrides: {
                    root: {
                        textTransform: 'none', fontWeight: 600, borderColor: c.border, color: c.textSecondary,
                        '&.Mui-selected': { backgroundColor: alpha(c.primary, isDark ? 0.16 : 0.12), color: c.text },
                    },
                },
            },
            MuiToggleButtonGroup: { styleOverrides: { root: { borderRadius: radii.pill } } },
        },
    });
};

export const darkTheme = buildTheme('dark');
export const lightTheme = buildTheme('light');
