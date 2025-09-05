import React, { useState } from 'react';
import { Modal, Box, Typography, Rating, Button } from '@mui/material';

// Modal penceresinin ortalanması için stil objesi
const style = {
  position: 'absolute',
  top: '50%',
  left: '50%',
  transform: 'translate(-50%, -50%)',
  width: 400,
  bgcolor: 'background.paper',
  border: '2px solid #000',
  boxShadow: 24,
  p: 4,
  textAlign: 'center'
};

export const RatingModal = ({ movie, open, onClose, onRate }) => {
  const [rating, setRating] = useState(0);

  const handleRate = () => {
    if (rating > 0) {
      onRate(movie, rating);
      onClose(); // İşlem bitince modalı kapat
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
    >
      <Box sx={style}>
        <Typography variant="h6" component="h2">
          "{movie?.title}" filmini puanla
        </Typography>
        <Rating
          name="movie-rating"
          value={rating}
          onChange={(event, newValue) => {
            setRating(newValue);
          }}
          max={10} // Puanlamayı 10 üzerinden yap
          size="large"
          sx={{ my: 2 }} // Üst ve alt boşluk
        />
        <Button onClick={handleRate} variant="contained" disabled={rating === 0}>
          Puanı Kaydet
        </Button>
      </Box>
    </Modal>
  );
};