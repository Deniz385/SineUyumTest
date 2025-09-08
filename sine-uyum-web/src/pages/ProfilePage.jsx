// sine-uyum-web/src/pages/ProfilePage.jsx
import React, { useEffect, useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Button, Box, Typography, Avatar } from '@mui/material';
import MessageIcon from '@mui/icons-material/Message';
import { RecommendationModal } from '../components/RecommendationModal';
import { FollowListModal } from '../components/FollowListModal';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';
const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

export const ProfilePage = () => {
    const { userId } = useParams();
    const { token, user } = useAuth();
    const navigate = useNavigate();
    
    const [profileData, setProfileData] = useState(null);
    const [compatibility, setCompatibility] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [followStatus, setFollowStatus] = useState({ isFollowing: false, followerCount: 0, followingCount: 0 });
    const [isFollowLoading, setIsFollowLoading] = useState(false);
    const [isRecsModalOpen, setIsRecsModalOpen] = useState(false);
    const [recommendations, setRecommendations] = useState([]);
    const [isRecsLoading, setIsRecsLoading] = useState(false);
    const [modalState, setModalState] = useState({ open: false, type: '', title: '' });
    const [listData, setListData] = useState([]);
    const [isListLoading, setIsListLoading] = useState(false);

    const isMyProfile = user?.id === userId;

    useEffect(() => {
        const fetchProfileData = async () => {
            if (!token) return;
            setIsLoading(true);
            setError('');
            try {
                const [profileRes, compatibilityRes, followStatusRes] = await Promise.all([
                    axios.get(`${API_URL}/api/profile/${userId}`, { headers: { 'Authorization': `Bearer ${token}` } }),
                    isMyProfile ? Promise.resolve(null) : axios.get(`${API_URL}/api/compatibility/${userId}`, { headers: { 'Authorization': `Bearer ${token}` } }),
                    axios.get(`${API_URL}/api/follow/${userId}/status`, { headers: { 'Authorization': `Bearer ${token}` } })
                ]);
                setProfileData(profileRes.data);
                setFollowStatus(followStatusRes.data);
                if (compatibilityRes) {
                    setCompatibility(compatibilityRes.data);
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
    
    const handleFollowToggle = async () => {
        setIsFollowLoading(true);
        const url = `${API_URL}/api/follow/${userId}`;
        try {
            if (followStatus.isFollowing) {
                await axios.delete(url, { headers: { 'Authorization': `Bearer ${token}` } });
                setFollowStatus(prev => ({ ...prev, isFollowing: false, followerCount: prev.followerCount - 1 }));
            } else {
                await axios.post(url, {}, { headers: { 'Authorization': `Bearer ${token}` } });
                setFollowStatus(prev => ({ ...prev, isFollowing: true, followerCount: prev.followerCount + 1 }));
            }
        } catch (err) {
            console.error("Takip işlemi başarısız:", err);
        } finally {
            setIsFollowLoading(false);
        }
    };

    const handleFetchRecommendations = async () => {
        setIsRecsModalOpen(true);
        setIsRecsLoading(true);
        try {
            const response = await axios.get(`${API_URL}/api/compatibility/${userId}/recommendations`, { headers: { 'Authorization': `Bearer ${token}` } });
            setRecommendations(response.data);
        } catch (err) {
            console.error("Öneriler alınamadı:", err);
            setRecommendations([]);
        } finally {
            setIsRecsLoading(false);
        }
    };

    const handleOpenFollowList = async (type) => {
        const title = type === 'followers' ? 'Takipçiler' : 'Takip Edilenler';
        setModalState({ open: true, type, title });
        setIsListLoading(true);
        try {
            const response = await axios.get(`${API_URL}/api/follow/${userId}/${type}`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            setListData(response.data);
        } catch (err) {
            console.error(`${title} listesi alınamadı:`, err);
        } finally {
            setIsListLoading(false);
        }
    };

    if (isLoading) return <div className="page-container">Yükleniyor...</div>;
    if (error) return <div className="page-container message error-message">{error}</div>;

    return (
        <>
            <div className="page-container">
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 3 }}>
                    <Avatar 
                        src={profileData?.profileImageUrl} 
                        sx={{ width: 100, height: 100, fontSize: '3rem' }}
                    >
                        {profileData?.userName?.charAt(0).toUpperCase()}
                    </Avatar>
                    <Box sx={{ flexGrow: 1 }}>
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                            <h1>{profileData?.userName}</h1>
                            {isMyProfile ? (
                                <Button component={Link} to="/profile/edit" variant="outlined">Profili Düzenle</Button>
                            ) : (
                                <Box sx={{ display: 'flex', gap: 1 }}>
                                    <Button 
                                        variant="contained" 
                                        startIcon={<MessageIcon />}
                                        onClick={() => navigate(`/messages/${userId}`)}
                                    >
                                        Mesaj Gönder
                                    </Button>
                                    <Button variant={followStatus.isFollowing ? "outlined" : "contained"} onClick={handleFollowToggle} disabled={isFollowLoading}>
                                        {isFollowLoading ? "İşleniyor..." : (followStatus.isFollowing ? "Takipten Çık" : "Takip Et")}
                                    </Button>
                                </Box>
                            )}
                        </Box>
                        <Box sx={{ display: 'flex', gap: 2, mt: 1 }}>
                            <Typography onClick={() => handleOpenFollowList('followers')} sx={{ cursor: 'pointer' }}>
                                <strong>{followStatus.followerCount}</strong> Takipçi
                            </Typography>
                            <Typography onClick={() => handleOpenFollowList('following')} sx={{ cursor: 'pointer' }}>
                                <strong>{followStatus.followingCount}</strong> Takip
                            </Typography>
                        </Box>
                    </Box>
                </Box>
                {profileData?.bio && (
                    <Typography variant="body1" sx={{ mb: 3, fontStyle: 'italic', maxWidth: '75ch', textAlign: 'left' }}>
                        "{profileData.bio}"
                    </Typography>
                )}

                {!isMyProfile && compatibility && (
                    <>
                        <div className="compatibility-score">
                            <h2>Uyum Puanınız: %{compatibility.compatibilityScore}</h2>
                            <p>({compatibility.commonMovieCount} ortak filme göre hesaplandı)</p>
                            {compatibility.commonMovieCount > 0 && (
                                <Button
                                    variant="contained"
                                    onClick={handleFetchRecommendations}
                                    sx={{ mt: 2 }}
                                >
                                    Ortak Film Önerileri Alın
                                </Button>
                            )}
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
            
            {profileData && <RecommendationModal
                open={isRecsModalOpen}
                onClose={() => setIsRecsModalOpen(false)}
                recommendations={recommendations}
                isLoading={isRecsLoading}
                targetUserName={profileData.userName}
            />}

            <FollowListModal
                open={modalState.open}
                onClose={() => setModalState({ open: false, type: '', title: '' })}
                title={modalState.title}
                list={listData}
                isLoading={isListLoading}
            />
        </>
    );
};