import React, { useEffect, useState, useCallback } from 'react';
import { useParams, Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Box, Typography, Alert, Tab, Tabs, Card, Skeleton, ButtonBase } from '@mui/material';
import { alpha, useTheme } from '@mui/material/styles';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';
import PersonOffIcon from '@mui/icons-material/PersonOffRounded';
import StarBorderIcon from '@mui/icons-material/StarBorderRounded';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesomeRounded';
import EditRoundedIcon from '@mui/icons-material/EditRounded';
import BlockRoundedIcon from '@mui/icons-material/BlockRounded';
import RateReviewRoundedIcon from '@mui/icons-material/RateReviewRounded';
import MovieFilterRoundedIcon from '@mui/icons-material/MovieFilterRounded';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { RecommendationModal } from '../components/RecommendationModal';
import { FollowListModal } from '../components/FollowListModal';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { CompatibilityInsights } from '../components/CompatibilityInsights';
import { FilmIdentityDialog } from '../components/FilmIdentityDialog';
import { ProfileDiary } from '../components/ProfileDiary';
import { ProfilePublicLists } from '../components/ProfilePublicLists';
import { ReviewCard } from '../components/ReviewCard';
import { useSnackbar } from '../context/SnackbarProvider';
import { EmptyState } from '../components/states/EmptyState';
import { ErrorState } from '../components/states/ErrorState';
import { defaultRecapYear, yearInReviewPath } from '../components/yearInReview';
import { Page } from '../components/ui/Page';
import { PosterCard } from '../components/ui/PosterCard';
import { MovieRow } from '../components/ui/MovieRow';
import { CompatibilityBadge } from '../components/ui/CompatibilityBadge';
import { UserAvatar } from '../components/ui/UserRow';
import { FeedSkeleton } from '../components/ui/Skeletons';
import { tmdbImage } from '../components/ui/posterUtils';
import { apiErrorMessage } from '../components/ui/apiError';

const PROFILE_TABS = ['ratings', 'diary', 'lists', 'reviews'];
const REVIEW_PAGES = 3;

const Stat = ({ value, label, onClick, testId }) => {
    const content = (
        <>
            <Typography component="span" sx={{ typography: 'number', fontSize: '1.375rem', display: 'block', color: 'text.primary' }}>{value}</Typography>
            <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25 }}>{label}</Typography>
        </>
    );
    const sx = { textAlign: 'left', minWidth: 72, borderRadius: 1, px: 0.5, py: 0.25 };
    return onClick ? (
        <ButtonBase onClick={onClick} data-testid={testId} sx={[sx, { '&:hover .MuiTypography-root:last-of-type': { color: 'text.primary' } }]}>{content}</ButtonBase>
    ) : <Box data-testid={testId} sx={sx}>{content}</Box>;
};

// Reviews shown on a profile. The API lists reviews per film, so they are
// gathered from the feed (your own and the people you follow).
const ProfileReviews = ({ userId, userName, canSee }) => {
    const { t } = useTranslation();
    const [reviews, setReviews] = useState(null);
    const [error, setError] = useState(false);
    const [attempt, setAttempt] = useState(0);

    useEffect(() => {
        if (!canSee) return undefined;
        let cancelled = false;
        (async () => {
            setReviews(null);
            setError(false);
            try {
                const found = [];
                let cursor = null;
                for (let i = 0; i < REVIEW_PAGES; i++) {
                    const { data } = await api.get('/api/v1/feed', { params: { limit: 50, ...(cursor ? { cursor } : {}) } });
                    found.push(...(data.items || []).filter((item) => item.review && item.author?.id === userId).map((item) => item.review));
                    cursor = data.nextCursor;
                    if (!cursor) break;
                }
                if (!cancelled) setReviews(found);
            } catch {
                if (!cancelled) setError(true);
            }
        })();
        return () => { cancelled = true; };
    }, [userId, canSee, attempt]);

    if (!canSee) {
        return <EmptyState compact icon={RateReviewRoundedIcon} title={t('profile.reviewsFollowTitle', { userName })} description={t('profile.reviewsFollowBody')} />;
    }
    if (error) return <ErrorState compact message={t('reviews.loadFailed')} onRetry={() => setAttempt((n) => n + 1)} />;
    if (reviews === null) return <FeedSkeleton count={2} />;
    if (!reviews.length) return <EmptyState compact icon={RateReviewRoundedIcon} title={t('profile.noReviews')} />;
    return (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 680 }} data-testid="profile-reviews">
            {reviews.map((r) => <ReviewCard key={r.id} review={r} showMovie />)}
        </Box>
    );
};

