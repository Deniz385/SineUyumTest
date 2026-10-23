import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/profile.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../providers/watchlist_provider.dart';
import '../../providers/year_in_review_provider.dart';
import '../../core/ui/movie_widgets.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/state_views.dart';
import '../watchlists/profile_lists_section.dart';

class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key, required this.userId});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final profile = ref.watch(userProfileProvider(userId));
    final isOwnProfile = ref.watch(sessionProvider).user?.id == userId;
    final loaded = profile.asData?.value;
    final recapYear = recapYearFor(ref.watch(clockProvider)());

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.profileTitle),
        actions: [
          if (isOwnProfile)
            IconButton(
              tooltip: l10n.commonSettings,
              icon: const Icon(Icons.settings_rounded),
              onPressed: () => context.push('/settings'),
            ),
          if (isOwnProfile)
            IconButton(
              tooltip: l10n.editProfile,
              icon: const Icon(Icons.edit_rounded),
              onPressed: () => context.push('/profile/edit'),
            ),
          if (!isOwnProfile && loaded != null && !loaded.isBlockedByMe)
            PopupMenuButton<String>(
              tooltip: l10n.moreTooltip,
              onSelected: (_) => _confirmBlock(context, ref, loaded),
              itemBuilder: (_) => [
                PopupMenuItem(value: 'block', child: Text(l10n.commonBlock)),
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
            ref.invalidate(userPublicListsProvider(userId));
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
                Wrap(
                  spacing: 8,
                  children: [
                    if (profile.statistics.totalRatings > 0)
                      TextButton.icon(
                        onPressed: () => context.push(
                          '/profile/$userId/identity${isOwnProfile ? '?mine=1' : ''}',
                        ),
                        icon: const Icon(Icons.badge_rounded),
                        label: Text(
                          isOwnProfile
                              ? l10n.myFilmIdentity
                              : l10n.filmIdentity,
                        ),
                      ),
                    TextButton.icon(
                      onPressed: () => context.push('/profile/$userId/diary'),
                      icon: const Icon(Icons.menu_book_rounded),
                      label: Text(
                        isOwnProfile ? l10n.myDiary : l10n.diaryTitle,
                      ),
                    ),
                    TextButton.icon(
                      onPressed: () => context.push(
                        '/profile/$userId/year-in-review?year=$recapYear',
                      ),
                      icon: const Icon(Icons.auto_awesome_rounded),
                      label: Text(
                        isOwnProfile
                            ? l10n.recapButtonMine(recapYear)
                            : l10n.recapButtonTheirs(recapYear),
                      ),
                    ),
                  ],
                ),
                if (!isOwnProfile) ...[
                  const SizedBox(height: 20),
                  _SocialActions(userId: userId, userName: profile.userName),
                ],
                ProfileListsSection(userId: userId, isMine: isOwnProfile),
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
      context.l10n.ratedMoviesTitle(profile.ratings.length),
      style: Theme.of(context).textTheme.titleMedium,
    ),
    const SizedBox(height: 8),
    if (profile.ratings.isEmpty)
      EmptyView(
        icon: Icons.star_outline_rounded,
        message: context.l10n.noRatedMovies,
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
    final l10n = context.l10n;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.blockConfirmTitle(profile.userName)),
        content: Text(l10n.blockConfirmBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.commonCancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.commonBlock),
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
    return Card(
      child: ListTile(
        leading: const Icon(Icons.lock_outline_rounded),
        title: Text(context.l10n.profileRestricted),
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
            Text(context.l10n.profileBlockedNotice),
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
              child: Text(context.l10n.commonUnblock),
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
    final l10n = context.l10n;
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: Row(
          children: [
            _StatTile(
              label: l10n.statRatings,
              value: '${statistics.totalRatings}',
            ),
            _StatTile(
              label: l10n.statAverage,
              value: l10n.decimal(statistics.averageRating),
            ),
            _StatTile(
              label: l10n.statInLists,
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

/// Follow / unfollow and the compatibility info.
class _SocialActions extends ConsumerWidget {
  const _SocialActions({
    required this.userId,
    required this.userName,
    this.showCompatibility = true,
  });

  final String userId;
  final String userName;

  /// On a private profile compatibility is hidden too (it derives from the
  /// ratings), so it isn't even requested.
  final bool showCompatibility;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
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
                data: (result) => Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    CompatibilityBadge(
                      score: result.compatibilityScore,
                      commonMovieCount: result.commonMovieCount,
                    ),
                    if (!result.insights.isEmpty)
                      _Insights(
                        insights: result.insights,
                        userId: userId,
                        userName: userName,
                      ),
                  ],
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
                        icon: const Icon(Icons.person_remove_rounded),
                        label: Text(l10n.unfollow),
                      )
                    : FilledButton.icon(
                        onPressed: () => toggleFollow(false),
                        icon: const Icon(Icons.person_add_rounded),
                        label: Text(l10n.commonFollow),
                      ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => context.push(
                    '/conversation/$userId?name=${Uri.encodeComponent(userName)}',
                  ),
                  icon: const Icon(Icons.chat_bubble_outline_rounded),
                  label: Text(l10n.sendMessage),
                ),
              ),
            ],
          ),
        ),
        followStatus.maybeWhen(
          data: (status) => Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              '${l10n.followerCount(status.followerCount)} · '
              '${l10n.followingCount(status.followingCount)}',
              style: Theme.of(context).textTheme.labelSmall,
            ),
          ),
          orElse: () => const SizedBox.shrink(),
        ),
      ],
    );
  }
}

