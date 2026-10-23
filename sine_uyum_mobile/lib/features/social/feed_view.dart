import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/movie_widgets.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/skeleton.dart';
import '../../core/ui/state_views.dart';
import '../../data/models/movie.dart';
import '../../data/models/social.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/social_provider.dart';
import '../year_in_review/year_in_review_banner.dart';
import 'review_card.dart';

/// Ratings and reviews from the people you follow (and your own), under an
/// optional [header] (the home greeting), the yearly recap banner and this
/// week's taste twin. When the feed is thin, movie rows fill the screen
/// instead of an empty state.
class FeedView extends ConsumerWidget {
  const FeedView({super.key, this.header});

  final Widget? header;

  /// Fewer items than this (and no next page): movie rows are added.
  static const thinBelow = 3;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feed = ref.watch(feedProvider);
    const cardPadding = EdgeInsets.fromLTRB(
      AppSpace.gutter,
      0,
      AppSpace.gutter,
      AppSpace.sm,
    );
    const banners = Padding(
      padding: EdgeInsets.symmetric(horizontal: AppSpace.gutter),
      child: Column(children: [YearInReviewBanner(), TasteTwinCard()]),
    );

    Future<void> refresh() async {
      ref.invalidate(feedProvider);
      ref.invalidate(tasteTwinProvider);
      await ref.read(feedProvider.future);
    }

    return feed.when(
      loading: () => ListView(
        children: [
          ?header,
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: AppSpace.gutter),
            child: Shimmer(
              child: Column(
                children: [
                  SkeletonFeedCard(),
                  SkeletonFeedCard(),
                  SkeletonFeedCard(),
                ],
              ),
            ),
          ),
        ],
      ),
      error: (error, _) => ListView(
        children: [
          ?header,
          ErrorView(
            message: error.toString(),
            onRetry: () => ref.invalidate(feedProvider),
          ),
        ],
      ),
      data: (state) {
        final thin = state.items.length < thinBelow && !state.hasMore;
        final lead = [?header, banners];
        final itemCount =
            lead.length +
            state.items.length +
            (state.hasMore ? 1 : 0) +
            (thin ? 1 : 0);
        return RefreshIndicator(
          onRefresh: refresh,
          child: ListView.builder(
            padding: const EdgeInsets.only(bottom: AppSpace.xl),
            itemCount: itemCount,
            itemBuilder: (context, position) {
              if (position < lead.length) return lead[position];
              final index = position - lead.length;
              if (index == state.items.length) {
                if (thin) return _ThinFeed(empty: state.items.isEmpty);
                // Reached the end: load the next page.
                WidgetsBinding.instance.addPostFrameCallback(
                  (_) => ref.read(feedProvider.notifier).loadMore(),
                );
                return const Padding(
                  padding: cardPadding,
                  child: Shimmer(child: SkeletonFeedCard()),
                );
              }
              final item = state.items[index];
              final review = item.review;
              return Padding(
                padding: cardPadding,
                child: review != null
                    ? ReviewCard(
                        review: review,
                        showMovie: true,
                        subtitle: context.l10n.feedWroteReview,
                      )
                    : PosterLedCard(
                        movie: item.movie,
                        author: item.author,
                        subtitle: context.l10n.feedRatedMovie,
                        when: item.activityAt,
                        rating: item.rating,
                        compatibility: item.authorCompatibility,
                      ),
              );
            },
          ),
        );
      },
    );
  }
}

/// Below a thin feed: an invitation to find people (when it is empty) and
/// rows of popular, top rated and now playing films.
class _ThinFeed extends ConsumerWidget {
  const _ThinFeed({required this.empty});

  final bool empty;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final colors = context.colors;

    Widget row(String title, FutureProvider<List<Movie>> provider) {
      final value = ref.watch(provider);
      return MovieRow(
        title: title,
        movies: value.value,
        error: value.hasError && !value.hasValue ? value.error : null,
        onRetry: () => ref.invalidate(provider),
        onSeeAll: () => context.go('/discover'),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (empty)
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AppSpace.gutter,
              AppSpace.xs,
              AppSpace.gutter,
              0,
            ),
            child: Container(
              padding: const EdgeInsets.all(AppSpace.md),
              decoration: BoxDecoration(
                color: colors.surface,
                borderRadius: BorderRadius.circular(AppRadii.md),
                border: Border.all(color: colors.border),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(Icons.dynamic_feed_rounded, color: colors.primary),
                      const SizedBox(width: AppSpace.sm),
                      Expanded(
                        child: Text(
                          l10n.feedEmpty,
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpace.sm),
                  FilledButton.icon(
                    onPressed: () => context.go('/discover?section=people'),
                    icon: const Icon(Icons.person_search_rounded),
                    label: Text(l10n.homeFindPeople),
                  ),
                ],
              ),
            ),
          ),
        row(l10n.homePopularNow, popularMoviesProvider),
        row(l10n.homeTopRated, topRatedMoviesProvider),
        row(l10n.homeNowPlaying, nowPlayingProvider),
      ],
    );
  }
}

/// This week's taste twin: your best match that you don't follow yet.
class TasteTwinCard extends ConsumerStatefulWidget {
  const TasteTwinCard({super.key});

  @override
  ConsumerState<TasteTwinCard> createState() => _TasteTwinCardState();
}

class _TasteTwinCardState extends ConsumerState<TasteTwinCard> {
  bool _followed = false;

  Future<void> _follow(TasteTwin twin) async {
    try {
      await ref.read(socialRepositoryProvider).follow(twin.user.id);
      if (mounted) setState(() => _followed = true);
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final twin = ref.watch(tasteTwinProvider).value;
    if (twin == null) return const SizedBox.shrink();
    final following = _followed || twin.isFollowing;

    return Container(
      margin: const EdgeInsets.only(bottom: AppSpace.sm),
      padding: const EdgeInsets.all(AppSpace.md),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadii.lg),
        color: colors.surfaceRaised,
        border: Border.all(color: colors.primary.withValues(alpha: 0.35)),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            colors.primary.withValues(alpha: colors.isDark ? 0.16 : 0.10),
            colors.surface,
          ],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            l10n.tasteTwinHeader,
            style: context.type.overline.copyWith(color: colors.primary),
          ),
          const SizedBox(height: AppSpace.sm),
          Row(
            children: [
              UserAvatar(
                userName: twin.user.userName,
                imageUrl: twin.user.imageUrl,
                radius: 26,
                borderColor: colors.primary,
              ),
              const SizedBox(width: AppSpace.sm),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(twin.user.userName, style: context.type.h3),
                    Text(
                      l10n.tasteTwinStats(
                        l10n.percent(twin.compatibility),
                        twin.commonMovieCount,
                      ),
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpace.sm),
          Wrap(
            spacing: AppSpace.xs,
            runSpacing: AppSpace.xs,
            children: [
              FilledButton(
                onPressed: following ? null : () => _follow(twin),
                child: Text(
                  following ? l10n.followingState : l10n.commonFollow,
                ),
              ),
              OutlinedButton(
                onPressed: () => context.push('/profile/${twin.user.id}'),
                child: Text(l10n.viewProfile),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
