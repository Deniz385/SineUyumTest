import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/movie.dart';
import '../../providers/repository_providers.dart';

class WelcomeMovies {
  const WelcomeMovies({
    this.movies = const [],
    this.topRatedPage = 1,
    this.isLoadingMore = false,
  });

  final List<Movie> movies;
  final int topRatedPage;
  final bool isLoadingMore;

  /// The server serves five pages of top rated films.
  bool get hasMore => topRatedPage < WelcomeMoviesNotifier.lastPage;

  WelcomeMovies copyWith({
    List<Movie>? movies,
    int? topRatedPage,
    bool? isLoadingMore,
  }) => WelcomeMovies(
    movies: movies ?? this.movies,
    topRatedPage: topRatedPage ?? this.topRatedPage,
    isLoadingMore: isLoadingMore ?? this.isLoadingMore,
  );
}

/// Films to rate in the first-run welcome: well-known top rated films
/// mixed with this week's popular ones (people are likely to have seen
/// them), without repeats; "more" adds the next top rated page.
final welcomeMoviesProvider =
    AsyncNotifierProvider.autoDispose<WelcomeMoviesNotifier, WelcomeMovies>(
      WelcomeMoviesNotifier.new,
    );

class WelcomeMoviesNotifier extends AsyncNotifier<WelcomeMovies> {
  static const lastPage = 5;

  @override
  Future<WelcomeMovies> build() async {
    final repository = ref.watch(movieRepositoryProvider);
    final (topRated, popular) = await (
      repository.topRated(),
      // Nice to have: the grid still works without it.
      repository.popular().catchError((_) => const <Movie>[]),
    ).wait;
    final mixed = <Movie>[];
    for (var i = 0; i < topRated.length || i < popular.length; i++) {
      if (i < topRated.length) mixed.add(topRated[i]);
      if (i < popular.length) mixed.add(popular[i]);
    }
    return WelcomeMovies(movies: _unique(mixed));
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.isLoadingMore) return;
    state = AsyncData(current.copyWith(isLoadingMore: true));
    try {
      final next = current.topRatedPage + 1;
      final more = await ref.read(movieRepositoryProvider).topRated(page: next);
      if (!ref.mounted) return;
      state = AsyncData(
        WelcomeMovies(
          movies: _unique([...current.movies, ...more]),
          topRatedPage: next,
        ),
      );
    } catch (_) {
      // Keep what is shown; the button can be tapped again.
      if (ref.mounted) state = AsyncData(current);
    }
  }

  static List<Movie> _unique(List<Movie> movies) {
    final seen = <int>{};
    return [
      for (final movie in movies)
        if (seen.add(movie.id)) movie,
    ];
  }
}
