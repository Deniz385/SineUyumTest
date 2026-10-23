import React, { useEffect, useState, useCallback } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { Trans, useTranslation } from 'react-i18next';
import api, { mediaUrl } from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Button, Box, Typography, Avatar, Grid, Paper, Alert } from '@mui/material';
import MessageIcon from '@mui/icons-material/Message';
import StarIcon from '@mui/icons-material/Star';
import RateReviewIcon from '@mui/icons-material/RateReview';
import PlaylistPlayIcon from '@mui/icons-material/PlaylistPlay';
import PersonOffIcon from '@mui/icons-material/PersonOff';
import StarBorderIcon from '@mui/icons-material/StarBorder';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import { RecommendationModal } from '../components/RecommendationModal';
import { FollowListModal } from '../components/FollowListModal';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { CompatibilityInsights } from '../components/CompatibilityInsights';
import { FilmIdentityDialog } from '../components/FilmIdentityDialog';
import { ProfileDiary } from '../components/ProfileDiary';
import { ProfilePublicLists } from '../components/ProfilePublicLists';
import { useSnackbar } from '../context/SnackbarProvider';
import { LoadingState } from '../components/states/LoadingState';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { defaultRecapYear, yearInReviewPath } from '../components/yearInReview';

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

const StatCard = ({ icon, value, label }) => (
    <Grid size={{ xs: 6, sm: 4 }}>
        <Paper elevation={2} sx={{ p: 2, textAlign: 'center' }}>
            {icon}
            <Typography variant="h5" component="p" sx={{ fontWeight: 'bold' }}>{value}</Typography>
            <Typography variant="body2" color="text.secondary">{label}</Typography>
        </Paper>
    </Grid>
);

