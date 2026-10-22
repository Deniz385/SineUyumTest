import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

export const UserSearch = () => {
    const [searchTerm, setSearchTerm] = useState('');
    const navigate = useNavigate();
    const { t } = useTranslation();

    const handleSearch = (e) => {
        e.preventDefault();
        if (searchTerm.trim()) {
            // Go to the search results page
            navigate(`/search?query=${searchTerm}`);
            setSearchTerm(''); // Clear the search box
        }
    };

    return (
        <form onSubmit={handleSearch} className="user-search-form">
            <input
                type="text"
                className="user-search-input"
                placeholder={t('search.usersPlaceholder')}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
            />
        </form>
    );
};