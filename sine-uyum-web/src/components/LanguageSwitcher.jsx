import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
    Button, FormControl, IconButton, InputLabel, List, ListItem, ListItemButton, ListItemIcon, ListItemText,
    ListSubheader, Menu, MenuItem, Select, Tooltip
} from '@mui/material';
import TranslateIcon from '@mui/icons-material/Translate';
import CheckIcon from '@mui/icons-material/Check';
import { SUPPORTED_LANGUAGES, changeLanguage } from '../i18n';

// Language picker in four shapes:
//   "icon"   → icon button with a menu (desktop navbar)
//   "list"   → list items (mobile drawer)
//   "select" → form field (profile settings)
//   "text"   → small button with a menu (login / register screens)
// Languages are always shown in their own name.
export const LanguageSwitcher = ({ variant = 'icon', sx }) => {
    const { t, i18n } = useTranslation();
    const [anchorEl, setAnchorEl] = useState(null);
    const current = i18n.language;
    const currentLabel = SUPPORTED_LANGUAGES.find((l) => l.code === current)?.label ?? current;

    const choose = (code) => {
        setAnchorEl(null);
        if (code !== current) changeLanguage(code);
    };

    if (variant === 'select') {
        return (
            <FormControl fullWidth margin="normal" sx={sx}>
                <InputLabel id="language-select-label">{t('language.label')}</InputLabel>
                <Select
                    labelId="language-select-label"
                    label={t('language.label')}
                    value={current}
                    onChange={(e) => choose(e.target.value)}
                    inputProps={{ 'data-testid': 'language-select' }}
                >
                    {SUPPORTED_LANGUAGES.map((l) => (
                        <MenuItem key={l.code} value={l.code} lang={l.code}>{l.label}</MenuItem>
                    ))}
                </Select>
            </FormControl>
        );
    }

    if (variant === 'list') {
        return (
            <List sx={sx} subheader={<ListSubheader disableSticky>{t('language.label')}</ListSubheader>}>
                {SUPPORTED_LANGUAGES.map((l) => (
                    <ListItem key={l.code} disablePadding>
                        <ListItemButton selected={l.code === current} onClick={() => choose(l.code)} lang={l.code}>
                            <ListItemText primary={l.label} />
                        </ListItemButton>
                    </ListItem>
                ))}
            </List>
        );
    }

    const trigger = variant === 'text' ? (
        <Button size="small" color="inherit" startIcon={<TranslateIcon />} onClick={(e) => setAnchorEl(e.currentTarget)}
            aria-label={t('language.change')} sx={sx}>
            {currentLabel}
        </Button>
    ) : (
        <Tooltip title={t('language.change')}>
            <IconButton color="inherit" onClick={(e) => setAnchorEl(e.currentTarget)} aria-label={t('language.change')} sx={sx}>
                <TranslateIcon />
            </IconButton>
        </Tooltip>
    );

    return (
        <>
            {trigger}
            <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={() => setAnchorEl(null)}>
                {SUPPORTED_LANGUAGES.map((l) => (
                    <MenuItem key={l.code} selected={l.code === current} onClick={() => choose(l.code)} lang={l.code}>
                        <ListItemIcon>{l.code === current && <CheckIcon fontSize="small" />}</ListItemIcon>
                        {l.label}
                    </MenuItem>
                ))}
            </Menu>
        </>
    );
};
