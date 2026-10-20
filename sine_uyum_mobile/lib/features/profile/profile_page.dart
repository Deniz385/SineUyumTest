import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/profile.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../widgets/movie_widgets.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key, required this.userId});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(userProfileProvider(userId));
    final isOwnProfile = ref.watch(sessionProvider).user?.id == userId;
    final loaded = profile.asData?.value;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profil'),
        actions: [
          if (isOwnProfile)
            IconButton(
              tooltip: 'Profili düzenle',
              icon: const Icon(Icons.edit_outlined),
              onPressed: () => context.push('/profile/edit'),
            ),
          if (!isOwnProfile && loaded != null && !loaded.isBlockedByMe)
            PopupMenuButton<String>(
              tooltip: 'Diğer',
              onSelected: (_) => _confirmBlock(context, ref, loaded),
              itemBuilder: (_) => const [
                PopupMenuItem(value: 'block', child: Text('Engelle')),
              ],
            ),
        ],
      ),
      body: profile.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(userProfileProvider(userId)),
        ),
        data: (profile) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(userProfileProvider(userId));
            ref.invalidate(followStatusProvider(userId));
            ref.invalidate(compatibilityProvider(userId));
            await ref.read(userProfileProvider(userId).future);
          },
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _Header(profile: profile),
              const SizedBox(height: 20),
              if (profile.isBlockedByMe)
                _BlockedNotice(userId: userId)
              else if (profile.isRestricted) ...[
                const _RestrictedNotice(),
                if (!isOwnProfile) ...[
                  const SizedBox(height: 20),
                  _SocialActions(
                    userId: userId,
                    userName: profile.userName,
                    showCompatibility: false,
                  ),
                ],
              ] else ...[
                _Stats(statistics: profile.statistics),
                if (!isOwnProfile) ...[
                  const SizedBox(height: 20),
                  _SocialActions(userId: userId, userName: profile.userName),
                ],
                const SizedBox(height: 24),
                ..._ratings(context, profile),
              ],
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _ratings(BuildContext context, UserProfile profile) => [
    Text(
      'Puanladığı filmler (${profile.ratings.length})',
      style: Theme.of(context).textTheme.titleMedium,
    ),
    const SizedBox(height: 8),
    if (profile.ratings.isEmpty)
      const EmptyView(
        icon: Icons.star_outline,
        message: 'Henüz puanlanmış film yok.',
      )
    else
      for (final rated in profile.ratings)
        MovieListTile(
          movieId: rated.movieId,
          title: rated.title,
          posterPath: rated.posterPath,
          trailing: _RatingChip(rating: rated.rating),
        ),
  ];

  Future<void> _confirmBlock(
    BuildContext context,
    WidgetRef ref,
    UserProfile profile,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('${profile.userName} engellensin mi?'),
        content: const Text(
          'Birbirinizin profilini göremez, mesajlaşamaz ve aynı etkinlik '
          'grubuna düşmezsiniz. Karşılıklı takip kaldırılır. Engeli '
          'Ayarlar > Gizlilik\'ten kaldırabilirsin.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Engelle'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      await ref.read(socialRepositoryProvider).block(userId);
      ref.invalidate(userProfileProvider(userId));
      ref.invalidate(followStatusProvider(userId));
      ref.invalidate(blockedUsersProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }
}

class _RestrictedNotice extends StatelessWidget {
  const _RestrictedNotice();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: ListTile(
        leading: Icon(Icons.lock_outline),
        title: Text('Bu kullanıcı puanlarını ve listelerini gizli tutuyor.'),
      ),
    );
  }
}

