import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal, Box, Typography, Rating, Button } from '@mui/material';

// Style that centres the modal
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
  const { t } = useTranslation();
  const [rating, setRating] = useState(0);

  const handleRate = () => {
    if (rating > 0) {
      onRate(movie, rating);
      onClose(); // Close the modal when done
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
    >
      <Box sx={style}>
        <Typography variant="h6" component="h2">
          {t('rating.title', { title: movie?.title })}
        </Typography>
        <Rating
          name="movie-rating"
          value={rating}
          onChange={(event, newValue) => {
            setRating(newValue);
          }}
          max={10} // Rate out of 10
          size="large"
          sx={{ my: 2 }} // Vertical spacing
        />
        <Button onClick={handleRate} variant="contained" disabled={rating === 0}>
          {t('rating.save')}
        </Button>
      </Box>
    </Modal>
  );
};