/// Why you match, a shared favorite and a "debate movie" (a conversation
/// starter).
class _Insights extends StatelessWidget {
  const _Insights({
    required this.insights,
    required this.userId,
    required this.userName,
  });

  final CompatibilityInsights insights;
  final String userId;
  final String userName;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final favorite = insights.sharedFavorite;
    final debate = insights.debateMovie;

    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (insights.summary != null) ...[
            Text(insights.summary!, style: theme.textTheme.bodyMedium),
            const SizedBox(height: 6),
            Wrap(
              spacing: 6,
              runSpacing: 4,
              children: [
                for (final g in insights.sharedGenres)
                  Chip(
                    label: Text(g),
                    visualDensity: VisualDensity.compact,
                    backgroundColor: theme.colorScheme.primaryContainer,
                  ),
                for (final g in insights.divergentGenres)
                  Chip(
                    label: Text(g),
                    visualDensity: VisualDensity.compact,
                    side: BorderSide(color: theme.colorScheme.tertiary),
                  ),
              ],
            ),
          ],
          if (favorite != null)
            Card(
              child: ListTile(
                leading: const Icon(
                  Icons.favorite_rounded,
                  color: Colors.redAccent,
                ),
                title: Text(l10n.sharedFavorite(favorite.title)),
                subtitle: Text(
                  l10n.sharedFavoriteRatings(
                    favorite.currentUserRating,
                    userName,
                    favorite.targetUserRating,
                  ),
                ),
                onTap: () => context.push('/movie/${favorite.movieId}'),
              ),
            ),
          if (debate != null)
            Card(
              child: ListTile(
                leading: const Icon(Icons.forum_rounded),
                title: Text(l10n.debateMovie(debate.title)),
                subtitle: Text(
                  l10n.debateMovieRatings(
                    debate.currentUserRating,
                    userName,
                    debate.targetUserRating,
                  ),
                ),
                trailing: TextButton(
                  onPressed: () {
                    final draft = l10n.debateDraft(
                      debate.title,
                      debate.currentUserRating,
                      debate.targetUserRating,
                    );
                    context.push(
                      '/conversation/$userId?name=${Uri.encodeComponent(userName)}'
                      '&draft=${Uri.encodeComponent(draft)}',
                    );
                  },
                  child: Text(l10n.askThem),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _RatingChip extends StatelessWidget {
  const _RatingChip({required this.rating});

  final int rating;

  @override
  Widget build(BuildContext context) {
    return Chip(
      label: Text(context.l10n.ratingOutOfTen(rating)),
      visualDensity: VisualDensity.compact,
      padding: EdgeInsets.zero,
    );
  }
}
