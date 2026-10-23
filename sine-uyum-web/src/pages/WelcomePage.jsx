import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Box, Button, LinearProgress, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import StarRoundedIcon from '@mui/icons-material/StarRounded';
import CompareArrowsRoundedIcon from '@mui/icons-material/CompareArrowsRounded';
import GroupsRoundedIcon from '@mui/icons-material/GroupsRounded';
import CheckRoundedIcon from '@mui/icons-material/CheckRounded';
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { useSnackbar } from '../context/SnackbarProvider';
import { SUPPORTED_LANGUAGES, changeLanguage } from '../i18n';
import { Wordmark } from '../components/ui/Wordmark';
import { PosterWall } from '../components/ui/PosterWall';
import { PosterCard } from '../components/ui/PosterCard';
import { PosterGridSkeleton } from '../components/ui/Skeletons';
import { RatingModal } from '../components/RatingModal';
import { PeopleList } from '../components/PeopleList';
import { ErrorState } from '../components/states/ErrorState';
import { fromTmdb } from '../components/ui/posterUtils';
import { apiErrorMessage } from '../components/ui/apiError';
import { readPosterWall, savePosterWall } from '../components/ui/posterWallCache';
import { dedupe, usePeopleSuggestions } from './discoverData';
import { marqueeText } from '../theme';
import { useTheme } from '@mui/material/styles';

const STEPS = ['welcome', 'rate', 'people', 'done'];
const RATINGS_GOAL = 5;

// Well-known films first (more likely seen), mixed with this week's popular.
const interleave = (a, b) => {
    const out = [];
    for (let i = 0; i < Math.max(a.length, b.length); i++) {
        if (a[i]) out.push(a[i]);
        if (b[i]) out.push(b[i]);
    }
    return dedupe(out);
};

const StepDots = ({ index }) => {
    const { t } = useTranslation();
    return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }} aria-label={t('welcome.stepOf', { step: index + 1, total: STEPS.length })} role="img">
            {STEPS.map((step, i) => (
                <Box key={step} sx={{ width: i === index ? 24 : 8, height: 8, borderRadius: 999, bgcolor: i <= index ? 'primary.main' : 'divider', transition: (theme) => `width ${theme.motion.standard}ms ${theme.motion.easing}` }} />
            ))}
        </Box>
    );
};

// The heading of each step takes focus when it appears (screen readers hear it).
const StepHeading = ({ children, variant = 'h1', sx }) => {
    const ref = useRef(null);
    useEffect(() => { ref.current?.focus(); }, []);
    return (
        <Typography ref={ref} tabIndex={-1} variant={variant} component="h1" sx={[{ outline: 'none' }, ...(Array.isArray(sx) ? sx : [sx])]}>
            {children}
        </Typography>
    );
};

