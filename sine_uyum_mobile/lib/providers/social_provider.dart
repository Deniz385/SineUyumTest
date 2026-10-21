import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/social.dart';
import 'repository_providers.dart';

/// Bir filmin incelemeleri (ilk sayfa) ve kullanıcının kendi puanı/incelemesi.
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

/// Akış: ilk sayfa yüklenir, [loadMore] imleçle sonrakini ekler.
final feedProvider = AsyncNotifierProvider.autoDispose<FeedNotifier, FeedState>(
  FeedNotifier.new,
);

class FeedNotifier extends AsyncNotifier<FeedState> {
  @override
  Future<FeedState> build() async {
    final page = await ref.read(socialRepositoryProvider).feed();
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
      // Sonraki sayfa yüklenemezse eldeki liste korunur; tekrar denenebilir.
      if (ref.mounted) state = AsyncData(current);
    }
  }
}
