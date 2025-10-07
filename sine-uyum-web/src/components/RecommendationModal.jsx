import React from 'react';
import { Modal, Box, Typography, Grid, CircularProgress } from '@mui/material';
import { Link } from 'react-router-dom';

const style = {
  position: 'absolute',
  top: '50%',
  left: '50%',
  transform: 'translate(-50%, -50%)',
  width: '80%',
  maxWidth: 900,
  bgcolor: 'background.paper',
  border: '2px solid #000',
  boxShadow: 24,
  p: 4,
  maxHeight: '80vh',
  overflowY: 'auto'
};

const IMAGE_BASE_URL = 'https://image.tmdb.org/t/p/w200';

export const RecommendationModal = ({ open, onClose, recommendations, isLoading, targetUserName }) => {
  return (
    <Modal open={open} onClose={onClose}>
      <Box sx={style}>
        <Typography variant="h5" component="h2" gutterBottom>
          {targetUserName} ile Ortak Zevkinize Göre Film Önerileri
        </Typography>
        {isLoading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', my: 3 }}>
            <CircularProgress />
          </Box>
        ) : recommendations.length === 0 ? (
          <Typography sx={{ mt: 2 }}>
            İkinize de uygun bir öneri bulunamadı. Daha fazla ortak filmi yüksek puanla oylamayı deneyin.
          </Typography>
        ) : (
          <Grid container spacing={2} sx={{ mt: 2 }}>
            {recommendations.map(movie => (
              <Grid item xs={4} sm={3} md={2} key={movie.id}>
                <Link to={`/movie/${movie.id}`} onClick={onClose}>
                  <img
                    src={movie.poster_path ? `${IMAGE_BASE_URL}${movie.poster_path}` : '/vite.svg'}
                    alt={movie.title}
                    style={{ width: '100%', borderRadius: '8px' }}
                  />
                  <Typography variant="caption" display="block" align="center">
                    {movie.title}
                  </Typography>
                </Link>
              </Grid>
            ))}
          </Grid>
        )}
      </Box>
    </Modal>
  );
};