const WelcomeStep = ({ onNext }) => {
    const { t, i18n } = useTranslation();
    const theme = useTheme();
    const benefits = [
        { icon: <StarRoundedIcon />, title: t('welcome.intro.benefit1Title'), body: t('welcome.intro.benefit1Body') },
        { icon: <CompareArrowsRoundedIcon />, title: t('welcome.intro.benefit2Title'), body: t('welcome.intro.benefit2Body') },
        { icon: <GroupsRoundedIcon />, title: t('welcome.intro.benefit3Title'), body: t('welcome.intro.benefit3Body') },
    ];
    return (
        <Box sx={{ maxWidth: 560, mx: 'auto', textAlign: 'center' }}>
            <Wordmark size={30} sx={{ display: 'inline-block', mb: 3 }} />
            <StepHeading variant="display" sx={{ fontSize: { xs: '2rem', sm: '2.5rem' }, lineHeight: { xs: '2.375rem', sm: '2.75rem' } }}>
                <Box component="span" sx={marqueeText(theme.palette.mode)}>
                    {t('welcome.intro.title')}
                </Box>
            </StepHeading>
            <Typography variant="body1" color="text.secondary" sx={{ mt: 1.5, fontSize: '1.0625rem', lineHeight: 1.5 }}>{t('welcome.intro.subtitle')}</Typography>
            <Box component="ul" sx={{ listStyle: 'none', p: 0, m: 0, mt: 4, display: 'grid', gap: 1.5, textAlign: 'left' }}>
                {benefits.map(({ icon, title, body }) => (
                    <Box component="li" key={title} sx={{ display: 'flex', gap: 2, alignItems: 'flex-start', p: 2, borderRadius: 1.5, bgcolor: 'background.paper', border: 1, borderColor: 'divider' }}>
                        <Box aria-hidden="true" sx={{ width: 40, height: 40, borderRadius: '50%', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'surface.raised', color: 'primary.main' }}>
                            {icon}
                        </Box>
                        <Box>
                            <Typography variant="subtitle1" component="p">{title}</Typography>
                            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25, fontSize: '0.875rem', lineHeight: 1.4 }}>{body}</Typography>
                        </Box>
                    </Box>
                ))}
            </Box>
            <Box sx={{ mt: 4, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1 }}>
                <Typography id="welcome-language" variant="body2" color="text.secondary">{t('language.label')}</Typography>
                <ToggleButtonGroup
                    exclusive
                    value={i18n.language}
                    onChange={(e, code) => { if (code) changeLanguage(code); }}
                    aria-labelledby="welcome-language"
                    size="small"
                >
                    {SUPPORTED_LANGUAGES.map((l) => (
                        <ToggleButton key={l.code} value={l.code} lang={l.code} sx={{ px: 2.5, borderRadius: 999 }} data-testid={`welcome-lang-${l.code}`}>
                            {l.label}
                        </ToggleButton>
                    ))}
                </ToggleButtonGroup>
            </Box>
            <Button variant="contained" size="large" endIcon={<ArrowForwardRoundedIcon />} onClick={onNext} sx={{ mt: 4, minWidth: 220 }}>
                {t('welcome.intro.start')}
            </Button>
        </Box>
    );
};

// Films to rate: well-known ones (top rated) mixed with popular ones.
// Loaded when the flow opens (they also make the poster wall) and again
// when the language changes, so titles follow it.
const useFilmsToRate = () => {
    const { i18n } = useTranslation();
    const [state, setState] = useState({ movies: null, error: false });
    const load = useCallback(async () => {
        setState({ movies: null, error: false });
        const [top, popular] = await Promise.allSettled([api.get('/api/v1/movies/top_rated'), api.get('/api/v1/movies/popular')]);
        const pick = (r) => (r.status === 'fulfilled' ? r.value.data?.results || [] : []);
        const list = interleave(pick(top), pick(popular)).map(fromTmdb);
        if (list.length) savePosterWall(list.map((m) => m.posterPath));
        setState({ movies: list, error: list.length === 0 });
    }, []);
    useEffect(() => { load(); }, [load, i18n.language]);
    return { ...state, reload: load };
};