export const ProfilePage = () => {
    const { t } = useTranslation();
    const { userId } = useParams();
    const { user } = useAuth();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar();
    
    const [profileData, setProfileData] = useState(null);
    const [compatibility, setCompatibility] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    // 'notFound' (also a user who blocked you) or 'failed'.
    const [error, setError] = useState('');
    const [followStatus, setFollowStatus] = useState({ isFollowing: false, followerCount: 0, followingCount: 0 });
    const [isFollowLoading, setIsFollowLoading] = useState(false);
    const [isRecsModalOpen, setIsRecsModalOpen] = useState(false);
    const [recommendations, setRecommendations] = useState([]);
    const [isRecsLoading, setIsRecsLoading] = useState(false);
    const [modalState, setModalState] = useState({ open: false, type: '', title: '' });
    const [listData, setListData] = useState([]);
    const [isListLoading, setIsListLoading] = useState(false);
    const [isBlockDialogOpen, setIsBlockDialogOpen] = useState(false);
    const [isIdentityOpen, setIsIdentityOpen] = useState(false);

    const isMyProfile = user?.id === userId;
    const recapYear = defaultRecapYear();

    const fetchProfileData = useCallback(async () => {
        if (!user) return;
        setIsLoading(true);
        setError('');
        setCompatibility(null);
        try {
            const [profileRes, followStatusRes] = await Promise.all([
                api.get(`/api/v1/profile/${userId}`),
                api.get(`/api/v1/follow/${userId}/status`)
            ]);
            
            setProfileData(profileRes.data);
            setFollowStatus(followStatusRes.data);

            // On a private profile compatibility is hidden too (it derives from ratings), so it is not requested.
            if (!isMyProfile && !profileRes.data.isRestricted) {
                const compatibilityRes = await api.get(`/api/v1/compatibility/${userId}`);
                setCompatibility(compatibilityRes.data);
            }
        } catch (err) {
            // A user who blocked you looks as if they don't exist (404).
            setError(err.response?.status === 404 ? 'notFound' : 'failed');
        } finally {
            setIsLoading(false);
        }
    }, [userId, user, isMyProfile]);

    useEffect(() => {
        fetchProfileData();
    }, [fetchProfileData]);
    
    const handleFollowToggle = async () => {
        setIsFollowLoading(true);
        const url = `/api/v1/follow/${userId}`;
        try {
            if (followStatus.isFollowing) {
                await api.delete(url);
                setFollowStatus(prev => ({ ...prev, isFollowing: false, followerCount: prev.followerCount - 1 }));
            } else {
                await api.post(url, {});
                setFollowStatus(prev => ({ ...prev, isFollowing: true, followerCount: prev.followerCount + 1 }));
            }
        } catch (err) {
            showSnackbar(err.response?.data?.title || t('profile.followFailed'), 'error');
        } finally {
            setIsFollowLoading(false);
        }
    };

    const handleBlock = async () => {
        setIsBlockDialogOpen(false);
        try {
            await api.post(`/api/v1/blocks/${userId}`);
            showSnackbar(t('profile.blocked', { userName: profileData?.userName }), 'success');
            fetchProfileData();
        } catch {
            showSnackbar(t('profile.blockFailed'), 'error');
        }
    };

    const handleUnblock = async () => {
        try {
            await api.delete(`/api/v1/blocks/${userId}`);
            showSnackbar(t('profile.unblocked'), 'success');
            fetchProfileData();
        } catch {
            showSnackbar(t('profile.unblockFailed'), 'error');
        }
    };

    const handleFetchRecommendations = async () => {
        setIsRecsModalOpen(true);
        setIsRecsLoading(true);
        try {
            const response = await api.get(`/api/v1/compatibility/${userId}/recommendations`);
            setRecommendations(response.data);
        } catch {
            setRecommendations([]);
        } finally {
            setIsRecsLoading(false);
        }
    };

    const handleOpenFollowList = async (type) => {
        if (profileData?.isRestricted) return;
        const title = type === 'followers' ? t('profile.followers') : t('profile.followingList');
        setModalState({ open: true, type, title });
        setIsListLoading(true);
        try {
            const response = await api.get(`/api/v1/follow/${userId}/${type}`);
            setListData(response.data);
        } catch (err) {
            console.error(`Could not load the ${type} list:`, err);
        } finally {
            setIsListLoading(false);
        }
    };

    if (isLoading) return <LoadingState />;
    if (error === 'notFound') return <EmptyState icon={PersonOffIcon} title={t('profile.notFound')} />;
    if (error) return <ErrorState message={t('profile.loadError')} onRetry={fetchProfileData} />;

    const isRestricted = profileData?.isRestricted;
    const isBlockedByMe = profileData?.isBlockedByMe;
    const stats = isRestricted ? null : profileData?.statistics;
    const topRatedMovies = stats?.topRatedMovies || [];
    const ratings = profileData?.ratings || [];

    return (
        <>
            <div className="page-container" style={{maxWidth: '1200px'}}>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 3 }}>
                    <Avatar src={mediaUrl(profileData?.profileImageUrl)} sx={{ width: 120, height: 120, fontSize: '3.5rem' }}>
                        {profileData?.userName?.charAt(0).toUpperCase()}
                    </Avatar>
                    <Box sx={{ flexGrow: 1, textAlign: 'left' }}>
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                            <Typography variant="h4" component="h1">{profileData?.userName}</Typography>
                            {isMyProfile ? (
                                <Button component={Link} to="/profile/edit" variant="outlined">{t('profile.editProfile')}</Button>
                            ) : isBlockedByMe ? (
                                <Button variant="outlined" onClick={handleUnblock}>{t('profile.unblock')}</Button>
                            ) : (
                                <Box sx={{ display: 'flex', gap: 1 }}>
                                    <Button variant="contained" startIcon={<MessageIcon />} onClick={() => navigate(`/messages/${userId}`)}>{t('profile.sendMessage')}</Button>
                                    <Button variant={followStatus.isFollowing ? "outlined" : "contained"} onClick={handleFollowToggle} disabled={isFollowLoading}>
                                        {isFollowLoading ? t('common.processing') : (followStatus.isFollowing ? t('profile.unfollow') : t('profile.follow'))}
                                    </Button>
                                    <Button color="error" onClick={() => setIsBlockDialogOpen(true)}>{t('profile.block')}</Button>
                                </Box>
                            )}
                        </Box>
                        <Box sx={{ display: 'flex', gap: 3, mt: 1 }}>
                            <Typography onClick={() => handleOpenFollowList('followers')} sx={{ cursor: isRestricted ? 'default' : 'pointer' }}><Trans i18nKey="profile.followerCount" count={followStatus.followerCount} components={{ strong: <strong /> }} /></Typography>
                            <Typography onClick={() => handleOpenFollowList('following')} sx={{ cursor: isRestricted ? 'default' : 'pointer' }}><Trans i18nKey="profile.followingCount" count={followStatus.followingCount} components={{ strong: <strong /> }} /></Typography>
                        </Box>
                        {profileData?.bio && <Typography variant="body1" sx={{ mt: 2, fontStyle: 'italic', maxWidth: '75ch' }}>"{profileData.bio}"</Typography>}
                    </Box>
                </Box>
                
                {isRestricted && (
                    <Alert severity="info" sx={{ my: 3, textAlign: 'left' }}>
                        {isBlockedByMe
                            ? t('profile.blockedByMe')
                            : t('profile.private')}
                    </Alert>
                )}

                {/* Film identity and the yearly recap; hidden with the rest of a private profile. */}
                {!isRestricted && (
                    <Box sx={{ display: 'flex', justifyContent: 'center', flexWrap: 'wrap', gap: 1, mt: 2 }}>
                        {stats && stats.totalRatings > 0 && (
                            <Button variant="outlined" color="secondary" onClick={() => setIsIdentityOpen(true)}>
                                {isMyProfile ? t('profile.myIdentity') : t('profile.identity')}
                            </Button>
                        )}
                        <Button
                            component={Link}
                            to={yearInReviewPath(userId, recapYear)}
                            variant="contained"
                            color="secondary"
                            startIcon={<AutoAwesomeIcon />}
                            data-testid="profile-recap"
                        >
                            {isMyProfile ? t('profile.recapMine', { year: recapYear }) : t('profile.recap', { year: recapYear })}
                        </Button>
                    </Box>
                )}

                {stats && (
                    <Grid container spacing={2} sx={{ my: 3, justifyContent: 'center' }}>
                        <StatCard icon={<RateReviewIcon color="primary" sx={{mb:1}}/>} value={stats.totalRatings} label={t('profile.statRated')} />
                        <StatCard icon={<StarIcon color="warning" sx={{mb:1}}/>} value={`${stats.averageRating} / 10`} label={t('profile.statAverage')} />
                        <StatCard icon={<PlaylistPlayIcon color="secondary" sx={{mb:1}}/>} value={stats.totalMoviesInWatchlists} label={t('profile.statInLists')} />
                    </Grid>
                )}

                {!isMyProfile && compatibility && (
                    <>
                        <div className="compatibility-score">
                            <h2>{t('profile.compatibilityScore', { score: compatibility.compatibilityScore })}</h2>
                            <p>{t('profile.compatibilityBasis', { count: compatibility.commonMovieCount })}</p>
                            <CompatibilityInsights
                                insights={compatibility.insights}
                                otherUserId={userId}
                                otherUserName={profileData?.userName}
                            />
                            {compatibility.commonMovieCount > 0 && (
                                <Button variant="contained" onClick={handleFetchRecommendations} sx={{ mt: 2 }}>{t('profile.getRecommendations')}</Button>
                            )}
                        </div>
                    </>
                )}
                
                {topRatedMovies.length > 0 && (
                    <Box sx={{ my: 4, textAlign: 'left' }}>
                        <Typography variant="h5" component="h3" gutterBottom sx={{fontWeight: 'bold'}}>{t('profile.topRated')}</Typography>
                         <Box sx={{ display: 'flex', gap: 2, overflowX: 'auto', pb: 2 }}>
                            {topRatedMovies.map(movie => (
                                <Link to={`/movie/${movie.movieId}`} key={movie.movieId}>
                                    <img src={movie.posterPath ? `${IMAGE_BASE_URL}${movie.posterPath}` : '/vite.svg'} alt={movie.title} style={{ height: '210px', borderRadius: '8px' }}/>
                                </Link>
                            ))}
                        </Box>
                    </Box>
                )}

                <ProfileDiary key={userId} userId={userId} isMine={isMyProfile} />

                {/* A private profile's lists are hidden too (the API answers 403). */}
                <ProfilePublicLists key={`lists-${userId}`} userId={userId} isMine={isMyProfile} enabled={!isRestricted} />

                {!isRestricted && (
                    <>
                        <hr />
                        <Typography variant="h5" component="h3" sx={{textAlign: 'left', mt: 3}}>{t('profile.allRated', { count: ratings.length || 0 })}</Typography>
                        {ratings.length === 0 && (
                            <EmptyState compact icon={StarBorderIcon} title={t('profile.noRatings')} />
                        )}
                        <div className="rated-movies-grid">
                            {ratings.map(rating => (
                                <div key={rating.movieId} className="rated-movie-card">
                                    <img src={rating.posterPath ? `${IMAGE_BASE_URL}${rating.posterPath}` : '/vite.svg'} alt={t('profile.posterAlt', { title: rating.title })}/>
                                    <div className="rated-movie-info">
                                        <strong>{rating.title}</strong>
                                        <span>{t('profile.givenRating', { rating: rating.rating })}</span>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </>
                )}
            </div>
            
            {profileData && <RecommendationModal open={isRecsModalOpen} onClose={() => setIsRecsModalOpen(false)} recommendations={recommendations} isLoading={isRecsLoading} targetUserName={profileData.userName} />}
            <FilmIdentityDialog open={isIdentityOpen} onClose={() => setIsIdentityOpen(false)} userId={userId} isMine={isMyProfile} />
            <ConfirmationDialog
                open={isBlockDialogOpen}
                onClose={() => setIsBlockDialogOpen(false)}
                onConfirm={handleBlock}
                title={t('profile.blockConfirmTitle', { userName: profileData?.userName ?? t('profile.userFallback') })}
                description={t('profile.blockConfirmBody')}
            />
            <FollowListModal open={modalState.open} onClose={() => setModalState({ open: false, type: '', title: '' })} title={modalState.title} list={listData} isLoading={isListLoading} />
        </>
    );
};