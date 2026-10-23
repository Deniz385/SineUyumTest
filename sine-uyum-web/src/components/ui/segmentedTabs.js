// Tabs drawn as a segmented control (design system: sorts). They keep the
// tab semantics and keyboard behaviour; only the look changes.
export const segmentedTabsSx = {
    minHeight: 40,
    '& .MuiTabs-indicator': { display: 'none' },
    '& .MuiTabs-flexContainer': { gap: 0.5 },
    '& .MuiTab-root': {
        minHeight: 36,
        borderRadius: 999,
        px: 2,
        py: 0.75,
        fontSize: '0.875rem',
        border: 1,
        borderColor: 'divider',
        '&.Mui-selected': { bgcolor: 'surface.raised', color: 'text.primary', borderColor: 'text.muted' },
    },
};
