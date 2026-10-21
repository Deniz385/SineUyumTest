import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/movie.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/movie_widgets.dart';
import '../../widgets/state_views.dart';
import '../social/feed_view.dart';

/// Ana sayfa: takip edilenlerin akışı ve film şeritleri.
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return const DefaultTabController(
      length: 2,
      child: Column(
        children: [
          TabBar(
            tabs: [
              Tab(text: 'Akış'),
              Tab(text: 'Filmler'),
            ],
          ),
          Expanded(child: TabBarView(children: [FeedView(), _MoviesTab()])),
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

    // İki şeritten biri yüklenmeye devam ediyorsa tek bir yükleniyor
    // göstergesi yerine, hazır olan şeridi hemen gösteriyoruz.
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
            title: 'Vizyondakiler',
            value: nowPlaying,
            onRetry: () => ref.invalidate(nowPlayingProvider),
          ),
          _Section(
            title: 'Popüler filmler',
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
          : MovieCarousel(title: title, movies: movies),
    );
  }
}
