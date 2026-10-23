import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/social.dart';
import 'repository_providers.dart';
import 'watchlist_provider.dart' show retryUnlessRefused;

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

class UserReviewsState {
  const UserReviewsState({
    this.items = const [],
    this.page = 1,
    this.hasMore = false,
    this.isLoadingMore = false,
    this.loadMoreFailed = false,
  });

  final List<Review> items;
  final int page;
  final bool hasMore;
  final bool isLoadingMore;

  /// The next page failed: the list shows a retry instead of looping.
  final bool loadMoreFailed;
}

/// A user's reviews for the profile's Reviews tab, page by page.
final userReviewsProvider = AsyncNotifierProvider.autoDispose
    .family<UserReviewsNotifier, UserReviewsState, String>(
      UserReviewsNotifier.new,
      // A private profile (403) or a block (404) is final, not retried.
      retry: retryUnlessRefused,
    );

class UserReviewsNotifier extends AsyncNotifier<UserReviewsState> {
  UserReviewsNotifier(this.userId);

  final String userId;

  @override
  Future<UserReviewsState> build() async {
    final page = await ref.watch(socialRepositoryProvider).userReviews(userId);
    return UserReviewsState(items: page.items, hasMore: page.hasMore);
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.isLoadingMore) return;
    state = AsyncData(
      UserReviewsState(
        items: current.items,
        page: current.page,
        hasMore: true,
        isLoadingMore: true,
      ),
    );
    try {
      final next = await ref
          .read(socialRepositoryProvider)
          .userReviews(userId, page: current.page + 1);
      if (!ref.mounted) return;
      // A review that moved between pages (a new one was written) is not
      // shown twice.
      final seen = {for (final r in current.items) r.id};
      state = AsyncData(
        UserReviewsState(
          items: [
            ...current.items,
            for (final r in next.items)
              if (seen.add(r.id)) r,
          ],
          page: next.page,
          hasMore: next.hasMore,
        ),
      );
    } catch (_) {
      if (!ref.mounted) return;
      state = AsyncData(
        UserReviewsState(
          items: current.items,
          page: current.page,
          hasMore: true,
          loadMoreFailed: true,
        ),
      );
    }
  }
}

/// People worth following (Discover → People, first-run welcome).
final peopleSuggestionsProvider = FutureProvider.autoDispose<PeopleSuggestions>(
  (ref) => ref.watch(socialRepositoryProvider).peopleSuggestions(limit: 20),
);
