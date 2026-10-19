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
              _Stats(statistics: profile.statistics),
              if (!isOwnProfile) ...[
                const SizedBox(height: 20),
                _SocialActions(userId: userId, userName: profile.userName),
              ],
              const SizedBox(height: 24),
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
            ],
          ),
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
                Text(profile.bio!, style: Theme.of(context).textTheme.bodySmall),
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
            _StatTile(
              label: 'Puanlama',
              value: '${statistics.totalRatings}',
            ),
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
  const _SocialActions({required this.userId, required this.userName});

  final String userId;
  final String userName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final followStatus = ref.watch(followStatusProvider(userId));
    final compatibility = ref.watch(compatibilityProvider(userId));

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
        compatibility.when(
          loading: () => const SizedBox(height: 28),
          error: (_, _) => const SizedBox.shrink(),
          data: (result) => CompatibilityBadge(
            score: result.compatibilityScore,
            commonMovieCount: result.commonMovieCount,
          ),
        ),
        const SizedBox(height: 12),
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
