import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Avatar, Box, Button, Typography } from '@mui/material';
import CheckRoundedIcon from '@mui/icons-material/CheckRounded';
import PersonAddAlt1RoundedIcon from '@mui/icons-material/PersonAddAlt1Rounded';
import api, { mediaUrl } from '../../api/axiosConfig';
import { useSnackbar } from '../../context/SnackbarProvider';
import { CompatibilityBadge } from './CompatibilityBadge';
import { clampLines } from './posterUtils';
import { apiErrorMessage } from './apiError';

/** A user's avatar: their photo, or the initial on a raised circle. */
export const UserAvatar = ({ user, size = 40, sx, ...rest }) => (
    <Avatar
        src={mediaUrl(user?.profileImageUrl)}
        alt=""
        sx={[{ width: size, height: size, fontSize: size * 0.42 }, ...(Array.isArray(sx) ? sx : [sx])]}
        {...rest}
    >
        {user?.userName?.charAt(0).toUpperCase()}
    </Avatar>
);

/**
 * Follow / Following toggle (POST or DELETE /api/v1/follow/{id}). The state
 * flips at once and rolls back with the server's message on failure.
 */
export const FollowButton = ({ user, following: initialFollowing = false, onChange, size = 'small', sx }) => {
    const { t } = useTranslation();
    const { showSnackbar } = useSnackbar();
    const [following, setFollowing] = useState(initialFollowing);
    const [busy, setBusy] = useState(false);

    const toggle = async () => {
        if (busy) return;
        const next = !following;
        setBusy(true);
        setFollowing(next);
        try {
            if (next) await api.post(`/api/v1/follow/${user.id}`);
            else await api.delete(`/api/v1/follow/${user.id}`);
            onChange?.(next);
        } catch (err) {
            setFollowing(!next);
            showSnackbar(apiErrorMessage(err, t('follow.failed')), 'error');
        } finally {
            setBusy(false);
        }
    };

    return (
        <Button
            size={size}
            variant={following ? 'tonal' : 'outlined'}
            color={following ? 'inherit' : 'primary'}
            startIcon={following ? <CheckRoundedIcon /> : <PersonAddAlt1RoundedIcon />}
            onClick={toggle}
            aria-pressed={following}
            aria-label={following ? t('follow.unfollowUser', { userName: user.userName }) : t('follow.followUser', { userName: user.userName })}
            data-testid="follow-button"
            sx={[{ flexShrink: 0 }, ...(Array.isArray(sx) ? sx : [sx])]}
        >
            {following ? t('follow.following') : t('follow.follow')}
        </Button>
    );
};

/**
 * A person in a list: avatar, @name, an optional line under it, an optional
 * compatibility badge and an action (usually a FollowButton) on the right.
 */
export const UserRow = ({ user, subtitle, detail, compatibility, action, avatarSize = 48, sx }) => (
    <Box
        data-testid="user-row"
        sx={[{ display: 'flex', alignItems: 'center', gap: 1.5, py: 1.25, minWidth: 0 }, ...(Array.isArray(sx) ? sx : [sx])]}
    >
        <Box component={Link} to={`/profile/${user.id}`} tabIndex={-1} aria-hidden="true" sx={{ borderRadius: '50%', flexShrink: 0 }}>
            <UserAvatar user={user} size={avatarSize} />
        </Box>
        <Box sx={{ flex: 1, minWidth: 0 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                <Typography
                    component={Link}
                    to={`/profile/${user.id}`}
                    variant="subtitle1"
                    noWrap
                    sx={{ color: 'text.primary', minWidth: 0, '&:hover': { textDecoration: 'underline' } }}
                >
                    @{user.userName}
                </Typography>
                {compatibility != null && <CompatibilityBadge value={compatibility} size="sm" />}
            </Box>
            {subtitle && <Typography variant="body2" color="text.secondary" noWrap>{subtitle}</Typography>}
            {detail && <Typography variant="body2" color="text.secondary" sx={{ ...clampLines(2), mt: 0.25 }}>{detail}</Typography>}
        </Box>
        {action}
    </Box>
);