const RateStep = ({ films, ratings, onRated, onNext }) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const { movies, error, reload: load } = films;
    const [active, setActive] = useState(null);
    const count = Object.keys(ratings).length;

    const rate = async (movie, rating) => {
        const before = ratings[movie.id];
        onRated(movie.id, rating);
        try {
            await api.post('/api/v1/ratings', { movieId: movie.id, rating });
        } catch (err) {
            onRated(movie.id, before);
            showSnackbar(apiErrorMessage(err, t('movie.rateFailed')), 'error');
        }
    };

    return (
        <Box sx={{ pb: 14 }}>
            <Box sx={{ maxWidth: 640, mb: 3 }}>
                <StepHeading>{t('welcome.rate.title')}</StepHeading>
                <Typography variant="body1" color="text.secondary" sx={{ mt: 1 }}>{t('welcome.rate.subtitle', { count: RATINGS_GOAL })}</Typography>
            </Box>
            {movies === null ? (
                <PosterGridSkeleton count={18} />
            ) : error ? (
                <ErrorState message={t('welcome.rate.loadError')} onRetry={load} />
            ) : (
                <Box component="ul" data-testid="welcome-grid" sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: { xs: 'repeat(3, minmax(0, 1fr))', sm: 'repeat(auto-fill, minmax(140px, 1fr))' }, gap: { xs: 1.5, sm: 2 } }}>
                    {movies.map((movie) => {
                        const rating = ratings[movie.id];
                        return (
                            <li key={movie.id}>
                                <PosterCard
                                    movie={movie}
                                    fluid
                                    onClick={() => setActive(movie)}
                                    ariaLabel={rating ? t('welcome.rate.ratedLabel', { title: movie.title, value: rating }) : t('welcome.rate.rateLabel', { title: movie.title })}
                                    aria-pressed={Boolean(rating)}
                                    overlay={rating ? (
                                        <Box sx={{ position: 'absolute', inset: 0, borderRadius: 1.5, boxShadow: (theme) => `inset 0 0 0 3px ${theme.palette.primary.main}`, background: 'linear-gradient(180deg, transparent 55%, rgba(11,12,16,.85))', display: 'flex', alignItems: 'flex-end', justifyContent: 'center', pb: 1 }}>
                                            <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, px: 1, py: 0.25, borderRadius: 999, bgcolor: 'primary.main', color: 'primary.contrastText', fontWeight: 700 }} className="tabular-nums">
                                                <StarRoundedIcon sx={{ fontSize: 16 }} aria-hidden="true" />{rating}
                                            </Box>
                                        </Box>
                                    ) : null}
                                />
                            </li>
                        );
                    })}
                </Box>
            )}
            <RatingModal
                open={Boolean(active)}
                movie={active}
                initialRating={active ? ratings[active.id] : 0}
                onClose={() => setActive(null)}
                onRate={rate}
            />
            <BottomBar>
                <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography variant="subtitle1" component="p" role="status" aria-live="polite" data-testid="welcome-progress" className="tabular-nums">
                        {t('welcome.rate.progress', { count: Math.min(count, RATINGS_GOAL), goal: RATINGS_GOAL })}
                    </Typography>
                    <LinearProgress variant="determinate" value={Math.min(100, (count / RATINGS_GOAL) * 100)} sx={{ mt: 0.75, height: 6, maxWidth: 240 }} aria-hidden="true" />
                </Box>
                <Button color="inherit" onClick={onNext} sx={{ color: 'text.secondary' }}>{t('welcome.skipStep')}</Button>
                <Button variant="contained" onClick={onNext} disabled={count < RATINGS_GOAL} endIcon={<ArrowForwardRoundedIcon />}>
                    {t('welcome.continue')}
                </Button>
            </BottomBar>
        </Box>
    );
};

const BottomBar = ({ children }) => (
    <Box sx={{ position: 'fixed', left: 0, right: 0, bottom: 0, zIndex: 2, borderTop: 1, borderColor: 'divider', bgcolor: 'background.paper', pb: 'env(safe-area-inset-bottom)' }}>
        <Box sx={{ maxWidth: 1200 + 48, mx: 'auto', px: { xs: 2, sm: 3 }, py: 1.5, display: 'flex', alignItems: 'center', gap: { xs: 1, sm: 2 } }}>
            {children}
        </Box>
    </Box>
);

const PeopleStep = ({ onFollow, onNext }) => {
    const { t } = useTranslation();
    const { status, basis, items, reload } = usePeopleSuggestions(12);
    return (
        <Box sx={{ maxWidth: 720, mx: 'auto', pb: 14 }}>
            <StepHeading>{t('welcome.people.title')}</StepHeading>
            <Typography variant="body1" color="text.secondary" sx={{ mt: 1, mb: 2 }}>
                {status === 'ready' && basis === 'taste' ? t('welcome.people.tasteSubtitle') : t('welcome.people.popularSubtitle')}
            </Typography>
            <Box sx={{ bgcolor: 'background.paper', border: 1, borderColor: 'divider', borderRadius: 1.5, px: 2 }}>
                <PeopleList status={status} items={items} reload={reload} onFollowChange={onFollow} />
            </Box>
            <BottomBar>
                <Box sx={{ flex: 1 }} />
                <Button color="inherit" onClick={onNext} sx={{ color: 'text.secondary' }}>{t('welcome.skipStep')}</Button>
                <Button variant="contained" onClick={onNext} endIcon={<ArrowForwardRoundedIcon />}>{t('welcome.continue')}</Button>
            </BottomBar>
        </Box>
    );
};

