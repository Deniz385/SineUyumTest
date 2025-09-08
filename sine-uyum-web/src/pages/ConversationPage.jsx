// sine-uyum-web/src/pages/ConversationPage.jsx
import React, { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { Box, Typography, CircularProgress, TextField, IconButton, Avatar } from '@mui/material';
import SendIcon from '@mui/icons-material/Send';

const API_URL = 'https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev';

export const ConversationPage = () => {
    const { otherUserId } = useParams();
    const { token, user } = useAuth();
    const [messages, setMessages] = useState([]);
    const [otherUser, setOtherUser] = useState(null);
    const [newMessage, setNewMessage] = useState('');
    const [isLoading, setIsLoading] = useState(true);
    const messagesEndRef = useRef(null);

    const scrollToBottom = () => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    };

    useEffect(() => {
        const fetchThread = async () => {
            if (!token) return;
            try {
                const response = await axios.get(`${API_URL}/api/messages/thread/${otherUserId}`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                setMessages(response.data);
                if (response.data.length > 0) {
                    const firstMessage = response.data[0];
                    setOtherUser(firstMessage.senderId === user.id ? 
                        { id: firstMessage.recipientId, userName: firstMessage.recipientUsername, profileImageUrl: null } :
                        { id: firstMessage.senderId, userName: firstMessage.senderUsername, profileImageUrl: firstMessage.senderProfileImageUrl }
                    );
                } else {
                    // Eğer hiç mesaj yoksa, diğer kullanıcının bilgisini profilden çek
                    const profileResponse = await axios.get(`${API_URL}/api/profile/${otherUserId}`, { headers: { 'Authorization': `Bearer ${token}` } });
                    setOtherUser(profileResponse.data);
                }
            } catch (err) {
                console.error("Mesajlar yüklenemedi:", err);
            } finally {
                setIsLoading(false);
            }
        };

        fetchThread();
    }, [otherUserId, token, user.id]);

    useEffect(scrollToBottom, [messages]);

    const handleSendMessage = async (e) => {
        e.preventDefault();
        if (!newMessage.trim()) return;

        try {
            await axios.post(`${API_URL}/api/messages`, 
                { recipientId: otherUserId, content: newMessage },
                { headers: { 'Authorization': `Bearer ${token}` } }
            );
            
            // Yeni mesajı anında ekranda göster
            const sentMessage = {
                id: Date.now(), // Geçici bir key için
                senderId: user.id,
                senderUsername: user.username,
                content: newMessage,
                messageSent: new Date().toISOString()
            };
            setMessages(prev => [...prev, sentMessage]);
            setNewMessage('');
        } catch (err) {
            console.error("Mesaj gönderilemedi:", err);
        }
    };

    if (isLoading) return <CircularProgress />;

    return (
        <Box sx={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 120px)', width: '100%', maxWidth: '800px', margin: 'auto' }}>
            <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider' }}>
                <Typography variant="h6" component={Link} to={`/profile/${otherUserId}`} sx={{textDecoration: 'none', color: 'inherit'}}>
                    {otherUser?.userName || 'Sohbet'}
                </Typography>
            </Box>
            <Box sx={{ flexGrow: 1, overflowY: 'auto', p: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
                {messages.map(msg => (
                    <Box key={msg.id} sx={{ alignSelf: msg.senderId === user.id ? 'flex-end' : 'flex-start', maxWidth: '70%' }}>
                        <Box sx={{
                            bgcolor: msg.senderId === user.id ? 'primary.main' : 'grey.300',
                            color: msg.senderId === user.id ? 'primary.contrastText' : 'text.primary',
                            p: 1.5,
                            borderRadius: 2
                        }}>
                            <Typography variant="body1">{msg.content}</Typography>
                        </Box>
                        <Typography variant="caption" sx={{ display: 'block', textAlign: msg.senderId === user.id ? 'right' : 'left', color: 'text.secondary', mt: 0.5 }}>
                            {new Date(msg.messageSent).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
                        </Typography>
                    </Box>
                ))}
                <div ref={messagesEndRef} />
            </Box>
            <Box component="form" onSubmit={handleSendMessage} sx={{ p: 2, borderTop: 1, borderColor: 'divider', display: 'flex', gap: 1 }}>
                <TextField
                    fullWidth
                    variant="outlined"
                    placeholder="Mesaj yaz..."
                    value={newMessage}
                    onChange={(e) => setNewMessage(e.target.value)}
                />
                <IconButton type="submit" color="primary">
                    <SendIcon />
                </IconButton>
            </Box>
        </Box>
    );
};