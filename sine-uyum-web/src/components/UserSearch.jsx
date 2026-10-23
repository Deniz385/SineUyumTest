import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, InputAdornment, TextField } from '@mui/material';
import PersonSearchRoundedIcon from '@mui/icons-material/PersonSearchRounded';

// People search (Discover › People): submits to the search results page.
export const UserSearch = ({ sx }) => {
    const [searchTerm, setSearchTerm] = useState('');
    const navigate = useNavigate();
    const { t } = useTranslation();
    const placeholder = t('search.usersPlaceholder');

    const handleSearch = (e) => {
        e.preventDefault();
        const query = searchTerm.trim();
        if (query) {
            navigate(`/search?query=${encodeURIComponent(query)}`);
            setSearchTerm('');
        }
    };

    return (
        <Box component="form" role="search" onSubmit={handleSearch} sx={[{ width: '100%' }, ...(Array.isArray(sx) ? sx : [sx])]}>
            <TextField
                size="small"
                fullWidth
                placeholder={placeholder}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                slotProps={{
                    htmlInput: { 'aria-label': placeholder, type: 'search', enterKeyHint: 'search' },
                    input: {
                        startAdornment: (
                            <InputAdornment position="start">
                                <PersonSearchRoundedIcon fontSize="small" sx={{ color: 'text.secondary' }} />
                            </InputAdornment>
                        ),
                    },
                }}
            />
        </Box>
    );
};