const DoneStep = ({ rated, followed, onFinish }) => {
    const { t } = useTranslation();
    return (
        <Box sx={{ maxWidth: 520, mx: 'auto', textAlign: 'center', pt: { xs: 4, md: 8 } }}>
            <Box aria-hidden="true" sx={{ width: 72, height: 72, mx: 'auto', mb: 3, borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'surface.raised', border: 1, borderColor: 'divider', color: 'success.main' }}>
                <CheckRoundedIcon sx={{ fontSize: 36 }} />
            </Box>
            <StepHeading>{t('welcome.done.title')}</StepHeading>
            <Typography variant="body1" color="text.secondary" sx={{ mt: 1.5 }}>{t('welcome.done.body')}</Typography>
            {(rated > 0 || followed > 0) && (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }} data-testid="welcome-summary">
                    {[rated > 0 && t('welcome.done.rated', { count: rated }), followed > 0 && t('welcome.done.followed', { count: followed })].filter(Boolean).join(' · ')}
                </Typography>
            )}
            <Button variant="contained" size="large" onClick={onFinish} sx={{ mt: 4, minWidth: 220 }} endIcon={<ArrowForwardRoundedIcon />}>
                {t('welcome.done.cta')}
            </Button>
        </Box>
    );
};

/**
 * The first-run welcome (design system §8), shown once to new accounts:
 * welcome + language → rate a few films → people like you → done. Every step
 * can be skipped; finishing or skipping the whole flow calls
 * POST /account/onboarding/complete.
 */
export const WelcomePage = () => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { user, completeOnboarding } = useAuth();
    const [step, setStep] = useState('welcome');
    const [ratings, setRatings] = useState({});
    const [followed, setFollowed] = useState(0);
    const [finished] = useState(() => user?.onboardingCompleted === true);
    const completing = useRef(false);
    const index = STEPS.indexOf(step);
    const ratedCount = useMemo(() => Object.keys(ratings).length, [ratings]);
    const films = useFilmsToRate();
    const wall = useMemo(() => (films.movies?.length ? films.movies.map((m) => m.posterPath).filter(Boolean) : readPosterWall()), [films.movies]);

    const finish = useCallback(async () => {
        if (!completing.current) {
            completing.current = true;
            await completeOnboarding();
        }
        navigate('/home', { replace: true });
    }, [completeOnboarding, navigate]);

    // Reaching "done" already counts as finished (even if the tab closes).
    useEffect(() => {
        if (step === 'done' && !completing.current) {
            completing.current = true;
            completeOnboarding();
        }
    }, [step, completeOnboarding]);

    useEffect(() => { window.scrollTo({ top: 0 }); }, [step]);

    // Already done (e.g. opened /welcome by hand): never show it again.
    if (finished) return <Navigate to="/home" replace />;

    const next = () => setStep(STEPS[Math.min(index + 1, STEPS.length - 1)]);
    const onRated = (movieId, rating) => setRatings((prev) => {
        const copy = { ...prev };
        if (rating) copy[movieId] = rating; else delete copy[movieId];
        return copy;
    });

    return (
        <Box sx={{ minHeight: '100vh', position: 'relative', color: 'text.primary' }} data-testid="welcome" data-step={step}>
            {step === 'welcome' && <PosterWall posters={wall} />}
            <Box sx={{ position: 'relative', zIndex: 1, maxWidth: 1200 + 48, mx: 'auto', px: { xs: 2, sm: 3 } }}>
                <Box component="header" sx={{ display: 'grid', gridTemplateColumns: '1fr auto 1fr', alignItems: 'center', gap: 2, py: 2, minHeight: 64 }}>
                    <Box>{step !== 'welcome' && <Wordmark size={20} />}</Box>
                    <StepDots index={index} />
                    <Box sx={{ justifySelf: 'end' }}>
                        {step !== 'done' && (
                            <Button color="inherit" onClick={finish} sx={{ color: 'text.secondary', px: { xs: 1, sm: 1.75 } }} data-testid="welcome-skip-all">{t('welcome.skipAll')}</Button>
                        )}
                    </Box>
                </Box>
                <Box component="main" sx={{ py: { xs: 2, md: step === 'welcome' ? 4 : 3 } }}>
                    {step === 'welcome' && <WelcomeStep onNext={next} />}
                    {step === 'rate' && <RateStep films={films} ratings={ratings} onRated={onRated} onNext={next} />}
                    {step === 'people' && <PeopleStep onNext={next} onFollow={(on) => setFollowed((n) => Math.max(0, n + (on ? 1 : -1)))} />}
                    {step === 'done' && <DoneStep rated={ratedCount} followed={followed} onFinish={finish} />}
                </Box>
            </Box>
        </Box>
    );
};
