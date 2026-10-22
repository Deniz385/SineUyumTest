import React from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material';

/**
 * Reusable confirmation dialog.
 * @param {boolean} open - Whether the dialog is open.
 * @param {function} onClose - Called when the dialog is closed (cancelled).
 * @param {function} onConfirm - Called when the confirm button is pressed.
 * @param {string} title - Dialog title.
 * @param {string} description - Explanatory text inside the dialog.
 */
export const ConfirmationDialog = ({ open, onClose, onConfirm, title, description }) => {
  const { t } = useTranslation();
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
        <Button onClick={onClose}>{t('common.cancel')}</Button>
        <Button onClick={onConfirm} color="primary" autoFocus>
          {t('common.confirm')}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

