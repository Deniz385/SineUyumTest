import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';

export const UserSearch = () => {
    const [searchTerm, setSearchTerm] = useState('');
    const navigate = useNavigate();

    const handleSearch = (e) => {
        e.preventDefault();
        if (searchTerm.trim()) {
            // Kullanıcıyı arama sonuçları sayfasına yönlendir
            navigate(`/search?query=${searchTerm}`);
            setSearchTerm(''); // Arama kutusunu temizle
        }
    };

    return (
        <form onSubmit={handleSearch} className="user-search-form">
            <input
                type="text"
                className="user-search-input"
                placeholder="Kullanıcı ara..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
            />
        </form>
    );
};