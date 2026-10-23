import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/tokens.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/skeleton.dart';
import '../../core/ui/state_views.dart';
import '../../data/models/movie.dart';
import '../../data/models/profile.dart';
import '../../l10n/l10n.dart';
import '../../providers/diary_provider.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../providers/social_provider.dart';
import '../../providers/watchlist_provider.dart';
import '../../providers/year_in_review_provider.dart';
import '../social/review_card.dart';
import '../watchlists/discover_lists_page.dart' show LoadMoreFooter;
import '../watchlists/profile_lists_section.dart';
import 'film_identity_page.dart';

/// A profile (docs §7): a cover made from their favourite film's poster
/// (blurred, darkened, behind a scrim), the avatar over its edge, name,
/// bio, identity title, stats, compatibility on others' profiles, then the
/// tabs Ratings · Diary · Lists · Reviews.
class ProfilePage extends ConsumerStatefulWidget {
  const ProfilePage({super.key, required this.userId});

  final String userId;

  @override
  ConsumerState<ProfilePage> createState() => _ProfilePageState();
}

enum _ProfileTab { ratings, diary, lists, reviews }

class _ProfilePageState extends ConsumerState<ProfilePage>
    with SingleTickerProviderStateMixin {
  late final _tabs = TabController(
    length: _ProfileTab.values.length,
    vsync: this,
  )..addListener(() => setState(() {}));

  String get userId => widget.userId;

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    ref.invalidate(userProfileProvider(userId));
    ref.invalidate(followStatusProvider(userId));
    ref.invalidate(compatibilityProvider(userId));
    ref.invalidate(userPublicListsProvider(userId));
    ref.invalidate(filmIdentityProvider(userId));
    ref.invalidate(userReviewsProvider(userId));
    await ref.read(userProfileProvider(userId).future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final profile = ref.watch(userProfileProvider(userId));
    final isOwnProfile = ref.watch(sessionProvider).user?.id == userId;
    final loaded = profile.asData?.value;

    final actions = [
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
          onSelected: (_) => _confirmBlock(context, loaded),
          itemBuilder: (_) => [
            PopupMenuItem(value: 'block', child: Text(l10n.commonBlock)),
          ],
        ),
    ];

    return Scaffold(
      body: profile.when(
        loading: () => CustomScrollView(
          slivers: [
            SliverAppBar(title: Text(l10n.profileTitle), actions: actions),
            const SliverToBoxAdapter(child: _HeaderSkeleton()),
          ],
        ),
        error: (error, _) => CustomScrollView(
          slivers: [
            SliverAppBar(title: Text(l10n.profileTitle), actions: actions),
            SliverFillRemaining(
              hasScrollBody: false,
              child: ErrorView(
                message: error.toString(),
                onRetry: () => ref.invalidate(userProfileProvider(userId)),
              ),
            ),
          ],
        ),
        data: (profile) {
          final full = !profile.isBlockedByMe && !profile.isRestricted;
          final tab = _ProfileTab.values[_tabs.index];
          return RefreshIndicator(
            onRefresh: _refresh,
            edgeOffset: kToolbarHeight,
            child: CustomScrollView(
              slivers: [
                _Cover(profile: profile, actions: actions),
                SliverToBoxAdapter(
                  child: _Header(
                    profile: profile,
                    isOwnProfile: isOwnProfile,
                    full: full,
                  ),
                ),
                if (full) ...[
                  SliverPersistentHeader(
                    pinned: true,
                    delegate: _TabBarDelegate(
                      TabBar(
                        controller: _tabs,
                        // All four always in view; long labels shrink
                        // rather than scroll away.
                        labelPadding: const EdgeInsets.symmetric(
                          horizontal: AppSpace.xxs,
                        ),
                        tabs: [
                          for (final label in [
                            l10n.profileTabRatings,
                            l10n.profileTabDiary,
                            l10n.profileTabLists,
                            l10n.profileTabReviews,
                          ])
                            Tab(
                              child: FittedBox(
                                fit: BoxFit.scaleDown,
                                child: Text(label, maxLines: 1),
                              ),
                            ),
                        ],
                      ),
                      background: context.colors.bg,
                      scaler: MediaQuery.textScalerOf(context),
                    ),
                  ),
                  ...switch (tab) {
                    _ProfileTab.ratings => _ratingsSlivers(context, profile),
                    _ProfileTab.diary => [
                      SliverToBoxAdapter(
                        child: _DiaryPreview(
                          userId: userId,
                          isMine: isOwnProfile,
                        ),
                      ),
                    ],
                    _ProfileTab.lists => [
                      SliverToBoxAdapter(
                        child: ProfileListsSection(
                          userId: userId,
                          isMine: isOwnProfile,
                        ),
                      ),
                    ],
                    _ProfileTab.reviews => [
                      _ReviewsTab(userId: userId, isMine: isOwnProfile),
                    ],
                  },
                ],
                const SliverPadding(
                  padding: EdgeInsets.only(bottom: AppSpace.xxl),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  List<Widget> _ratingsSlivers(BuildContext context, UserProfile profile) {
    final l10n = context.l10n;
    return [
      SliverToBoxAdapter(
        child: SectionHeader(
          title: l10n.ratedMoviesTitle(profile.ratings.length),
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.md,
            AppSpace.gutter,
            AppSpace.sm,
          ),
        ),
      ),
      if (profile.ratings.isEmpty)
        SliverToBoxAdapter(
          child: EmptyView(
            icon: Icons.star_outline_rounded,
            message: l10n.noRatedMovies,
          ),
        )
      else
        SliverPadding(
          padding: const EdgeInsets.symmetric(horizontal: AppSpace.gutter),
          sliver: SliverLayoutBuilder(
            builder: (context, constraints) {
              const gap = AppSpace.sm;
              final columns = constraints.crossAxisExtent >= 600 ? 5 : 3;
              final cell =
                  (constraints.crossAxisExtent - gap * (columns - 1)) / columns;
              final scaler = MediaQuery.textScalerOf(context);
              return SliverGrid(
                gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: columns,
                  crossAxisSpacing: gap,
                  mainAxisSpacing: AppSpace.md,
                  mainAxisExtent: cell * 1.5 + 8 + scaler.scale(1) * 40,
                ),
                delegate: SliverChildBuilderDelegate(
                  (context, index) =>
                      _RatedPoster(rated: profile.ratings[index]),
                  childCount: profile.ratings.length,
                ),
              );
            },
          ),
        ),
    ];
  }

  Future<void> _confirmBlock(BuildContext context, UserProfile profile) async {
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
            style: FilledButton.styleFrom(
              backgroundColor: context.colors.accentStrong,
              foregroundColor: Colors.white,
            ),
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

/// Keeps the tab bar under the app bar while the profile scrolls.
class _TabBarDelegate extends SliverPersistentHeaderDelegate {
  _TabBarDelegate(
    this.tabBar, {
    required this.background,
    required this.scaler,
  });

  final TabBar tabBar;
  final Color background;
  final TextScaler scaler;

  double get _height => tabBar.preferredSize.height;

  @override
  double get minExtent => _height;

  @override
  double get maxExtent => _height;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlaps) =>
      SizedBox(
        height: _height,
        child: ColoredBox(color: background, child: tabBar),
      );

  @override
  bool shouldRebuild(_TabBarDelegate old) =>
      old.tabBar != tabBar ||
      old.background != background ||
      old.scaler != scaler;
}

class _HeaderSkeleton extends StatelessWidget {
  const _HeaderSkeleton();

  @override
  Widget build(BuildContext context) => const Shimmer(
    child: Padding(
      padding: EdgeInsets.all(AppSpace.gutter),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SkeletonBox(height: 140, radius: AppRadii.lg),
          SizedBox(height: AppSpace.md),
          Row(
            children: [
              SkeletonBox(height: 72, circle: true),
              SizedBox(width: AppSpace.md),
              Expanded(child: SkeletonBox(height: 22)),
            ],
          ),
          SizedBox(height: AppSpace.md),
          SkeletonBox(height: 64, radius: AppRadii.md),
        ],
      ),
    ),
  );
}

/// The cover: their favourite film's poster, blurred and darkened behind a
/// scrim, with the avatar, the name and the identity title on its edge.
class _Cover extends ConsumerWidget {
  const _Cover({required this.profile, required this.actions});

  final UserProfile profile;
  final List<Widget> actions;

  static const avatarRadius = 44.0;

  String? get _coverPoster {
    for (final rated in [
      ...profile.statistics.topRatedMovies,
      ...profile.ratings,
    ]) {
      if (rated.posterPath?.isNotEmpty == true) return rated.posterPath;
    }
    return null;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = context.colors;
    final scaler = MediaQuery.textScalerOf(context);
    final expanded = 176 + scaler.scale(30) * 36 / 30;
    final poster = _coverPoster;

    return SliverAppBar(
      pinned: true,
      expandedHeight: expanded,
      backgroundColor: colors.bg,
      actions: actions,
      title: _FadingTitle(title: profile.userName),
      flexibleSpace: FlexibleSpaceBar(
        collapseMode: CollapseMode.parallax,
        background: Stack(
          fit: StackFit.expand,
          children: [
            if (poster != null)
              ImageFiltered(
                imageFilter: ImageFilter.blur(sigmaX: 24, sigmaY: 24),
                child: BackdropImage(path: poster, size: 'w342'),
              )
            else
              DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      colors.primary.withValues(alpha: 0.35),
                      colors.accent.withValues(alpha: 0.25),
                    ],
                  ),
                ),
              ),
            if (poster != null)
              ColoredBox(color: Colors.black.withValues(alpha: 0.35)),
            DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  stops: const [0, 0.45, 1],
                  colors: [
                    colors.bg.withValues(alpha: 0.2),
                    colors.bg.withValues(alpha: 0.55),
                    colors.bg,
                  ],
                ),
              ),
            ),
            Positioned(
              left: AppSpace.gutter,
              right: AppSpace.gutter,
              bottom: 0,
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  UserAvatar(
                    userName: profile.userName,
                    imageUrl: profile.profileImageUrl,
                    radius: avatarRadius,
                    borderColor: colors.bg,
                  ),
                  const SizedBox(width: AppSpace.md),
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.only(bottom: AppSpace.xs),
                      child: Semantics(
                        header: true,
                        child: Text(
                          profile.userName,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: context.type.h1,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The name in the collapsed app bar, once the cover has scrolled away.
class _FadingTitle extends StatelessWidget {
  const _FadingTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    final settings = context
        .dependOnInheritedWidgetOfExactType<FlexibleSpaceBarSettings>();
    var visible = 1.0;
    if (settings != null) {
      final range = settings.maxExtent - settings.minExtent;
      visible = range <= 0
          ? 1
          : (1 - (settings.currentExtent - settings.minExtent) / range * 4)
                .clamp(0.0, 1.0);
    }
    return ExcludeSemantics(
      excluding: visible < 0.5,
      child: Opacity(
        opacity: visible,
        child: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
      ),
    );
  }
}

class _Header extends ConsumerWidget {
  const _Header({
    required this.profile,
    required this.isOwnProfile,
    required this.full,
  });

  final UserProfile profile;
  final bool isOwnProfile;

  /// The details are visible (not private, not blocked).
  final bool full;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final userId = profile.id;
    final bio = profile.bio;
    final recapYear = recapYearFor(ref.watch(clockProvider)());

    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AppSpace.gutter,
        AppSpace.sm,
        AppSpace.gutter,
        AppSpace.xs,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (full && profile.statistics.totalRatings > 0)
            _IdentityPill(userId: userId, isMine: isOwnProfile),
          if (bio != null && bio.isNotEmpty) ...[
            const SizedBox(height: AppSpace.xs),
            Text(bio, style: context.type.body),
          ],
          const SizedBox(height: AppSpace.md),
          if (profile.isBlockedByMe)
            _BlockedNotice(userId: userId)
          else if (profile.isRestricted) ...[
            const _RestrictedNotice(),
            if (!isOwnProfile) ...[
              const SizedBox(height: AppSpace.lg),
              _SocialActions(
                userId: userId,
                userName: profile.userName,
                showCompatibility: false,
                showCounts: true,
              ),
            ],
          ] else ...[
            _Stats(userId: userId, statistics: profile.statistics),
            if (!isOwnProfile) ...[
              const SizedBox(height: AppSpace.md),
              _SocialActions(userId: userId, userName: profile.userName),
            ],
            const SizedBox(height: AppSpace.sm),
            Wrap(
              spacing: AppSpace.xs,
              runSpacing: 0,
              children: [
                if (profile.statistics.totalRatings > 0)
                  ActionChip(
                    avatar: const Icon(Icons.badge_rounded),
                    label: Text(
                      isOwnProfile ? l10n.myFilmIdentity : l10n.filmIdentity,
                    ),
                    onPressed: () => context.push(
                      '/profile/$userId/identity${isOwnProfile ? '?mine=1' : ''}',
                    ),
                  ),
                ActionChip(
                  avatar: const Icon(Icons.menu_book_rounded),
                  label: Text(isOwnProfile ? l10n.myDiary : l10n.diaryTitle),
                  onPressed: () => context.push('/profile/$userId/diary'),
                ),
                ActionChip(
                  avatar: const Icon(Icons.auto_awesome_rounded),
                  label: Text(
                    isOwnProfile
                        ? l10n.recapButtonMine(recapYear)
                        : l10n.recapButtonTheirs(recapYear),
                  ),
                  onPressed: () => context.push(
                    '/profile/$userId/year-in-review?year=$recapYear',
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// The film identity title ("The Midnight Dreamer") as a pill; opens the
/// identity card. Hidden until it loads (and when it can't).
class _IdentityPill extends ConsumerWidget {
  const _IdentityPill({required this.userId, required this.isMine});

  final String userId;
  final bool isMine;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final title = ref.watch(filmIdentityProvider(userId)).value?.title;
    if (title == null || title.isEmpty) return const SizedBox.shrink();
    final colors = context.colors;
    return Semantics(
      button: true,
      label: '${context.l10n.filmIdentity}: $title',
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.pill),
        onTap: () =>
            context.push('/profile/$userId/identity${isMine ? '?mine=1' : ''}'),
        child: Container(
          constraints: const BoxConstraints(minHeight: 36),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.pill),
            color: colors.primary.withValues(alpha: colors.isDark ? 0.14 : 0.1),
            border: Border.all(color: colors.primary.withValues(alpha: 0.6)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.auto_awesome_rounded, size: 16, color: colors.primary),
              const SizedBox(width: 6),
              Flexible(
                child: Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelLarge,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Films, average, followers and following.
class _Stats extends ConsumerWidget {
  const _Stats({required this.userId, required this.statistics});

  final String userId;
  final ProfileStatistics statistics;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final colors = context.colors;
    final follow = ref.watch(followStatusProvider(userId)).value;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: AppSpace.sm),
      decoration: BoxDecoration(
        color: colors.surface,
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(color: colors.border),
      ),
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
            label: l10n.statFollowers,
            value: follow == null ? '–' : '${follow.followerCount}',
          ),
          _StatTile(
            label: l10n.statFollowing,
            value: follow == null ? '–' : '${follow.followingCount}',
          ),
        ],
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
      child: Semantics(
        label: '$label: $value',
        excludeSemantics: true,
        child: Column(
          children: [
            Text(value, style: context.type.number.copyWith(fontSize: 20)),
            const SizedBox(height: 2),
            Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelMedium,
            ),
          ],
        ),
      ),
    );
  }
}

/// A rated film in the Ratings tab: the poster, the title and the rating.
class _RatedPoster extends StatelessWidget {
  const _RatedPoster({required this.rated});

  final RatedMovie rated;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: '${rated.title}, ${context.l10n.ratingSpoken(rated.rating)}',
      excludeSemantics: true,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: () => context.push('/movie/${rated.movieId}'),
        child: LayoutBuilder(
          builder: (context, constraints) => Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Stack(
                children: [
                  PosterImage(
                    posterPath: rated.posterPath,
                    width: constraints.maxWidth,
                    size: 'w185',
                  ),
                  Positioned(
                    left: 6,
                    bottom: 6,
                    child: RatingPill(rating: rated.rating),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                rated.title,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.titleSmall,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The Diary tab: this year's total, the latest entries and a way into the
/// full diary.
class _DiaryPreview extends ConsumerWidget {
  const _DiaryPreview({required this.userId, required this.isMine});

  final String userId;
  final bool isMine;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final diary = ref.watch(diaryProvider((userId: userId, year: null)));
    final open = Padding(
      padding: const EdgeInsets.all(AppSpace.gutter),
      child: OutlinedButton.icon(
        onPressed: () => context.push('/profile/$userId/diary'),
        icon: const Icon(Icons.menu_book_rounded),
        label: Text(isMine ? l10n.myDiary : l10n.diaryTitle),
      ),
    );

    return diary.when(
      loading: () => const LoadingView(rows: 3, posters: true),
      error: (error, _) => EmptyView(
        icon: Icons.lock_outline_rounded,
        message: error is ApiException ? error.message : error.toString(),
      ),
      data: (state) {
        if (state.entries.isEmpty) {
          return EmptyView(
            icon: Icons.menu_book_rounded,
            message: isMine ? l10n.diaryEmptyOwn : l10n.diaryEmptyOther,
          );
        }
        final dates = DateFormat.yMMMd(l10n.localeName);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SectionHeader(
              title: l10n.diaryYearTotal(state.totalCount, state.year),
              padding: const EdgeInsets.fromLTRB(
                AppSpace.gutter,
                AppSpace.md,
                AppSpace.gutter,
                AppSpace.xs,
              ),
            ),
            for (final entry in state.entries.take(6))
              ListTile(
                leading: PosterImage(
                  posterPath: entry.movie.posterPath,
                  width: 40,
                  size: 'w185',
                  borderRadius: AppRadii.sm,
                ),
                title: Text(
                  entry.movie.title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                subtitle: Text(dates.format(entry.watchedOn)),
                trailing: entry.rating == null
                    ? null
                    : RatingPill(rating: entry.rating!),
                onTap: () => context.push('/movie/${entry.movie.id}'),
              ),
            open,
          ],
        );
      },
    );
  }
}

/// The Reviews tab: their reviews, newest first, page by page. A private
/// profile or a block shows the server's message, like the Diary tab.
class _ReviewsTab extends ConsumerWidget {
  const _ReviewsTab({required this.userId, required this.isMine});

  final String userId;
  final bool isMine;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final provider = userReviewsProvider(userId);
    final reviews = ref.watch(provider);

    return reviews.when(
      loading: () =>
          const SliverToBoxAdapter(child: LoadingView(rows: 3, posters: true)),
      error: (error, _) => SliverToBoxAdapter(
        child:
            error is ApiException &&
                (error.statusCode == 403 || error.statusCode == 404)
            ? EmptyView(
                icon: Icons.lock_outline_rounded,
                message: error.message,
              )
            : ErrorView(
                message: error.toString(),
                onRetry: () => ref.invalidate(provider),
              ),
      ),
      data: (state) {
        if (state.items.isEmpty) {
          return SliverToBoxAdapter(
            child: EmptyView(
              icon: Icons.rate_review_rounded,
              message: l10n.profileReviewsEmpty,
              hint: isMine ? l10n.profileReviewsHintOwn : null,
            ),
          );
        }
        return SliverPadding(
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.md,
            AppSpace.gutter,
            0,
          ),
          sliver: SliverList.builder(
            itemCount: state.items.length + (state.hasMore ? 1 : 0),
            itemBuilder: (context, index) {
              if (index == state.items.length) {
                // Built when scrolled near the end: loads the next page.
                return LoadMoreFooter(
                  key: ValueKey('reviews-more-${state.page}'),
                  failed: state.loadMoreFailed,
                  onLoadMore: () => ref.read(provider.notifier).loadMore(),
                );
              }
              final review = state.items[index];
              return Padding(
                key: ValueKey(review.id),
                padding: const EdgeInsets.only(bottom: AppSpace.sm),
                child: ReviewCard(review: review, showMovie: true),
              );
            },
          ),
        );
      },
    );
  }
}

/// Follow / unfollow, a message, and the compatibility with its insights.
class _SocialActions extends ConsumerWidget {
  const _SocialActions({
    required this.userId,
    required this.userName,
    this.showCompatibility = true,
    this.showCounts = false,
  });

  final String userId;
  final String userName;

  /// On a private profile compatibility is hidden too (it derives from the
  /// ratings), so it isn't even requested.
  final bool showCompatibility;

  /// Followers and following under the buttons (when the stats are hidden).
  final bool showCounts;

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
                loading: () => const SizedBox(height: 56),
                error: (_, _) => const SizedBox.shrink(),
                data: (result) => Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        CompatibilityBadge(
                          score: result.compatibilityScore,
                          ring: true,
                        ),
                        const SizedBox(width: AppSpace.sm),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                l10n.compatibilityMatch(
                                  l10n.percent(result.compatibilityScore),
                                ),
                                style: context.type.h3,
                              ),
                              if (result.commonMovieCount > 0)
                                Text(
                                  l10n.commonMoviesCount(
                                    result.commonMovieCount,
                                  ),
                                  style: Theme.of(context).textTheme.bodySmall,
                                ),
                            ],
                          ),
                        ),
                      ],
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
          const SizedBox(height: AppSpace.md),
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
              const SizedBox(width: AppSpace.sm),
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
        if (showCounts)
          followStatus.maybeWhen(
            data: (status) => Padding(
              padding: const EdgeInsets.only(top: AppSpace.xs),
              child: Text(
                '${l10n.followerCount(status.followerCount)} · '
                '${l10n.followingCount(status.followingCount)}',
                style: Theme.of(context).textTheme.labelMedium,
              ),
            ),
            orElse: () => const SizedBox.shrink(),
          ),
      ],
    );
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
