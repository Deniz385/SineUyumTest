import React from 'react';
import { useTranslation } from 'react-i18next';
import { Box, Button, Typography } from '@mui/material';

// After a new release, an old tab tries to fetch a page chunk (lazy chunk)
// that no longer exists on the server; reloading the page fixes it.
const isChunkLoadError = (error) =>
    error?.name === 'ChunkLoadError' ||
    /dynamically imported module|Importing a module script failed|error loading dynamically imported/i
        .test(String(error?.message));

// The fallback is a function component so it can use the translation hook.
export const ErrorFallback = ({ outdated, onRetry }) => {
    const { t } = useTranslation();
    return (
        <Box role="alert" sx={{ textAlign: 'center', mt: 8, px: 2 }}>
            <Typography variant="h5" gutterBottom>
                {outdated ? t('errorBoundary.updatedTitle') : t('errorBoundary.errorTitle')}
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 3 }}>
                {outdated ? t('errorBoundary.updatedBody') : t('errorBoundary.errorBody')}
            </Typography>
            <Box sx={{ display: 'flex', gap: 1, justifyContent: 'center' }}>
                {!outdated && (
                    <Button variant="outlined" onClick={onRetry}>{t('common.retry')}</Button>
                )}
                <Button variant="contained" onClick={() => window.location.reload()}>{t('errorBoundary.reload')}</Button>
            </Box>
        </Box>
    );
};

// M13: a render error in a single component used to take the whole app down
// to a blank page. Now only the area inside this boundary is replaced with an
// error card; the navbar keeps working. The boundary resets itself when
// `resetKey` changes (e.g. navigating to another page).
export class ErrorBoundary extends React.Component {
    constructor(props) {
        super(props);
        this.state = { error: null };
    }

    static getDerivedStateFromError(error) {
        return { error };
    }

    componentDidCatch(error, info) {
        console.error('Screen error:', error, info?.componentStack);
    }

    componentDidUpdate(prevProps) {
        if (this.state.error && prevProps.resetKey !== this.props.resetKey) {
            this.setState({ error: null });
        }
    }

    render() {
        const { error } = this.state;
        if (!error) return this.props.children;

        return (
            <ErrorFallback
                outdated={isChunkLoadError(error)}
                onRetry={() => this.setState({ error: null })}
            />
        );
    }
}
