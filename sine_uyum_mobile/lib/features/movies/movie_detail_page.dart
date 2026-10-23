import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/movie.dart';
import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/diary_provider.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../core/ui/movie_widgets.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/state_views.dart';
import '../../providers/social_provider.dart';
import '../../providers/watchlist_provider.dart';
import '../diary/diary_entry_sheet.dart';
import '../social/movie_reviews_section.dart';

class MovieDetailPage extends ConsumerWidget {
  const MovieDetailPage({super.key, required this.movieId});

  final int movieId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final movie = ref.watch(movieDetailProvider(movieId));

    return Scaffold(
      appBar: AppBar(title: Text(context.l10n.movieDetailTitle)),
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
      // Profile statistics changed, so the related providers are refreshed.
      ref.invalidate(myEventProvider);
      // Rated: the review field becomes available and the feed updates.
      ref.invalidate(movieReviewsProvider(_movie.id));
      ref.invalidate(feedProvider);
      if (mounted) _showMessage(context.l10n.ratingSaved(rating));
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

    final l10n = context.l10n;
    if (lists.isEmpty) {
      _showMessage(l10n.watchlistCreateFirst);
      return;
    }

    final selected = await showModalBottomSheet<WatchlistSummary>(
      context: context,
      builder: (context) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(l10n.watchlistPickTitle),
            ),
            for (final list in lists)
              ListTile(
                leading: const Icon(Icons.bookmark_outline_rounded),
                title: Text(list.name),
                subtitle: Text(l10n.movieCount(list.itemCount)),
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
      _showMessage(l10n.addedToWatchlist(selected.name));
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  Future<void> _addToDiary() async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    // My current rating (from the reviews section) prefills the sheet.
    final myRating = ref.read(movieReviewsProvider(_movie.id)).value?.myRating;

    final saved = await DiaryEntrySheet.show(
      context,
      movieTitle: _movie.title,
      initialRating: myRating,
      loadMovieEntries: () =>
          ref.read(diaryRepositoryProvider).movieEntries(_movie.id),
      onSubmit: (draft) async {
        await ref.read(diaryRepositoryProvider).create(_movie.id, draft);
        // Any open diary (e.g. the one this page was opened from) reloads.
        ref.invalidate(diaryProvider);
        if (draft.rating != null) {
          // The rating was saved too: same refreshes as rating directly.
          ref.invalidate(myEventProvider);
          ref.invalidate(movieReviewsProvider(_movie.id));
          ref.invalidate(feedProvider);
          final myId = ref.read(sessionProvider).user?.id;
          if (myId != null) ref.invalidate(userProfileProvider(myId));
        }
      },
    );
    if (saved == true) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.diaryEntryAdded)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
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
                      l10n.runtimeMinutes(_movie.runtime!),
                      style: theme.textTheme.bodySmall,
                    ),
                  if (_movie.voteAverage != null) ...[
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        const Icon(
                          Icons.star_rounded,
                          size: 16,
                          color: Colors.amber,
                        ),
                        const SizedBox(width: 4),
                        Text(l10n.decimal(_movie.voteAverage!)),
                        Text(
                          l10n.tmdbScoreSuffix,
                          style: theme.textTheme.bodySmall,
                        ),
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
                icon: const Icon(Icons.star_outline_rounded),
                label: Text(l10n.rateMovie),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: _isBusy ? null : _addToWatchlist,
                icon: const Icon(Icons.bookmark_add_rounded),
                label: Text(l10n.addToList),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: _isBusy ? null : _addToDiary,
          icon: const Icon(Icons.edit_calendar_rounded),
          label: Text(l10n.diaryAdd),
        ),
        if (_movie.overview != null && _movie.overview!.isNotEmpty) ...[
          const SizedBox(height: 24),
          Text(l10n.overviewTitle, style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          Text(_movie.overview!, style: theme.textTheme.bodyMedium),
        ],
        MovieReviewsSection(movieId: _movie.id),
      ],
    );
  }
}
