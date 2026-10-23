import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { keyframes } from '@emotion/react';
import { Avatar, Box, Button, IconButton, MenuItem, TextField, Typography } from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';
import CloseIcon from '@mui/icons-material/Close';
import ShareIcon from '@mui/icons-material/Share';
import ReplayIcon from '@mui/icons-material/Replay';
import StarIcon from '@mui/icons-material/Star';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import TrendingDownIcon from '@mui/icons-material/TrendingDown';
import TrendingFlatIcon from '@mui/icons-material/TrendingFlat';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import MovieIcon from '@mui/icons-material/Movie';
import api, { mediaUrl } from '../api/axiosConfig';
import { darkTheme } from '../theme';
import { useSnackbar } from '../context/SnackbarProvider';
import { LoadingState } from '../components/states/LoadingState';
import { ErrorState } from '../components/states/ErrorState';
import { formatIsoDay, monthNames } from '../components/diaryDates';
import { monthName, weekdayName, yearInReviewPath } from '../components/yearInReview';
import { shareYearCard } from '../components/yearInReviewImage';

const SLIDE_MS = 6000;
// A press longer than this pauses the story instead of changing the slide.
const HOLD_MS = 250;
const POSTER = 'https://image.tmdb.org/t/p/w342';
const GOLD = '#ffd54f';
const SOFT = '#c9d3ff';

const fill = keyframes`from { width: 0%; } to { width: 100%; }`;
const fadeIn = keyframes`from { opacity: 0; transform: translateY(12px); } to { opacity: 1; transform: none; }`;

const BACKGROUNDS = [
    'radial-gradient(120% 80% at 85% 10%, #ff704366 0%, transparent 60%), linear-gradient(160deg, #2a0f45 0%, #120a2a 55%, #06243f 100%)',
    'radial-gradient(100% 70% at 10% 90%, #00e5ff40 0%, transparent 60%), linear-gradient(200deg, #0b1f3a 0%, #0a0f24 60%, #1d0b33 100%)',
    'radial-gradient(110% 70% at 90% 90%, #ff4f8b55 0%, transparent 60%), linear-gradient(170deg, #1a0b2e 0%, #2b0d3d 50%, #0b0b1f 100%)',
    'radial-gradient(120% 80% at 15% 15%, #7c4dff66 0%, transparent 60%), linear-gradient(190deg, #120a2a 0%, #071a33 100%)',
    'radial-gradient(120% 70% at 50% 0%, #ffb30055 0%, transparent 60%), linear-gradient(180deg, #1f1206 0%, #120a2a 60%, #050510 100%)',
];

const usePrefersReducedMotion = () => {
    const query = '(prefers-reduced-motion: reduce)';
    const [reduced, setReduced] = useState(() => window.matchMedia?.(query).matches ?? false);
    useEffect(() => {
        const media = window.matchMedia?.(query);
        if (!media) return undefined;
        const onChange = () => setReduced(media.matches);
        media.addEventListener('change', onChange);
        return () => media.removeEventListener('change', onChange);
    }, []);
    return reduced;
};

