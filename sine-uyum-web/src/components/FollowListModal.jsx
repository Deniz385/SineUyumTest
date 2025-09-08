// sine-uyum-web/src/components/FollowListModal.jsx
import React from 'react';
import { Modal, Box, Typography, List, ListItem, ListItemText, CircularProgress, Divider } from '@mui/material';
import { Link } from 'react-router-dom';

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
  maxHeight: '70vh',
  overflowY: 'auto'
};

export const FollowListModal = ({ open, onClose, title, list, isLoading }) => {
  return (
    <Modal open={open} onClose={onClose}>
      <Box sx={style}>
        <Typography variant="h6" component="h2">{title}</Typography>
        <Divider sx={{ my: 2 }} />
        {isLoading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center' }}>
            <CircularProgress />
          </Box>
        ) : (
          <List>
            {list.length === 0 ? (
              <ListItem>
                <ListItemText primary="Gösterilecek kimse yok." />
              </ListItem>
            ) : (
              list.map(user => (
                <ListItem 
                  key={user.id} 
                  component={Link} 
                  to={`/profile/${user.id}`} 
                  onClick={onClose}
                  sx={{ color: 'inherit', textDecoration: 'none' }}
                >
                  <ListItemText primary={user.userName} />
                </ListItem>
              ))
            )}
          </List>
        )}
      </Box>
    </Modal>
  );
};