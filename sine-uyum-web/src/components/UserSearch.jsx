import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, InputAdornment, TextField } from '@mui/material';
import PersonSearchIcon from '@mui/icons-material/PersonSearch';

// User search box in the (always dark) navbar; matches the movie search box.
export const UserSearch = () => {
    const [searchTerm, setSearchTerm] = useState('');
    const navigate = useNavigate();
    const { t } = useTranslation();
    const placeholder = t('search.usersPlaceholder');

    const handleSearch = (e) => {
        e.preventDefault();
        const query = searchTerm.trim();
        if (query) {
            // Go to the search results page
            navigate(`/search?query=${encodeURIComponent(query)}`);
            setSearchTerm(''); // Clear the search box
        }
    };

    return (
        <Box component="form" role="search" onSubmit={handleSearch} sx={{ width: '100%' }}>
            <TextField
                size="small"
                fullWidth
                placeholder={placeholder}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                slotProps={{
                    htmlInput: { 'aria-label': placeholder },
                    input: {
                        sx: { color: 'white' },
                        startAdornment: (
                            <InputAdornment position="start">
                                <PersonSearchIcon fontSize="small" sx={{ color: '#adb5bd' }} />
                            </InputAdornment>
                        ),
                    },
                }}
                sx={{
                    '& .MuiOutlinedInput-root': {
                        '& fieldset': { borderColor: '#6c757d' },
                        '&:hover fieldset': { borderColor: 'white' },
                    },
                }}
            />
        </Box>
    );
};
