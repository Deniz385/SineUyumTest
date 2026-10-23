import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/movie.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../core/ui/movie_widgets.dart';
import '../../core/ui/state_views.dart';
import '../social/feed_view.dart';

/// Home: the feed of followed users and the movie strips.
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return DefaultTabController(
      length: 2,
      child: Column(
        children: [
          TabBar(
            tabs: [
              Tab(text: l10n.homeTabFeed),
              Tab(text: l10n.homeTabMovies),
            ],
          ),
          const Expanded(
            child: TabBarView(children: [FeedView(), _MoviesTab()]),
          ),
        ],
      ),
    );
  }
}

class _MoviesTab extends ConsumerWidget {
  const _MoviesTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final nowPlaying = ref.watch(nowPlayingProvider);
    final popular = ref.watch(popularMoviesProvider);

    // If one strip is still loading, show the one that's ready right away
    // instead of a single loading indicator.
    final hasError = nowPlaying.hasError && popular.hasError;
    if (hasError) {
      return ErrorView(
        message: nowPlaying.error.toString(),
        onRetry: () {
          ref.invalidate(nowPlayingProvider);
          ref.invalidate(popularMoviesProvider);
        },
      );
    }

    final isLoading = nowPlaying.isLoading && popular.isLoading;
    if (isLoading) return const LoadingView();

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(nowPlayingProvider);
        ref.invalidate(popularMoviesProvider);
        await Future.wait([
          ref.read(nowPlayingProvider.future),
          ref.read(popularMoviesProvider.future),
        ]);
      },
      child: ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          _Section(
            title: context.l10n.homeNowPlaying,
            value: nowPlaying,
            onRetry: () => ref.invalidate(nowPlayingProvider),
          ),
          _Section(
            title: context.l10n.homePopular,
            value: popular,
            onRetry: () => ref.invalidate(popularMoviesProvider),
          ),
        ],
      ),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({
    required this.title,
    required this.value,
    required this.onRetry,
  });

  final String title;
  final AsyncValue<List<Movie>> value;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return value.when(
      loading: () => const Padding(
        padding: EdgeInsets.symmetric(vertical: 32),
        child: LoadingView(),
      ),
      error: (error, _) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: ErrorView(message: error.toString(), onRetry: onRetry),
      ),
      data: (movies) => movies.isEmpty
          ? const SizedBox.shrink()
          : MovieRow(title: title, movies: movies),
    );
  }
}
