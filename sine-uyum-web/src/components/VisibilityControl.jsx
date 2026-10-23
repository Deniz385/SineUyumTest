import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box, FormLabel, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import LockIcon from '@mui/icons-material/Lock';
import PublicIcon from '@mui/icons-material/Public';
import { VISIBILITY } from './publicLists';

// Private / Public choice for a list, with who can see it.
export const VisibilityControl = ({ value, onChange }) => {
    const { t } = useTranslation();
    const current = value === VISIBILITY.public ? VISIBILITY.public : VISIBILITY.private;
    return (
        <Box sx={{ mt: 2, textAlign: 'left' }}>
            <FormLabel id="list-visibility-label" sx={{ display: 'block', mb: 1 }}>{t('watchlist.visibility.label')}</FormLabel>
            <ToggleButtonGroup
                exclusive
                fullWidth
                size="small"
                color="primary"
                value={current}
                onChange={(event, next) => { if (next) onChange(next); }}
                aria-labelledby="list-visibility-label"
            >
                <ToggleButton value={VISIBILITY.private} sx={{ gap: 1 }} data-testid="visibility-private">
                    <LockIcon fontSize="small" aria-hidden="true" />
                    {t('watchlist.visibility.private')}
                </ToggleButton>
                <ToggleButton value={VISIBILITY.public} sx={{ gap: 1 }} data-testid="visibility-public">
                    <PublicIcon fontSize="small" aria-hidden="true" />
                    {t('watchlist.visibility.public')}
                </ToggleButton>
            </ToggleButtonGroup>
            <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1 }} data-testid="visibility-help">
                {current === VISIBILITY.public ? t('watchlist.visibility.publicHelp') : t('watchlist.visibility.privateHelp')}
            </Typography>
        </Box>
    );
};
