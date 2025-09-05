import React from 'react';
import { Alert } from '@mui/material';

/**
 * Başarı veya Hata mesajlarını göstermek için yeniden kullanılabilir Alert bileşeni.
 * @param {string} type - 'success' veya 'error' olabilir.
 * @param {string} message - Gösterilecek mesaj metni.
 */
export const AlertMessage = ({ type, message }) => {
  if (!message) return null;

  return (
    <Alert severity={type} sx={{ width: '100%', mb: 2 }}>
      {message}
    </Alert>
  );
};

