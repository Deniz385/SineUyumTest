import React from 'react';
import { Box, Button, Typography } from '@mui/material';

// Yeni sürüm yayınlandıktan sonra eski sekme, artık sunucuda olmayan bir
// sayfa parçasını (lazy chunk) istemeye çalışır; çözüm sayfayı yenilemektir.
const isChunkLoadError = (error) =>
    error?.name === 'ChunkLoadError' ||
    /dynamically imported module|Importing a module script failed|error loading dynamically imported/i
        .test(String(error?.message));

// M13: Tek bir bileşendeki render hatası önceden tüm uygulamayı beyaz ekrana
// düşürüyordu. Artık yalnızca bu sınırın içi bir hata kartıyla değişir;
// gezinti çubuğu çalışmaya devam eder. `resetKey` değişince (ör. başka sayfaya
// geçiş) sınır kendini sıfırlar.
export class ErrorBoundary extends React.Component {
    constructor(props) {
        super(props);
        this.state = { error: null };
    }

    static getDerivedStateFromError(error) {
        return { error };
    }

    componentDidCatch(error, info) {
        console.error('Ekran hatası:', error, info?.componentStack);
    }

    componentDidUpdate(prevProps) {
        if (this.state.error && prevProps.resetKey !== this.props.resetKey) {
            this.setState({ error: null });
        }
    }

    render() {
        const { error } = this.state;
        if (!error) return this.props.children;

        const outdated = isChunkLoadError(error);
        return (
            <Box role="alert" sx={{ textAlign: 'center', mt: 8, px: 2 }}>
                <Typography variant="h5" gutterBottom>
                    {outdated ? 'Uygulama güncellendi' : 'Bir şeyler ters gitti'}
                </Typography>
                <Typography color="text.secondary" sx={{ mb: 3 }}>
                    {outdated
                        ? 'Yeni sürümü kullanmak için sayfayı yenileyin.'
                        : 'Bu ekran açılırken beklenmeyen bir hata oluştu. Tekrar deneyebilir ya da sayfayı yenileyebilirsiniz.'}
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, justifyContent: 'center' }}>
                    {!outdated && (
                        <Button variant="outlined" onClick={() => this.setState({ error: null })}>Tekrar dene</Button>
                    )}
                    <Button variant="contained" onClick={() => window.location.reload()}>Sayfayı yenile</Button>
                </Box>
            </Box>
        );
    }
}
