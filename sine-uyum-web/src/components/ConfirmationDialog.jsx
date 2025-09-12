import React from 'react';
import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material';

/**
 * Yeniden kullanılabilir onaylama diyalog kutusu.
 * @param {boolean} open - Diyalogun açık olup olmadığını kontrol eder.
 * @param {function} onClose - Diyalog kapatıldığında (iptal edildiğinde) çalışacak fonksiyon.
 * @param {function} onConfirm - Onay butonuna basıldığında çalışacak fonksiyon.
 * @param {string} title - Diyalog başlığı.
 * @param {string} description - Diyalog içindeki açıklama metni.
 */
export const ConfirmationDialog = ({ open, onClose, onConfirm, title, description }) => {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      aria-labelledby="confirmation-dialog-title"
      aria-describedby="confirmation-dialog-description"
    >
      <DialogTitle id="confirmation-dialog-title">{title}</DialogTitle>
      <DialogContent>
        <DialogContentText id="confirmation-dialog-description">
          {description}
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>İptal</Button>
        <Button onClick={onConfirm} color="primary" autoFocus>
          Onayla
        </Button>
      </DialogActions>
    </Dialog>
  );
};

