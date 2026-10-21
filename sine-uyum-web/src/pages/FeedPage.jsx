import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { Box, Button, Card, CardContent, CircularProgress, Stack, Typography } from '@mui/material';
import { AuthorLine, ReviewCard } from '../components/ReviewCard';

const POSTER = 'https://image.tmdb.org/t/p/w92';

const RatedItem = ({ item }) => (
    <Card variant="outlined">
        <CardContent>
            <AuthorLine author={item.author} rating={item.rating} compatibility={item.authorCompatibility} when={item.activityAt}>
                <Typography variant="body2" color="text.secondary">bir filmi puanladı</Typography>
            </AuthorLine>
            <Box component={Link} to={`/movie/${item.movie.id}`}
                sx={{ display: 'flex', gap: 1.5, alignItems: 'center', mt: 1.5, color: 'inherit', textDecoration: 'none' }}>
                {item.movie.posterPath && <img src={`${POSTER}${item.movie.posterPath}`} alt="" style={{ width: 46, borderRadius: 4 }} />}
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{item.movie.title}</Typography>
            </Box>
        </CardContent>
    </Card>
);

// Takip ettiklerinin (ve kendi) puanları ve incelemeleri.
export const FeedPage = () => {
    const [items, setItems] = useState(null);
    const [cursor, setCursor] = useState(null);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [error, setError] = useState(false);

    const load = useCallback(async (from) => {
        try {
            const res = await api.get('/api/v1/feed', { params: from ? { cursor: from } : {} });
            setItems((prev) => (from ? [...(prev ?? []), ...res.data.items] : res.data.items));
            setCursor(res.data.nextCursor);
            setError(false);
        } catch {
            setError(true);
            setItems((prev) => prev ?? []);
        }
    }, []);

    useEffect(() => { load(null); }, [load]);

    const loadMore = async () => {
        setIsLoadingMore(true);
        await load(cursor);
        setIsLoadingMore(false);
    };

    if (items === null) return <CircularProgress sx={{ mt: 6 }} />;

    return (
        <Box className="page-container" sx={{ maxWidth: 720, width: '100%' }}>
            <Typography variant="h4" component="h1" gutterBottom>Akış</Typography>
            {error && <Typography color="error" sx={{ mb: 2 }}>Akış yüklenemedi.</Typography>}
            {items.length === 0 && !error ? (
                <Box sx={{ mt: 4 }}>
                    <Typography variant="h6">Akışın henüz boş</Typography>
                    <Typography color="text.secondary" sx={{ mb: 2 }}>
                        Takip ettiğin kişilerin puanları ve incelemeleri burada görünür. Üstteki kullanıcı aramasıyla
                        sana uyumlu kişileri bul ya da bir filme ilk incelemeyi sen yaz.
                    </Typography>
                    <Button component={Link} to="/home" variant="contained">Filmlere göz at</Button>
                </Box>
            ) : (
                <Stack spacing={2}>
                    {items.map((item) => item.review
                        ? <ReviewCard key={`r-${item.review.id}`} review={item.review} showMovie
                            headline={<Typography variant="body2" color="text.secondary">bir inceleme yazdı</Typography>} />
                        : <RatedItem key={`p-${item.author.id}-${item.movie.id}`} item={item} />)}
                </Stack>
            )}
            {cursor && (
                <Button sx={{ mt: 2 }} onClick={loadMore} disabled={isLoadingMore}>
                    {isLoadingMore ? 'Yükleniyor...' : 'Daha fazla'}
                </Button>
            )}
        </Box>
    );
};
