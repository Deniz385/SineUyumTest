import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../theme/tokens.dart';
import 'compatibility_badge.dart';
import 'poster_image.dart';

/// Avatar, @name, an optional line under it, an optional compatibility
/// badge and a trailing action (usually a follow button). Opens the profile.
class UserRow extends StatelessWidget {
  const UserRow({
    super.key,
    required this.userId,
    required this.userName,
    this.imageUrl,
    this.subtitle,
    this.compatibility,
    this.commonMovieCount,
    this.trailing,
    this.onTap,
    this.avatarRadius = 22,
  });

  final String userId;
  final String userName;
  final String? imageUrl;
  final String? subtitle;
  final double? compatibility;
  final int? commonMovieCount;
  final Widget? trailing;
  final VoidCallback? onTap;
  final double avatarRadius;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final line = subtitle;
    return InkWell(
      onTap: onTap ?? () => context.push('/profile/$userId'),
      child: ConstrainedBox(
        constraints: const BoxConstraints(minHeight: 64),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpace.gutter,
            vertical: AppSpace.xs,
          ),
          child: Row(
            children: [
              UserAvatar(
                userName: userName,
                imageUrl: imageUrl,
                radius: avatarRadius,
              ),
              const SizedBox(width: AppSpace.sm),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      userName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleMedium,
                    ),
                    if (line != null && line.isNotEmpty)
                      Text(
                        line,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.bodySmall,
                      ),
                    if (compatibility != null) ...[
                      const SizedBox(height: 4),
                      CompatibilityBadge(
                        score: compatibility!,
                        commonMovieCount: commonMovieCount,
                      ),
                    ],
                  ],
                ),
              ),
              if (trailing != null) ...[
                const SizedBox(width: AppSpace.xs),
                trailing!,
              ],
            ],
          ),
        ),
      ),
    );
  }
}