const ProfileSkeleton = () => (
    <Box role="status" aria-busy="true" data-testid="profile-skeleton">
        <Skeleton variant="rectangular" sx={{ height: { xs: 160, md: 240 } }} />
        <Page sx={{ mt: -7 }}>
            <Skeleton variant="circular" width={120} height={120} />
            <Skeleton variant="text" sx={{ fontSize: '2rem', width: 240, mt: 1 }} />
            <Skeleton variant="text" sx={{ width: 320 }} />
        </Page>
    </Box>
);

export const ProfilePage = () => {
    const { t, i18n } = useTranslation();
    const theme = useTheme();
    const { userId } = useParams();
    const [searchParams, setSearchParams] = useSearchParams();
    const { user } = useAuth();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar();

    const [profileData, setProfileData] = useState(null);
    const [compatibility, setCompatibility] = useState(null);
    const [identity, setIdentity] = useState(null);
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
    const tabParam = searchParams.get('tab');
    const tab = PROFILE_TABS.includes(tabParam) ? tabParam : 'ratings';

    const fetchProfileData = useCallback(async () => {
        if (!user) return;
        setIsLoading(true);
        setError('');
        setCompatibility(null);
        setIdentity(null);
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

    // The identity title (pill) and favourite film (cover). Optional.
    const hasRatings = Boolean(profileData && !profileData.isRestricted && profileData.statistics?.totalRatings > 0);
    useEffect(() => {
        if (!hasRatings) return undefined;
        let cancelled = false;
        api.get(`/api/v1/profile/${userId}/identity`)
            .then((res) => { if (!cancelled) setIdentity(res.data); })
            .catch(() => {});
        return () => { cancelled = true; };
    }, [hasRatings, userId]);

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
            showSnackbar(apiErrorMessage(err, t('profile.followFailed')), 'error');
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

    const changeTab = (event, next) => {
        setSearchParams(next === 'ratings' ? {} : { tab: next }, { replace: true });
    };

    if (isLoading) return <ProfileSkeleton />;
    if (error === 'notFound') return <Page><EmptyState icon={PersonOffIcon} title={t('profile.notFound')} /></Page>;
    if (error) return <Page><ErrorState message={t('profile.loadError')} onRetry={fetchProfileData} /></Page>;

    const isRestricted = profileData?.isRestricted;
    const isBlockedByMe = profileData?.isBlockedByMe;
    const stats = isRestricted ? null : profileData?.statistics;
    const topRatedMovies = stats?.topRatedMovies || [];
    const ratings = profileData?.ratings || [];
    const coverPath = identity?.favorite?.posterPath || topRatedMovies[0]?.posterPath || ratings[0]?.posterPath || null;
    const bg = theme.palette.tokens.bg;
    const average = stats ? Number(stats.averageRating).toLocaleString(i18n.language, { maximumFractionDigits: 1 }) : null;

    return (
        <>
            {/* Cover: the favourite film's poster, blurred and darkened. */}
            <Box
                aria-hidden="true"
                data-testid="profile-cover"
                sx={{
                    position: 'relative', height: { xs: 150, md: 230 }, overflow: 'hidden',
                    background: `linear-gradient(135deg, ${alpha(theme.palette.primary.main, 0.35)}, ${alpha(theme.palette.accent.main, 0.25)})`,
                }}
            >
                {coverPath && (
                    <Box sx={{ position: 'absolute', inset: -40, backgroundImage: `url(${tmdbImage(coverPath, 'w780')})`, backgroundSize: 'cover', backgroundPosition: 'center 30%', filter: 'blur(28px) saturate(1.2)', transform: 'scale(1.1)' }} />
                )}
                <Box sx={{ position: 'absolute', inset: 0, background: `linear-gradient(180deg, ${alpha(bg, 0.35)} 0%, ${alpha(bg, 0.7)} 60%, ${bg} 100%)` }} />
            </Box>

            <Page sx={{ pt: 0, mt: { xs: -6, md: -8 }, position: 'relative' }}>
                <Box component="header" sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, alignItems: { xs: 'flex-start', md: 'flex-end' }, gap: { xs: 1.5, md: 3 } }}>
                    <UserAvatar
                        user={profileData}
                        size={128}
                        sx={{ width: { xs: 96, md: 128 }, height: { xs: 96, md: 128 }, fontSize: { xs: 40, md: 52 }, border: 4, borderColor: 'background.default', boxShadow: '0 8px 24px rgba(0,0,0,.35)' }}
                    />
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
                            <Typography variant="h1" component="h1" sx={{ overflowWrap: 'anywhere' }}>{profileData?.userName}</Typography>
                            {!isMyProfile && compatibility && (
                                <CompatibilityBadge value={compatibility.compatibilityScore} data-testid="profile-compat" />
                            )}
                        </Box>
                        {identity?.title && (
                            <ButtonBase
                                onClick={() => setIsIdentityOpen(true)}
                                data-testid="identity-pill"
                                aria-label={`${isMyProfile ? t('profile.myIdentity') : t('profile.identity')}: ${identity.title}`}
                                sx={{ mt: 1, px: 1.5, py: 0.5, borderRadius: 999, gap: 0.75, border: 1, borderColor: alpha(theme.palette.primary.main, 0.5), bgcolor: alpha(theme.palette.primary.main, 0.12), color: 'text.primary', fontWeight: 600, fontSize: '0.875rem' }}
                            >
                                <AutoAwesomeIcon sx={{ fontSize: 16, color: 'primary.main' }} aria-hidden="true" />
                                {identity.title}
                            </ButtonBase>
                        )}
                        {profileData?.bio && <Typography variant="body1" color="text.secondary" sx={{ mt: 1, maxWidth: '65ch' }}>{profileData.bio}</Typography>}
                    </Box>
                    <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                        {isMyProfile ? (
                            <>
                                <Button component={Link} to="/profile/edit" variant="tonal" startIcon={<EditRoundedIcon />}>{t('profile.editProfile')}</Button>
                                <Button component={Link} to={yearInReviewPath(userId, recapYear)} variant="contained" startIcon={<AutoAwesomeIcon />} data-testid="profile-recap">
                                    {t('profile.recapMine', { year: recapYear })}
                                </Button>
                            </>
                        ) : isBlockedByMe ? (
                            <Button variant="tonal" onClick={handleUnblock}>{t('profile.unblock')}</Button>
                        ) : (
                            <>
                                <Button variant={followStatus.isFollowing ? 'tonal' : 'contained'} onClick={handleFollowToggle} disabled={isFollowLoading} aria-pressed={followStatus.isFollowing}>
                                    {isFollowLoading ? t('common.processing') : (followStatus.isFollowing ? t('profile.unfollow') : t('profile.follow'))}
                                </Button>
                                <Button variant="tonal" startIcon={<ChatBubbleRoundedIcon />} onClick={() => navigate(`/messages/${userId}`)}>{t('profile.sendMessage')}</Button>
                                {!isRestricted && (
                                    <Button component={Link} to={yearInReviewPath(userId, recapYear)} variant="tonal" startIcon={<AutoAwesomeIcon />} data-testid="profile-recap">
                                        {t('profile.recap', { year: recapYear })}
                                    </Button>
                                )}
                                <Button color="secondary" startIcon={<BlockRoundedIcon />} onClick={() => setIsBlockDialogOpen(true)}>{t('profile.block')}</Button>
                            </>
                        )}
                    </Box>
                </Box>

                <Box sx={{ display: 'flex', gap: { xs: 2, sm: 4 }, mt: 3, flexWrap: 'wrap' }} data-testid="profile-stats">
                    {stats && <Stat value={stats.totalRatings} label={t('profile.statFilms')} />}
                    {stats && <Stat value={average} label={t('profile.statAverage')} />}
                    <Stat value={followStatus.followerCount} label={t('profile.followers')} onClick={isRestricted ? undefined : () => handleOpenFollowList('followers')} testId="stat-followers" />
                    <Stat value={followStatus.followingCount} label={t('profile.followingList')} onClick={isRestricted ? undefined : () => handleOpenFollowList('following')} testId="stat-following" />
                </Box>

                {isRestricted && (
                    <Alert severity="info" sx={{ my: 3 }}>
                        {isBlockedByMe ? t('profile.blockedByMe') : t('profile.private')}
                    </Alert>
                )}

                {!isMyProfile && compatibility && (
                    <Card component="section" aria-labelledby="compat-heading" sx={{ mt: 4, p: { xs: 2, md: 3 } }} data-testid="compatibility-card">
                        <Box sx={{ display: 'flex', gap: 2.5, alignItems: 'center', flexWrap: 'wrap' }}>
                            <CompatibilityBadge value={compatibility.compatibilityScore} variant="ring" size="lg" />
                            <Box sx={{ flex: '1 1 240px', minWidth: 0 }}>
                                <Typography id="compat-heading" variant="h2" component="h2">{t('profile.compatibilityScore', { score: compatibility.compatibilityScore })}</Typography>
                                <Typography variant="body1" color="text.secondary" sx={{ mt: 0.5 }}>{t('profile.compatibilityBasis', { count: compatibility.commonMovieCount })}</Typography>
                            </Box>
                            {compatibility.commonMovieCount > 0 && (
                                <Button variant="tonal" startIcon={<MovieFilterRoundedIcon />} onClick={handleFetchRecommendations}>{t('profile.getRecommendations')}</Button>
                            )}
                        </Box>
                        <CompatibilityInsights insights={compatibility.insights} otherUserId={userId} otherUserName={profileData?.userName} />
                    </Card>
                )}

                {topRatedMovies.length > 0 && (
                    <MovieRow
                        sx={{ mt: 4 }}
                        title={t('profile.topRated')}
                        size="S"
                        movies={topRatedMovies.map((m) => ({ id: m.movieId, title: m.title, posterPath: m.posterPath }))}
                        testId="profile-top-rated"
                    />
                )}

                {!isRestricted && (
                    <>
                        <Tabs value={tab} onChange={changeTab} aria-label={t('profile.tabsLabel')} variant="scrollable" scrollButtons={false} sx={{ mt: 4, mb: 3, borderBottom: 1, borderColor: 'divider' }}>
                            {PROFILE_TABS.map((value) => (
                                <Tab key={value} value={value} label={t(`profile.tabs.${value}`)} id={`profile-tab-${value}`} aria-controls={`profile-panel-${value}`} />
                            ))}
                        </Tabs>
                        <Box role="tabpanel" id={`profile-panel-${tab}`} aria-labelledby={`profile-tab-${tab}`}>
                            {tab === 'ratings' && (
                                <Box component="section" aria-label={t('profile.allRated', { count: ratings.length || 0 })}>
                                    <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{t('profile.allRated', { count: ratings.length || 0 })}</Typography>
                                    {ratings.length === 0 ? (
                                        <EmptyState compact icon={StarBorderIcon} title={t('profile.noRatings')} />
                                    ) : (
                                        <Box component="ul" data-testid="profile-ratings" sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: { xs: 'repeat(3, minmax(0, 1fr))', sm: 'repeat(auto-fill, minmax(130px, 1fr))' }, gap: { xs: 1.5, sm: 2 } }}>
                                            {ratings.map((rating) => (
                                                <li key={rating.movieId}>
                                                    <PosterCard movie={{ id: rating.movieId, title: rating.title, posterPath: rating.posterPath }} rating={rating.rating} fluid />
                                                </li>
                                            ))}
                                        </Box>
                                    )}
                                </Box>
                            )}
                            {tab === 'diary' && <ProfileDiary key={userId} userId={userId} isMine={isMyProfile} />}
                            {tab === 'lists' && (
                                <ProfilePublicLists key={`lists-${userId}`} userId={userId} isMine={isMyProfile} enabled={!isRestricted} userName={profileData?.userName} showEmpty />
                            )}
                            {tab === 'reviews' && (
                                <ProfileReviews userId={userId} userName={profileData?.userName} canSee={isMyProfile || followStatus.isFollowing} />
                            )}
                        </Box>
                    </>
                )}
            </Page>

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
