import React, { useState, useEffect } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const SearchResultsPage = () => {
    const [searchParams] = useSearchParams();
    const query = searchParams.get('query');
    const { token } = useAuth();

    const [users, setUsers] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [message, setMessage] = useState('');

    useEffect(() => {
        const fetchUsers = async () => {
            if (!query || !token) return;

            setIsLoading(true);
            setMessage('');
            setUsers([]);

            try {
                const response = await axios.get(`${API_URL}/api/account/search`, {
                    headers: { 'Authorization': `Bearer ${token}` },
                    params: { query }
                });
                setUsers(response.data);
                if (response.data.length === 0) {
                    setMessage(`'${query}' için sonuç bulunamadı.`);
                }
            } catch (error) {
                setMessage('Kullanıcılar aranırken bir hata oluştu.');
            } finally {
                setIsLoading(false);
            }
        };

        fetchUsers();
    }, [query, token]);

    return (
        <div className="page-container">
            <h1>Arama Sonuçları: "{query}"</h1>
            {isLoading && <p>Yükleniyor...</p>}
            {message && <p>{message}</p>}
            
            <div className="user-list">
                <ul>
                    {users.map(user => (
                        <li key={user.id}>
                            <Link to={`/profile/${user.id}`}>{user.userName}</Link>
                        </li>
                    ))}
                </ul>
            </div>
        </div>
    );
};