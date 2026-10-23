import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/social.dart';
import 'repository_providers.dart';

/// A movie's reviews (first page) and the user's own rating/review.
final movieReviewsProvider = FutureProvider.autoDispose
    .family<MovieReviews, int>(
      (ref, movieId) =>
          ref.watch(socialRepositoryProvider).movieReviews(movieId),
    );

class FeedState {
  const FeedState({
    this.items = const [],
    this.nextCursor,
    this.isLoadingMore = false,
  });

  final List<FeedItem> items;
  final String? nextCursor;
  final bool isLoadingMore;

  bool get hasMore => nextCursor != null;
}

/// The feed: the first page is loaded, [FeedNotifier.loadMore] appends the
/// next one using the cursor.
final feedProvider = AsyncNotifierProvider.autoDispose<FeedNotifier, FeedState>(
  FeedNotifier.new,
);

class FeedNotifier extends AsyncNotifier<FeedState> {
  @override
  Future<FeedState> build() async {
    // Watched: the repository is rebuilt on a language change, so the feed
    // reloads in the new language.
    final page = await ref.watch(socialRepositoryProvider).feed();
    return FeedState(items: page.items, nextCursor: page.nextCursor);
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.isLoadingMore) return;
    state = AsyncData(
      FeedState(
        items: current.items,
        nextCursor: current.nextCursor,
        isLoadingMore: true,
      ),
    );
    try {
      final page = await ref
          .read(socialRepositoryProvider)
          .feed(cursor: current.nextCursor);
      if (!ref.mounted) return;
      state = AsyncData(
        FeedState(
          items: [...current.items, ...page.items],
          nextCursor: page.nextCursor,
        ),
      );
    } catch (_) {
      // If the next page fails, the current list is kept; it can be retried.
      if (ref.mounted) state = AsyncData(current);
    }
  }
}

/// This week's taste twin (above the feed).
final tasteTwinProvider = FutureProvider.autoDispose<TasteTwin?>(
  (ref) => ref.watch(socialRepositoryProvider).tasteTwin(),
);

/// People worth following (Discover → People, first-run welcome).
final peopleSuggestionsProvider = FutureProvider.autoDispose<PeopleSuggestions>(
  (ref) => ref.watch(socialRepositoryProvider).peopleSuggestions(limit: 20),
);