class _BlockedNotice extends ConsumerWidget {
  const _BlockedNotice({required this.userId});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Bu kullanıcıyı engelledin. Birbirinizin profilini göremez, '
              'mesajlaşamaz ve aynı etkinlik grubuna düşmezsiniz.',
            ),
            const SizedBox(height: 12),
            OutlinedButton(
              onPressed: () async {
                try {
                  await ref.read(socialRepositoryProvider).unblock(userId);
                  ref.invalidate(userProfileProvider(userId));
                  ref.invalidate(followStatusProvider(userId));
                  ref.invalidate(compatibilityProvider(userId));
                  ref.invalidate(blockedUsersProvider);
                } catch (error) {
                  if (context.mounted) {
                    ScaffoldMessenger.of(
                      context,
                    ).showSnackBar(SnackBar(content: Text(error.toString())));
                  }
                }
              },
              child: const Text('Engeli kaldır'),
            ),
          ],
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.profile});

  final UserProfile profile;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        UserAvatar(
          userName: profile.userName,
          imageUrl: profile.profileImageUrl,
          radius: 36,
        ),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                profile.userName,
                style: Theme.of(context).textTheme.titleLarge,
              ),
              if (profile.bio != null && profile.bio!.isNotEmpty) ...[
                const SizedBox(height: 4),
                Text(
                  profile.bio!,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _Stats extends StatelessWidget {
  const _Stats({required this.statistics});

  final ProfileStatistics statistics;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: Row(
          children: [
            _StatTile(label: 'Puanlama', value: '${statistics.totalRatings}'),
            _StatTile(
              label: 'Ortalama',
              value: statistics.averageRating.toStringAsFixed(1),
            ),
            _StatTile(
              label: 'Listelerde',
              value: '${statistics.totalMoviesInWatchlists}',
            ),
          ],
        ),
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: [
          Text(value, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 2),
          Text(label, style: Theme.of(context).textTheme.labelSmall),
        ],
      ),
    );
  }
}

/// Takip et / takibi bırak ve uyumluluk bilgisi.
class _SocialActions extends ConsumerWidget {
  const _SocialActions({
    required this.userId,
    required this.userName,
    this.showCompatibility = true,
  });

  final String userId;
  final String userName;

  /// Gizli profilde uyum da kapalıdır (puanlardan türer); hiç istenmez.
  final bool showCompatibility;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final followStatus = ref.watch(followStatusProvider(userId));

    Future<void> toggleFollow(bool isFollowing) async {
      final repository = ref.read(socialRepositoryProvider);
      try {
        if (isFollowing) {
          await repository.unfollow(userId);
        } else {
          await repository.follow(userId);
        }
        ref.invalidate(followStatusProvider(userId));
      } catch (error) {
        if (context.mounted) {
          ScaffoldMessenger.of(context)
              .showSnackBar(SnackBar(content: Text(error.toString())));
        }
      }
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (showCompatibility) ...[
          ref
              .watch(compatibilityProvider(userId))
              .when(
                loading: () => const SizedBox(height: 28),
                error: (_, _) => const SizedBox.shrink(),
                data: (result) => CompatibilityBadge(
                  score: result.compatibilityScore,
                  commonMovieCount: result.commonMovieCount,
                ),
              ),
          const SizedBox(height: 12),
        ],
        followStatus.when(
          loading: () => const SizedBox(height: 48),
          error: (error, _) => ErrorView(
            message: error.toString(),
            onRetry: () => ref.invalidate(followStatusProvider(userId)),
          ),
          data: (status) => Row(
            children: [
              Expanded(
                child: status.isFollowing
                    ? OutlinedButton.icon(
                        onPressed: () => toggleFollow(true),
                        icon: const Icon(Icons.person_remove_outlined),
                        label: const Text('Takibi bırak'),
                      )
                    : FilledButton.icon(
                        onPressed: () => toggleFollow(false),
                        icon: const Icon(Icons.person_add_outlined),
                        label: const Text('Takip et'),
                      ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => context.push(
                    '/conversation/$userId?name=${Uri.encodeComponent(userName)}',
                  ),
                  icon: const Icon(Icons.chat_bubble_outline),
                  label: const Text('Mesaj'),
                ),
              ),
            ],
          ),
        ),
        followStatus.maybeWhen(
          data: (status) => Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              '${status.followerCount} takipçi · ${status.followingCount} takip',
              style: Theme.of(context).textTheme.labelSmall,
            ),
          ),
          orElse: () => const SizedBox.shrink(),
        ),
      ],
    );
  }
}

class _RatingChip extends StatelessWidget {
  const _RatingChip({required this.rating});

  final int rating;

  @override
  Widget build(BuildContext context) {
    return Chip(
      label: Text('$rating/10'),
      visualDensity: VisualDensity.compact,
      padding: EdgeInsets.zero,
    );
  }
}