// A poster, or a film icon on a tile when there is none.
const Poster = ({ path, title, width }) => {
    const sx = {
        width, aspectRatio: '2 / 3', objectFit: 'cover', borderRadius: 1.5, flexShrink: 0,
        bgcolor: 'rgba(255,255,255,0.08)', boxShadow: '0 10px 30px rgba(0,0,0,0.45)',
    };
    if (path) return <Box component="img" src={`${POSTER}${path}`} alt={title} sx={{ ...sx, display: 'block' }} />;
    return (
        <Box sx={{ ...sx, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <MovieIcon sx={{ color: 'rgba(255,255,255,0.35)', fontSize: width / 2 }} />
        </Box>
    );
};

const Kicker = ({ children }) => (
    <Typography sx={{ color: SOFT, fontWeight: 700, letterSpacing: '0.12em', textTransform: 'uppercase', fontSize: '0.8rem', mb: 1 }}>
        {children}
    </Typography>
);

const Heading = ({ children }) => (
    <Typography component="h2" sx={{ fontWeight: 800, fontSize: { xs: '1.7rem', sm: '1.85rem' }, lineHeight: 1.15, mb: 2.5 }}>
        {children}
    </Typography>
);

const Big = ({ children, sx }) => (
    <Typography component="p" sx={[{ fontWeight: 900, lineHeight: 1, letterSpacing: '-0.03em' }, ...(Array.isArray(sx) ? sx : [sx])]}>
        {children}
    </Typography>
);

const gradientText = {
    background: `linear-gradient(90deg, ${GOLD}, #ff6e9c)`,
    WebkitBackgroundClip: 'text',
    backgroundClip: 'text',
    color: 'transparent',
};

// The story's slides for a recap, leaving out the ones without data.
const buildSlides = (data, { t, language, isCurrentYear, onShare, isSharing, onReplay }) => {
    const mine = data.isMine;
    const v = (keyMine, keyTheirs) => (mine ? keyMine : keyTheirs);
    const num = (n) => Number(n).toLocaleString(language);
    const fmt1 = (n) => Number(n).toLocaleString(language, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
    const { totals, identity } = data;
    const slides = [];

    slides.push({
        key: 'intro',
        content: (
            <>
                <Typography component="h1" sx={{ fontWeight: 800, fontSize: { xs: '2.2rem', sm: '2.4rem' }, lineHeight: 1.1 }}>
                    {mine ? t('yearInReview.intro.titleMine', { year: data.year }) : t('yearInReview.intro.titleTheirs', { year: data.year, userName: data.userName })}
                </Typography>
                <Big sx={[gradientText, { fontSize: { xs: '7rem', sm: '8rem' }, mt: 4 }]}>{num(totals.movies)}</Big>
                <Typography sx={{ fontSize: '1.3rem', fontWeight: 600, mt: 1 }}>
                    {t(v('yearInReview.intro.moviesMine', 'yearInReview.intro.moviesTheirs'), { count: totals.movies })}
                </Typography>
                {isCurrentYear && <Typography sx={{ color: SOFT, mt: 3 }}>{t('yearInReview.intro.soFar')}</Typography>}
            </>
        ),
    });

    const counters = ['diaryEntries', 'rewatches', 'ratings', 'reviews', 'likesReceived', 'commentsReceived']
        .filter((k) => totals[k] > 0);
    if (counters.length > 0) {
        slides.push({
            key: 'totals',
            content: (
                <>
                    <Heading>{t(v('yearInReview.totals.headingMine', 'yearInReview.totals.headingTheirs'))}</Heading>
                    <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5 }}>
                        {counters.map((k) => (
                            <Box key={k} data-testid={`yir-total-${k}`} sx={{ p: 2, borderRadius: 3, bgcolor: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.12)' }}>
                                <Big sx={{ fontSize: '2.6rem' }}>{num(totals[k])}</Big>
                                <Typography sx={{ color: SOFT, mt: 0.75, lineHeight: 1.25 }}>{t(`yearInReview.totals.${k}`, { count: totals[k] })}</Typography>
                            </Box>
                        ))}
                    </Box>
                </>
            ),
        });
    }

    if (data.busiestMonth) {
        const max = Math.max(1, ...data.months.map((m) => m.count));
        const narrow = monthNames(language, 'narrow');
        const busiest = data.busiestMonth;
        const weekday = data.favoriteWeekday;
        slides.push({
            key: 'rhythm',
            content: (
                <>
                    <Heading>{t(v('yearInReview.rhythm.headingMine', 'yearInReview.rhythm.headingTheirs'))}</Heading>
                    <Box role="img" aria-label={t('diary.chartLabel', { year: data.year })} data-testid="yir-chart"
                        sx={{ display: 'grid', gridTemplateColumns: 'repeat(12, 1fr)', gap: 0.75, alignItems: 'end' }}>
                        {data.months.map(({ month, count }) => (
                            <Box key={month} sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', minWidth: 0 }}>
                                <Typography variant="caption" sx={{ visibility: count ? 'visible' : 'hidden', color: SOFT }}>{count}</Typography>
                                <Box sx={{ height: 150, width: '100%', display: 'flex', alignItems: 'flex-end' }}>
                                    <Box sx={{
                                        width: '100%',
                                        height: count ? `${Math.max(5, (count / max) * 100)}%` : 3,
                                        borderRadius: '6px 6px 2px 2px',
                                        background: month === busiest.month ? `linear-gradient(180deg, ${GOLD}, #ff8a65)`
                                            : count ? 'linear-gradient(180deg, #8ea2ff, #5c6bc0)' : 'rgba(255,255,255,0.2)',
                                    }} />
                                </Box>
                                <Typography variant="caption" aria-hidden="true" sx={{ mt: 0.5, fontWeight: month === busiest.month ? 800 : 400 }}>
                                    {narrow[month - 1]}
                                </Typography>
                            </Box>
                        ))}
                    </Box>
                    <Typography sx={{ fontSize: '1.2rem', fontWeight: 600, mt: 3 }}>
                        {t(v('yearInReview.rhythm.busiestMine', 'yearInReview.rhythm.busiestTheirs'), { month: monthName(busiest.month, language), count: busiest.count })}
                    </Typography>
                    {weekday && (
                        <Typography sx={{ fontSize: '1.05rem', color: SOFT, mt: 1.5 }}>
                            {t(v('yearInReview.rhythm.weekdayMine', 'yearInReview.rhythm.weekdayTheirs'), { weekday: weekdayName(weekday.weekday, language), count: weekday.count })}
                        </Typography>
                    )}
                </>
            ),
        });
    }

    if (identity.topGenres.length > 0) {
        slides.push({
            key: 'genres',
            content: (
                <>
                    <Heading>{t(v('yearInReview.genres.headingMine', 'yearInReview.genres.headingTheirs'))}</Heading>
                    {identity.topGenres.slice(0, 5).map((g, i) => (
                        <Box key={g.name} data-testid="yir-genre" sx={{ mb: 2 }}>
                            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 1 }}>
                                <Typography sx={{ fontWeight: 700, fontSize: i === 0 ? '1.35rem' : '1.1rem' }}>{g.name}</Typography>
                                <Typography sx={{ fontWeight: 800, color: i === 0 ? GOLD : '#fff' }}>{t('yearInReview.genres.share', { share: g.share })}</Typography>
                            </Box>
                            <Box sx={{ height: 10, borderRadius: 5, bgcolor: 'rgba(255,255,255,0.12)', mt: 0.5, overflow: 'hidden' }}>
                                <Box sx={{ height: '100%', width: `${Math.max(3, g.share)}%`, borderRadius: 5, background: i === 0 ? `linear-gradient(90deg, ${GOLD}, #ff8a65)` : 'linear-gradient(90deg, #8ea2ff, #b388ff)' }} />
                            </Box>
                            <Typography variant="body2" sx={{ color: SOFT, mt: 0.5 }}>{t('yearInReview.genres.stat', { count: g.count, average: fmt1(g.average) })}</Typography>
                        </Box>
                    ))}
                </>
            ),
        });
    }

    if (data.topMovies.length > 0) {
        const favoriteId = identity.favorite?.movieId;
        slides.push({
            key: 'top',
            content: (
                <>
                    <Heading>{t(v('yearInReview.top.headingMine', 'yearInReview.top.headingTheirs'), { year: data.year })}</Heading>
                    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                        {data.topMovies.map((m, i) => {
                            const isFavorite = m.movieId === favoriteId;
                            return (
                                <Box key={m.movieId} data-testid="yir-top-movie" data-favorite={isFavorite || undefined} sx={{
                                    display: 'flex', alignItems: 'center', gap: 1.5, p: 0.75, pr: 1.5, borderRadius: 2,
                                    bgcolor: isFavorite ? 'rgba(255,213,79,0.12)' : 'rgba(255,255,255,0.05)',
                                    border: isFavorite ? `2px solid ${GOLD}` : '2px solid transparent',
                                }}>
                                    <Typography sx={{ width: 22, flexShrink: 0, textAlign: 'center', fontWeight: 900, color: isFavorite ? GOLD : SOFT }}>{i + 1}</Typography>
                                    <Poster path={m.posterPath} title={m.title} width={isFavorite ? 56 : 44} />
                                    <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                                        {isFavorite && <Typography variant="caption" sx={{ color: GOLD, fontWeight: 800, letterSpacing: '0.08em', textTransform: 'uppercase' }}>{t('yearInReview.top.favorite')}</Typography>}
                                        <Typography sx={{ fontWeight: 700, lineHeight: 1.2, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>{m.title}</Typography>
                                    </Box>
                                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25, flexShrink: 0 }}>
                                        <StarIcon sx={{ color: GOLD, fontSize: 18 }} />
                                        <Typography sx={{ fontWeight: 800 }}>{t('yearInReview.rating', { rating: m.rating })}</Typography>
                                    </Box>
                                </Box>
                            );
                        })}
                    </Box>
                </>
            ),
        });
    }

    const take = identity.contrarianTake;
    if (take && take.tmdbAverage != null) {
        const higher = take.rating > take.tmdbAverage;
        slides.push({
            key: 'contrarian',
            content: (
                <>
                    <Heading>{t(v('yearInReview.contrarian.headingMine', 'yearInReview.contrarian.headingTheirs'))}</Heading>
                    <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
                        <Poster path={take.posterPath} title={take.title} width={120} />
                        <Typography sx={{ fontWeight: 800, fontSize: '1.4rem', lineHeight: 1.2 }}>{take.title}</Typography>
                    </Box>
                    <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5, mt: 3 }}>
                        <Box sx={{ p: 2, borderRadius: 3, bgcolor: 'rgba(255,255,255,0.08)' }}>
                            <Typography sx={{ color: SOFT }}>{t('yearInReview.contrarian.everyone')}</Typography>
                            <Big sx={{ fontSize: '2.8rem', mt: 0.5 }}>{fmt1(take.tmdbAverage)}</Big>
                        </Box>
                        <Box sx={{ p: 2, borderRadius: 3, bgcolor: 'rgba(255,213,79,0.14)', border: `1px solid ${GOLD}` }}>
                            <Typography sx={{ color: GOLD }}>{mine ? t('yearInReview.contrarian.you') : `@${data.userName}`}</Typography>
                            <Big sx={{ fontSize: '2.8rem', mt: 0.5, color: GOLD }}>{num(take.rating)}</Big>
                        </Box>
                    </Box>
                    <Typography sx={{ fontSize: '1.15rem', fontWeight: 600, mt: 3 }}>
                        {t(higher
                            ? v('yearInReview.contrarian.higherMine', 'yearInReview.contrarian.higherTheirs')
                            : v('yearInReview.contrarian.lowerMine', 'yearInReview.contrarian.lowerTheirs'))}
                    </Typography>
                </>
            ),
        });
    }

    if (data.firstMovie || data.lastMovie || data.mostRewatched) {
        const dated = (label, entry, testId) => entry && (
            <Box data-testid={testId} sx={{ display: 'flex', gap: 1.5, alignItems: 'center', p: 1, borderRadius: 2, bgcolor: 'rgba(255,255,255,0.06)' }}>
                <Poster path={entry.movie.posterPath} title={entry.movie.title} width={56} />
                <Box sx={{ minWidth: 0 }}>
                    <Typography variant="caption" sx={{ color: SOFT, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase' }}>{label}</Typography>
                    <Typography sx={{ fontWeight: 700, lineHeight: 1.2 }}>{entry.movie.title}</Typography>
                    <Typography variant="body2" sx={{ color: SOFT }}>{formatIsoDay(entry.watchedOn, language, { day: 'numeric', month: 'long' })}</Typography>
                </Box>
            </Box>
        );
        const rewatched = data.mostRewatched;
        slides.push({
            key: 'bookends',
            content: (
                <>
                    <Heading>{t('yearInReview.bookends.heading')}</Heading>
                    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
                        {dated(t('yearInReview.bookends.first'), data.firstMovie, 'yir-first')}
                        {dated(t('yearInReview.bookends.last'), data.lastMovie, 'yir-last')}
                        {rewatched && (
                            <Box data-testid="yir-rewatched" sx={{ display: 'flex', gap: 1.5, alignItems: 'center', p: 1, borderRadius: 2, bgcolor: 'rgba(255,213,79,0.12)', border: `1px solid ${GOLD}` }}>
                                <Poster path={rewatched.movie.posterPath} title={rewatched.movie.title} width={56} />
                                <Box sx={{ minWidth: 0 }}>
                                    <Typography variant="caption" sx={{ color: GOLD, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase' }}>{t('yearInReview.bookends.rewatched')}</Typography>
                                    <Typography sx={{ fontWeight: 700, lineHeight: 1.2 }}>{rewatched.movie.title}</Typography>
                                    <Typography variant="body2" sx={{ color: GOLD }}>{t(v('yearInReview.bookends.timesMine', 'yearInReview.bookends.timesTheirs'), { count: rewatched.count })}</Typography>
                                </Box>
                            </Box>
                        )}
                    </Box>
                </>
            ),
        });
    }

    if (data.averageRating != null) {
        const prev = data.previousYearAverage;
        const diff = prev == null ? null : Math.round(data.averageRating * 10) - Math.round(prev * 10);
        const trend = diff == null ? null : diff > 0 ? 'higher' : diff < 0 ? 'lower' : 'same';
        const TrendIcon = { higher: TrendingUpIcon, lower: TrendingDownIcon, same: TrendingFlatIcon }[trend];
        slides.push({
            key: 'average',
            content: (
                <>
                    <Heading>{t(v('yearInReview.average.headingMine', 'yearInReview.average.headingTheirs'))}</Heading>
                    <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
                        <Big sx={[gradientText, { fontSize: { xs: '6.5rem', sm: '7.5rem' } }]}>{fmt1(data.averageRating)}</Big>
                        <Typography sx={{ fontSize: '1.8rem', fontWeight: 700, color: SOFT }}>/10</Typography>
                    </Box>
                    {trend ? (
                        <Box data-testid="yir-trend" data-trend={trend} sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', mt: 3 }}>
                            <TrendIcon sx={{ color: GOLD, fontSize: 32, flexShrink: 0 }} />
                            <Typography sx={{ fontSize: '1.15rem', fontWeight: 600 }}>
                                {t(`yearInReview.average.${trend}${mine ? 'Mine' : 'Theirs'}`, { previous: fmt1(prev), previousYear: data.year - 1 })}
                            </Typography>
                        </Box>
                    ) : (
                        <Typography sx={{ fontSize: '1.1rem', color: SOFT, mt: 3 }}>
                            {t(v('yearInReview.average.basisMine', 'yearInReview.average.basisTheirs'), { count: identity.totalRatings })}
                        </Typography>
                    )}
                </>
            ),
        });
    }

    const mate = data.tasteMate;
    if (mate) {
        slides.push({
            key: 'mate',
            content: (
                <Box sx={{ textAlign: 'center' }}>
                    <Heading>{t(v('yearInReview.mate.headingMine', 'yearInReview.mate.headingTheirs'))}</Heading>
                    <Avatar src={mediaUrl(mate.user.profileImageUrl)} alt="" sx={{ width: 128, height: 128, mx: 'auto', fontSize: '3.5rem', border: `4px solid ${GOLD}`, bgcolor: '#5c6bc0', color: '#fff' }}>
                        {mate.user.userName?.charAt(0).toUpperCase()}
                    </Avatar>
                    <Typography sx={{ fontWeight: 800, fontSize: '1.6rem', mt: 2 }}>@{mate.user.userName}</Typography>
                    <Big sx={[gradientText, { fontSize: { xs: '2.8rem', sm: '3.2rem' }, mt: 2 }]}>{t('yearInReview.mate.compatibility', { compatibility: num(Math.round(mate.compatibility)) })}</Big>
                    <Typography sx={{ color: SOFT, mt: 1 }}>{t('yearInReview.mate.common', { count: mate.commonCount })}</Typography>
                    <Button component={Link} to={`/profile/${mate.user.id}`} variant="outlined"
                        sx={{ mt: 3, pointerEvents: 'auto', color: '#fff', borderColor: 'rgba(255,255,255,0.6)', borderRadius: 999, px: 3 }}>
                        {t('yearInReview.mate.viewProfile')}
                    </Button>
                </Box>
            ),
        });
    }

    slides.push({
        key: 'finale',
        content: (
            <Box sx={{ textAlign: 'center' }}>
                <Kicker>{t(v('yearInReview.finale.kickerMine', 'yearInReview.finale.kickerTheirs'), { year: data.year })}</Kicker>
                <Typography component="p" sx={[gradientText, { fontWeight: 900, fontSize: { xs: '2.6rem', sm: '2.9rem' }, lineHeight: 1.05, mt: 1 }]}>
                    {identity.title}
                </Typography>
                {identity.titleDescription && <Typography sx={{ fontSize: '1.1rem', mt: 2, color: '#e3e8ff' }}>{identity.titleDescription}</Typography>}
                <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1.5, mt: 5 }}>
                    <Button variant="contained" color="warning" size="large" startIcon={<ShareIcon />} onClick={onShare} disabled={isSharing}
                        sx={{ pointerEvents: 'auto', borderRadius: 999, px: 4, fontWeight: 800 }}>
                        {t(v('yearInReview.finale.shareMine', 'yearInReview.finale.shareTheirs'))}
                    </Button>
                    <Button startIcon={<ReplayIcon />} onClick={onReplay} sx={{ pointerEvents: 'auto', color: '#fff' }}>
                        {t('yearInReview.finale.replay')}
                    </Button>
                </Box>
            </Box>
        ),
    });

    return slides;
};

// A user's year in movies as a full-screen story: tap the right half (or →)
// for the next slide, the left half (or ←) for the previous one, hold to
// pause, Esc to close. Slides advance on their own unless reduced motion is on.
export const YearInReviewPage = () => {
    const { t, i18n } = useTranslation();
    const language = i18n.language;
    const { userId, year: yearParam } = useParams();
    const navigate = useNavigate();
    const { showSnackbar } = useSnackbar();
    const reducedMotion = usePrefersReducedMotion();
    const requestedYear = /^\d{4}$/.test(yearParam ?? '') ? Number(yearParam) : null;

    // { status: 'loading' | 'ready' | 'error', data, error: { status, title } }
    const [load, setLoad] = useState({ status: 'loading' });
    const [attempt, setAttempt] = useState(0);
    const [index, setIndex] = useState(0);
    const [isHeld, setIsHeld] = useState(false);
    const [isPickerOpen, setIsPickerOpen] = useState(false);
    const [isSharing, setIsSharing] = useState(false);
    const pressStart = useRef(0);

    useEffect(() => {
        let cancelled = false;
        setLoad((prev) => ({ status: 'loading', data: prev.data }));
        api.get(`/api/v1/year-in-review/user/${userId}`, { params: requestedYear ? { year: requestedYear } : {} })
            .then((res) => {
                if (cancelled) return;
                setIndex(0);
                setLoad({ status: 'ready', data: res.data });
            })
            .catch((err) => {
                if (!cancelled) setLoad({ status: 'error', error: { status: err.response?.status, title: err.response?.data?.title } });
            });
        return () => { cancelled = true; };
    }, [userId, requestedYear, attempt, language]);

    const close = useCallback(() => navigate(`/profile/${userId}`), [navigate, userId]);

    const share = async () => {
        setIsSharing(true);
        try {
            const result = await shareYearCard(load.data);
            if (result === 'downloaded') showSnackbar(t('identity.downloaded'), 'success');
        } catch (e) {
            if (e?.name !== 'AbortError') showSnackbar(t('identity.imageFailed'), 'error');
        } finally {
            setIsSharing(false);
        }
    };

    const data = load.status === 'ready' ? load.data : null;
    const showStory = Boolean(data?.ready);
    const slides = showStory ? buildSlides(data, {
        t, language, isSharing,
        isCurrentYear: data.year === new Date().getFullYear(),
        onShare: share,
        onReplay: () => setIndex(0),
    }) : [];
    const count = slides.length;
    const current = Math.min(index, Math.max(0, count - 1));

    const go = useCallback((delta) => {
        setIndex((i) => Math.min(Math.max(i + delta, 0), Math.max(0, count - 1)));
    }, [count]);

    useEffect(() => {
        const onKey = (e) => {
            if (isPickerOpen || e.defaultPrevented) return;
            if (['INPUT', 'SELECT', 'TEXTAREA'].includes(e.target?.tagName)) return;
            if (e.key === 'ArrowRight') go(1);
            else if (e.key === 'ArrowLeft') go(-1);
            else if (e.key === 'Escape') close();
        };
        window.addEventListener('keydown', onKey);
        return () => window.removeEventListener('keydown', onKey);
    }, [go, close, isPickerOpen]);

    const isPaused = isHeld || isPickerOpen || isSharing;
    const autoplay = showStory && !reducedMotion;

    const tapHandlers = (delta) => ({
        onPointerDown: () => { pressStart.current = Date.now(); setIsHeld(true); },
        onPointerUp: () => setIsHeld(false),
        onPointerLeave: () => setIsHeld(false),
        onPointerCancel: () => setIsHeld(false),
        onContextMenu: (e) => e.preventDefault(),
        onClick: () => {
            const wasHold = pressStart.current && Date.now() - pressStart.current > HOLD_MS;
            pressStart.current = 0;
            if (!wasHold) go(delta);
        },
    });

    const yearOptions = data ? [...new Set([...data.availableYears, data.year])].sort((a, b) => b - a) : [];
    const restricted = load.error?.status === 403 || load.error?.status === 404;

    return (
        <ThemeProvider theme={darkTheme}>
            <Box sx={{
                position: 'fixed', inset: 0, zIndex: 1200, bgcolor: '#05030c', color: '#fff',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
            }}>
                <Box data-testid="yir-frame" sx={{
                    position: 'relative', overflow: 'hidden', width: '100%', height: '100%',
                    background: BACKGROUNDS[current % BACKGROUNDS.length], transition: 'background 0.4s',
                    userSelect: 'none', WebkitTouchCallout: 'none',
                    '@media (min-width: 600px)': {
                        width: 'auto', height: 'min(calc(100dvh - 48px), 920px)', aspectRatio: '9 / 16',
                        borderRadius: 4, boxShadow: '0 30px 80px rgba(0,0,0,0.6)',
                    },
                }}>
                    {/* Header: progress, year picker, close. */}
                    <Box sx={{ position: 'absolute', top: 0, left: 0, right: 0, zIndex: 3, px: 1.5, pt: 1.5, background: 'linear-gradient(180deg, rgba(0,0,0,0.45), transparent)' }}>
                        {showStory && (
                            <Box data-testid="yir-progress" sx={{ display: 'flex', gap: 0.5 }} aria-hidden="true">
                                {slides.map((s, i) => (
                                    <Box key={s.key} sx={{ flex: 1, height: 3, borderRadius: 2, bgcolor: 'rgba(255,255,255,0.3)', overflow: 'hidden' }}>
                                        {i === current && autoplay ? (
                                            <Box
                                                key={`run-${current}`}
                                                data-testid="yir-progress-active"
                                                onAnimationEnd={() => go(1)}
                                                sx={{ height: '100%', bgcolor: '#fff', animation: `${fill} ${SLIDE_MS}ms linear forwards`, animationPlayState: isPaused ? 'paused' : 'running' }}
                                            />
                                        ) : (
                                            <Box data-testid={i <= current ? 'yir-progress-done' : undefined} sx={{ height: '100%', bgcolor: '#fff', width: i <= current ? '100%' : '0%' }} />
                                        )}
                                    </Box>
                                ))}
                            </Box>
                        )}
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 1 }}>
                            <Typography sx={{ fontWeight: 800, letterSpacing: '0.04em', flexGrow: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                {data ? `SineUyum · @${data.userName}` : 'SineUyum'}
                            </Typography>
                            {data && yearOptions.length > 1 && (
                                <TextField
                                    select
                                    size="small"
                                    value={data.year}
                                    onChange={(e) => navigate(yearInReviewPath(userId, e.target.value), { replace: true })}
                                    slotProps={{
                                        htmlInput: { 'aria-label': t('yearInReview.year') },
                                        select: { onOpen: () => setIsPickerOpen(true), onClose: () => setIsPickerOpen(false) },
                                    }}
                                    sx={{ minWidth: 92, '& .MuiInputBase-root': { borderRadius: 999, bgcolor: 'rgba(0,0,0,0.25)' } }}
                                >
                                    {yearOptions.map((y) => <MenuItem key={y} value={y}>{y}</MenuItem>)}
                                </TextField>
                            )}
                            <IconButton onClick={close} aria-label={t('yearInReview.close')} sx={{ color: '#fff' }}>
                                <CloseIcon />
                            </IconButton>
                        </Box>
                    </Box>

                    {load.status === 'loading' && (
                        <Box sx={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center' }}>
                            <LoadingState />
                        </Box>
                    )}

                    {load.status === 'error' && (
                        <Box sx={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', px: 2 }}>
                            <ErrorState
                                message={load.error.title || t(restricted ? 'yearInReview.unavailable' : 'yearInReview.loadFailed')}
                                onRetry={restricted ? undefined : () => setAttempt((n) => n + 1)}
                            />
                        </Box>
                    )}

                    {data && !data.ready && load.status === 'ready' && (
                        <Box data-testid="yir-not-ready" sx={{ position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center', textAlign: 'center', px: 4, gap: 2 }}>
                            <HourglassEmptyIcon sx={{ fontSize: 64, color: GOLD }} />
                            <Typography component="h1" sx={{ fontWeight: 800, fontSize: '1.7rem', lineHeight: 1.2 }}>
                                {data.isMine ? t('yearInReview.notReady.titleMine', { year: data.year }) : t('yearInReview.notReady.titleTheirs', { year: data.year, userName: data.userName })}
                            </Typography>
                            <Typography sx={{ color: SOFT }}>
                                {t(data.isMine ? 'yearInReview.notReady.bodyMine' : 'yearInReview.notReady.bodyTheirs', { count: data.minMovies, year: data.year })}
                            </Typography>
                            <Typography sx={{ fontWeight: 700 }}>
                                {t('yearInReview.notReady.progress', { done: data.totals.movies, min: data.minMovies })}
                            </Typography>
                            {data.isMine && (
                                <Button component={Link} to={`/profile/${userId}`} variant="contained" color="warning" sx={{ borderRadius: 999, px: 3, mt: 1 }}>
                                    {t('yearInReview.notReady.toDiary')}
                                </Button>
                            )}
                        </Box>
                    )}

                    {showStory && (
                        <>
                            <Box component="button" type="button" aria-label={t('yearInReview.previous')} data-testid="yir-prev" {...tapHandlers(-1)}
                                sx={{ position: 'absolute', top: 0, bottom: 0, left: 0, width: '50%', zIndex: 1, border: 0, p: 0, m: 0, background: 'transparent', cursor: 'w-resize', touchAction: 'manipulation', WebkitTapHighlightColor: 'transparent', '&:focus-visible': { outline: `2px solid ${GOLD}`, outlineOffset: -4 } }} />
                            <Box component="button" type="button" aria-label={t('yearInReview.next')} data-testid="yir-next" {...tapHandlers(1)}
                                sx={{ position: 'absolute', top: 0, bottom: 0, right: 0, width: '50%', zIndex: 1, border: 0, p: 0, m: 0, background: 'transparent', cursor: 'e-resize', touchAction: 'manipulation', WebkitTapHighlightColor: 'transparent', '&:focus-visible': { outline: `2px solid ${GOLD}`, outlineOffset: -4 } }} />
                            <Box
                                key={slides[current].key}
                                role="group"
                                aria-roledescription={t('yearInReview.slideRole')}
                                aria-label={t('yearInReview.slideLabel', { current: current + 1, total: count })}
                                data-testid={`yir-slide-${slides[current].key}`}
                                sx={{
                                    position: 'absolute', inset: 0, zIndex: 2, pointerEvents: 'none',
                                    pt: '88px', pb: 4, px: { xs: 3, sm: 3.5 },
                                    display: 'flex', flexDirection: 'column', justifyContent: 'center', overflow: 'hidden',
                                    animation: reducedMotion ? 'none' : `${fadeIn} 0.45s ease-out`,
                                }}
                            >
                                {slides[current].content}
                            </Box>
                        </>
                    )}
                </Box>
            </Box>
        </ThemeProvider>
    );
};
