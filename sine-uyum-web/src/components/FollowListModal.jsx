// sine-uyum-web/src/components/FollowListModal.jsx
import React from 'react';
import { Box, Dialog, DialogContent, DialogTitle, IconButton, Tooltip } from '@mui/material';
import PeopleOutlineIcon from '@mui/icons-material/PeopleOutlineRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import { useTranslation } from 'react-i18next';
import { EmptyState } from './states/EmptyState';
import { UserListSkeleton } from './ui/Skeletons';
import { UserRow } from './ui/UserRow';

// Followers / following of a profile.
export const FollowListModal = ({ open, onClose, title, list, isLoading }) => {
  const { t } = useTranslation();
  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth aria-labelledby="follow-list-title">
      <DialogTitle id="follow-list-title" sx={{ pr: 7 }}>{title}</DialogTitle>
      <Tooltip title={t('common.close')}>
        <IconButton onClick={onClose} aria-label={t('common.close')} sx={{ position: 'absolute', right: 12, top: 12 }}><CloseRoundedIcon /></IconButton>
      </Tooltip>
      <DialogContent sx={{ pt: 0 }}>
        {isLoading ? (
          <UserListSkeleton count={4} />
        ) : list.length === 0 ? (
          <EmptyState compact icon={PeopleOutlineIcon} title={t('follow.empty')} />
        ) : (
          <Box component="ul" sx={{ listStyle: 'none', m: 0, p: 0 }} onClick={(e) => { if (e.target.closest('a')) onClose(); }}>
            {list.map((user) => <li key={user.id}><UserRow user={user} avatarSize={40} /></li>)}
          </Box>
        )}
      </DialogContent>
    </Dialog>
  );
};
