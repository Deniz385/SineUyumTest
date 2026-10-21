import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/movie.dart';
import '../../data/models/watchlist.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/movie_widgets.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';
import '../../providers/social_provider.dart';
import '../social/movie_reviews_section.dart';

class MovieDetailPage extends ConsumerWidget {
  const MovieDetailPage({super.key, required this.movieId});

  final int movieId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final movie = ref.watch(movieDetailProvider(movieId));

    return Scaffold(
      appBar: AppBar(title: const Text('Film detayı')),
      body: movie.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(movieDetailProvider(movieId)),
        ),
        data: (movie) => _MovieDetailBody(movie: movie),
      ),
    );
  }
}

class _MovieDetailBody extends ConsumerStatefulWidget {
  const _MovieDetailBody({required this.movie});

  final Movie movie;

  @override
  ConsumerState<_MovieDetailBody> createState() => _MovieDetailBodyState();
}

class _MovieDetailBodyState extends ConsumerState<_MovieDetailBody> {
  bool _isBusy = false;

  Movie get _movie => widget.movie;

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _rate() async {
    final rating = await RatingSheet.show(context, movieTitle: _movie.title);
    if (rating == null) return;

    setState(() => _isBusy = true);
    try {
      await ref.read(movieRepositoryProvider).rate(_movie, rating);
      // Profil istatistikleri değiştiği için ilgili sağlayıcılar tazelenir.
      ref.invalidate(myEventProvider);
      // Puan verildi: inceleme yazma alanı açılsın, akış güncellensin.
      ref.invalidate(movieReviewsProvider(_movie.id));
      ref.invalidate(feedProvider);
      _showMessage('Puanın kaydedildi: $rating/10');
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  Future<void> _addToWatchlist() async {
    final List<WatchlistSummary> lists;
    try {
      lists = await ref.read(watchlistRepositoryProvider).myLists();
    } catch (error) {
      _showMessage(error.toString());
      return;
    }

    if (!mounted) return;

    if (lists.isEmpty) {
      _showMessage('Önce "Listeler" sekmesinden bir liste oluştur.');
      return;
    }

    final selected = await showModalBottomSheet<WatchlistSummary>(
      context: context,
      builder: (context) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          children: [
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Hangi listeye eklenecek?'),
            ),
            for (final list in lists)
              ListTile(
                leading: const Icon(Icons.bookmark_outline),
                title: Text(list.name),
                subtitle: Text('${list.itemCount} film'),
                onTap: () => Navigator.of(context).pop(list),
              ),
          ],
        ),
      ),
    );

    if (selected == null) return;

    setState(() => _isBusy = true);
    try {
      await ref.read(watchlistRepositoryProvider).addMovie(selected.id, _movie);
      ref.invalidate(myWatchlistsProvider);
      ref.invalidate(watchlistDetailProvider(selected.id));
      _showMessage('"${selected.name}" listesine eklendi.');
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            PosterImage(posterPath: _movie.posterPath, width: 120, height: 180),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(_movie.title, style: theme.textTheme.titleLarge),
                  const SizedBox(height: 6),
                  if (_movie.year != null)
                    Text(_movie.year!, style: theme.textTheme.bodySmall),
                  if (_movie.runtime != null)
                    Text(
                      '${_movie.runtime} dakika',
                      style: theme.textTheme.bodySmall,
                    ),
                  if (_movie.voteAverage != null) ...[
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        const Icon(Icons.star, size: 16, color: Colors.amber),
                        const SizedBox(width: 4),
                        Text(_movie.voteAverage!.toStringAsFixed(1)),
                        Text(' / 10 (TMDb)', style: theme.textTheme.bodySmall),
                      ],
                    ),
                  ],
                  ForYouBadge(movieId: _movie.id),
                ],
              ),
            ),
          ],
        ),
        if (_movie.genres.isNotEmpty) ...[
          const SizedBox(height: 16),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final genre in _movie.genres)
                Chip(label: Text(genre), visualDensity: VisualDensity.compact),
            ],
          ),
        ],
        const SizedBox(height: 20),
        Row(
          children: [
            Expanded(
              child: FilledButton.icon(
                onPressed: _isBusy ? null : _rate,
                icon: const Icon(Icons.star_outline),
                label: const Text('Puan ver'),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: _isBusy ? null : _addToWatchlist,
                icon: const Icon(Icons.bookmark_add_outlined),
                label: const Text('Listeye ekle'),
              ),
            ),
          ],
        ),
        if (_movie.overview != null && _movie.overview!.isNotEmpty) ...[
          const SizedBox(height: 24),
          Text('Özet', style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          Text(_movie.overview!, style: theme.textTheme.bodyMedium),
        ],
        MovieReviewsSection(movieId: _movie.id),
      ],
    );
  }
}
