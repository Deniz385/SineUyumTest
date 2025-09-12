import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, List, ListItem, ListItemText, ListItemAvatar, Avatar, Divider } from '@mui/material';

export const MessagesPage = () => {
    const { user } = useAuth();
    const [conversations, setConversations] = useState([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        const fetchConversations = async () => {
            if (!user) return;
            try {
                const response = await api.get(`/api/messages`);
                // DÜZELTME: Gelen verinin içindeki $values dizisini alıyoruz.
                setConversations(response.data.$values || response.data);
            } catch (err) {
                setError('Konuşmalar yüklenirken bir hata oluştu.');
                console.error(err);
            } finally {
                setIsLoading(false);
            }
        };

        fetchConversations();
    }, [user]);

    if (isLoading) return <CircularProgress />;
    if (error) return <Typography color="error">{error}</Typography>;

    return (
        <Box className="page-container" sx={{ maxWidth: '800px' }}>
            <Typography variant="h4" component="h1" gutterBottom>
                Mesajlar
            </Typography>
            <List sx={{ width: '100%', bgcolor: 'background.paper' }}>
                {conversations.length === 0 ? (
                    <ListItem>
                        <ListItemText primary="Hiç konuşmanız yok." />
                    </ListItem>
                ) : (
                    conversations.map((convo, index) => (
                        <React.Fragment key={convo.otherUserId}>
                            <ListItem 
                                alignItems="flex-start" 
                                component={Link} 
                                to={`/messages/${convo.otherUserId}`}
                                sx={{ textDecoration: 'none', color: 'inherit' }}
                            >
                                <ListItemAvatar>
                                    <Avatar src={convo.otherUserProfileImageUrl}>
                                        {convo.otherUserUsername?.charAt(0).toUpperCase()}
                                    </Avatar>
                                </ListItemAvatar>
                                <ListItemText
                                    primary={convo.otherUserUsername}
                                    secondary={
                                        <Typography
                                            sx={{ display: 'inline' }}
                                            component="span"
                                            variant="body2"
                                            color="text.primary"
                                        >
                                            {convo.lastMessageContent.substring(0, 100)}...
                                        </Typography>
                                    }
                                />
                            </ListItem>
                            {index < conversations.length - 1 && <Divider variant="inset" component="li" />}
                        </React.Fragment>
                    ))
                )}
            </List>
        </Box>
    );
};
