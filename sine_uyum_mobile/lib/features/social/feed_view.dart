import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/social.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/social_provider.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/state_views.dart';
import '../year_in_review/year_in_review_banner.dart';
import 'review_card.dart';

/// Ratings and reviews from the people you follow (and your own).
class FeedView extends ConsumerWidget {
  const FeedView({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feed = ref.watch(feedProvider);

    return feed.when(
      loading: () => const LoadingView(),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.invalidate(feedProvider),
      ),
      data: (state) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(feedProvider);
          ref.invalidate(tasteTwinProvider);
          await ref.read(feedProvider.future);
        },
        child: state.items.isEmpty
            ? ListView(
                children: [
                  const Padding(
                    padding: EdgeInsets.all(12),
                    child: Column(
                      children: [YearInReviewBanner(), TasteTwinCard()],
                    ),
                  ),
                  const SizedBox(height: 24),
                  EmptyView(
                    icon: Icons.dynamic_feed_rounded,
                    message: context.l10n.feedEmpty,
                  ),
                ],
              )
            : ListView.builder(
                padding: const EdgeInsets.all(12),
                // First item: the yearly recap banner (in season) and this
                // week's taste twin (each empty if none).
                itemCount: state.items.length + 1 + (state.hasMore ? 1 : 0),
                itemBuilder: (context, position) {
                  if (position == 0) {
                    return const Column(
                      children: [YearInReviewBanner(), TasteTwinCard()],
                    );
                  }
                  final index = position - 1;
                  if (index == state.items.length) {
                    // Reached the end: load the next page.
                    WidgetsBinding.instance.addPostFrameCallback(
                      (_) => ref.read(feedProvider.notifier).loadMore(),
                    );
                    return const Padding(
                      padding: EdgeInsets.all(16),
                      child: Center(child: CircularProgressIndicator()),
                    );
                  }
                  final item = state.items[index];
                  final review = item.review;
                  return review != null
                      ? ReviewCard(
                          review: review,
                          showMovie: true,
                          subtitle: context.l10n.feedWroteReview,
                        )
                      : _RatedCard(item: item);
                },
              ),
      ),
    );
  }
}

class _RatedCard extends StatelessWidget {
  const _RatedCard({required this.item});

  final FeedItem item;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AuthorRow(
              author: item.author,
              rating: item.rating,
              compatibility: item.authorCompatibility,
              subtitle: context.l10n.feedRatedMovie,
              when: item.activityAt,
            ),
            const SizedBox(height: 10),
            InkWell(
              onTap: () => context.push('/movie/${item.movie.id}'),
              child: Row(
                children: [
                  PosterImage(
                    posterPath: item.movie.posterPath,
                    width: 40,
                    height: 60,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      item.movie.title,
                      style: Theme.of(context).textTheme.titleMedium,
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
    final twin = ref.watch(tasteTwinProvider).value;
    if (twin == null) return const SizedBox.shrink();
    final following = _followed || twin.isFollowing;

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(16),
        gradient: const LinearGradient(
          colors: [Color(0xFF1B1038), Color(0xFF0D2A4A)],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            l10n.tasteTwinHeader,
            style: const TextStyle(
              color: Color(0xFFFFD54F),
              fontSize: 12,
              fontWeight: FontWeight.w600,
              letterSpacing: 1.1,
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              UserAvatar(
                userName: twin.user.userName,
                imageUrl: twin.user.imageUrl,
                radius: 26,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      twin.user.userName,
                      style: const TextStyle(color: Colors.white, fontSize: 17),
                    ),
                    Text(
                      l10n.tasteTwinStats(
                        l10n.percent(twin.compatibility),
                        twin.commonMovieCount,
                      ),
                      style: const TextStyle(color: Color(0xFFE3E8FF)),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              OutlinedButton(
                style: OutlinedButton.styleFrom(foregroundColor: Colors.white),
                onPressed: () => context.push('/profile/${twin.user.id}'),
                child: Text(l10n.viewProfile),
              ),
              const SizedBox(width: 8),
              FilledButton(
                onPressed: following ? null : () => _follow(twin),
                child: Text(
                  following ? l10n.followingState : l10n.commonFollow,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
