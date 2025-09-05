import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

export const ProfilePage = () => {
    const { userId } = useParams();
    
    const { token, user } = useAuth(); // AuthContext'ten giriş yapan kullanıcıyı alabiliriz

    const [profileData, setProfileData] = useState(null);
    const [compatibility, setCompatibility] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
 console.log("ProfilePage'e gelen userId:", userId);
    const isMyProfile = user?.id === userId; // Kendi profilinde olup olmadığını kontrol et

    useEffect(() => {
        const fetchProfileData = async () => {
            if (!token) return;

            setIsLoading(true);
            setError('');
            try {
                const profileResponse = await axios.get(`${API_URL}/api/profile/${userId}`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                setProfileData(profileResponse.data);

                // Kendi profilinde değilse uyumluluk skorunu çek
                if (!isMyProfile) {
                    const compatibilityResponse = await axios.get(`${API_URL}/api/compatibility/${userId}`, {
                        headers: { 'Authorization': `Bearer ${token}` }
                    });
                    setCompatibility(compatibilityResponse.data);
                }

            } catch (err) {
                console.error("Profil verisi alınamadı:", err);
                setError("Profil bilgileri yüklenirken bir hata oluştu.");
            } finally {
                setIsLoading(false);
            }
        };

        fetchProfileData();
    }, [userId, token, isMyProfile]);

    if (isLoading) return <div className="page-container">Yükleniyor...</div>;
    if (error) return <div className="page-container message error-message">{error}</div>;

    return (
        <div className="page-container">
            <h1>{profileData?.userName}'in Profili</h1>
            
            {/* --- UYUMLULUK BÖLÜMÜ GÜNCELLENDİ --- */}
            {!isMyProfile && compatibility && (
                <>
                    <div className="compatibility-score">
                        <h2>Uyum Puanınız: %{compatibility.compatibilityScore}</h2>
                        <p>({compatibility.commonMovieCount} ortak filme göre hesaplandı)</p>
                    </div>
                    {compatibility.commonMovies.length > 0 && (
                        <div className="common-movies-table">
                            <h3>Ortak Oylanan Filmler</h3>
                            <table>
                                <thead>
                                    <tr>
                                        <th>Film</th>
                                        <th>Sizin Puanınız</th>
                                        <th>Onun Puanı</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {compatibility.commonMovies.map(movie => (
                                        <tr key={movie.movieId}>
                                            <td>{movie.title}</td>
                                            <td>{movie.currentUserRating} / 10</td>
                                            <td>{movie.targetUserRating} / 10</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </>
            )}
            
            <hr />
            <h2>Oylanan Filmler ({profileData?.ratings.length || 0})</h2>
            <div className="rated-movies-grid">
                {/* ... bu kısım aynı kalıyor ... */}
                {profileData?.ratings.map(rating => (
                    <div key={rating.movieId} className="rated-movie-card">
                        <img 
                            src={rating.posterPath ? `${IMAGE_BASE_URL}${rating.posterPath}` : '/vite.svg'} 
                            alt={`${rating.title} afişi`} 
                        />
                        <div className="rated-movie-info">
                            <strong>{rating.title}</strong>
                            <span>Verilen Puan: {rating.rating}/10</span>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};