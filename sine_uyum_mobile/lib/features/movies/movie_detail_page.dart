import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:share_plus/share_plus.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/movie_widgets.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/skeleton.dart';
import '../../core/ui/state_views.dart';
import '../../data/models/movie.dart';
import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/diary_provider.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../providers/social_provider.dart';
import '../../providers/watchlist_provider.dart';
import '../diary/diary_entry_sheet.dart';
import '../social/movie_reviews_section.dart';

/// A movie: backdrop hero with a scrim, poster, title, year, runtime and
/// genres; the "for you" and TMDb scores side by side; one action row
/// (Rate · Diary · Add to list · Share); the cast and the reviews.
class MovieDetailPage extends ConsumerWidget {
  const MovieDetailPage({super.key, required this.movieId});

  final int movieId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final movie = ref.watch(movieDetailProvider(movieId));

    return Scaffold(
      body: movie.when(
        loading: () => const _DetailSkeleton(),
        error: (error, _) => CustomScrollView(
          slivers: [
            SliverAppBar(title: Text(context.l10n.movieDetailTitle)),
            SliverFillRemaining(
              hasScrollBody: false,
              child: ErrorView(
                message: error.toString(),
                onRetry: () => ref.invalidate(movieDetailProvider(movieId)),
              ),
            ),
          ],
        ),
        data: (movie) => _MovieDetailBody(movie: movie),
      ),
    );
  }
}

class _DetailSkeleton extends StatelessWidget {
  const _DetailSkeleton();

  @override
  Widget build(BuildContext context) => CustomScrollView(
    slivers: [
      SliverAppBar(title: Text(context.l10n.movieDetailTitle)),
      const SliverToBoxAdapter(
        child: Shimmer(
          child: Padding(
            padding: EdgeInsets.all(AppSpace.gutter),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SkeletonBox(height: 180, radius: AppRadii.lg),
                SizedBox(height: AppSpace.md),
                Row(
                  children: [
                    SkeletonBox(width: 100, height: 150, radius: AppRadii.md),
                    SizedBox(width: AppSpace.md),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          SkeletonBox(height: 24),
                          SizedBox(height: 8),
                          FractionallySizedBox(
                            widthFactor: 0.5,
                            child: SkeletonBox(),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    ],
  );
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
    final current = ref.read(movieReviewsProvider(_movie.id)).value?.myRating;
    final rating = await RatingSheet.show(
      context,
      movieTitle: _movie.title,
      posterPath: _movie.posterPath,
      initialRating: current,
    );
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
      isScrollControlled: true,
      builder: (context) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          padding: const EdgeInsets.only(bottom: AppSpace.md),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpace.lg,
                0,
                AppSpace.lg,
                AppSpace.xs,
              ),
              child: Text(l10n.watchlistPickTitle, style: context.type.h2),
            ),
            for (final list in lists)
              ListTile(
                leading: Icon(
                  list.isShared ? Icons.group_rounded : Icons.bookmark_rounded,
                  color: context.colors.primary,
                ),
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

  Future<void> _share() async {
    final l10n = context.l10n;
    final title = _movie.year == null
        ? _movie.title
        : '${_movie.title} (${_movie.year})';
    try {
      await SharePlus.instance.share(
        ShareParams(
          text: l10n.movieShareText(
            title,
            'https://www.themoviedb.org/movie/${_movie.id}',
          ),
        ),
      );
    } catch (_) {
      _showMessage(l10n.shareFailed);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final theme = Theme.of(context);
    final myRating = ref.watch(movieReviewsProvider(_movie.id)).value?.myRating;
    final width = MediaQuery.sizeOf(context).width;
    // The poster and title sit on the backdrop's lower edge.
    final heroHeight = (width * 9 / 16).clamp(200.0, 340.0) + 110;
    final meta = [
      ?_movie.year,
      if (_movie.runtime != null && _movie.runtime! > 0)
        l10n.runtimeMinutes(_movie.runtime!),
    ].join(' · ');

    return CustomScrollView(
      slivers: [
        SliverAppBar(
          pinned: true,
          stretch: true,
          expandedHeight: heroHeight,
          backgroundColor: colors.bg,
          leading: const _HeroButton(child: BackButton()),
          title: _CollapsingTitle(title: _movie.title),
          flexibleSpace: FlexibleSpaceBar(
            collapseMode: CollapseMode.parallax,
            stretchModes: const [StretchMode.zoomBackground],
            background: Stack(
              fit: StackFit.expand,
              children: [
                BackdropImage(path: _movie.backdropPath ?? _movie.posterPath),
                // Backdrop scrim: bg from 0% at the top to 100% at the
                // bottom, over a 40% bg layer (text stays above 4.5:1).
                ColoredBox(color: colors.bg.withValues(alpha: 0.4)),
                DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      stops: const [0.2, 0.85],
                      colors: [colors.bg.withValues(alpha: 0), colors.bg],
                    ),
                  ),
                ),
                Positioned(
                  left: AppSpace.gutter,
                  right: AppSpace.gutter,
                  bottom: 0,
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      PosterImage(
                        posterPath: _movie.posterPath,
                        title: _movie.title,
                        width: 112,
                      ),
                      const SizedBox(width: AppSpace.md),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Semantics(
                              header: true,
                              child: Text(
                                _movie.title,
                                maxLines: 3,
                                overflow: TextOverflow.ellipsis,
                                style: context.type.h1,
                              ),
                            ),
                            if (meta.isNotEmpty) ...[
                              const SizedBox(height: AppSpace.xxs),
                              Text(meta, style: theme.textTheme.bodySmall),
                            ],
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpace.gutter),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (_movie.genres.isNotEmpty) ...[
                  const SizedBox(height: AppSpace.sm),
                  Wrap(
                    spacing: AppSpace.xs,
                    runSpacing: AppSpace.xs,
                    children: [
                      for (final genre in _movie.genres)
                        Chip(label: Text(genre)),
                    ],
                  ),
                ],
                const SizedBox(height: AppSpace.lg),
                _Scores(movie: _movie),
                const SizedBox(height: AppSpace.lg),
                _ActionRow(
                  busy: _isBusy,
                  myRating: myRating,
                  onRate: _rate,
                  onDiary: _addToDiary,
                  onList: _addToWatchlist,
                  onShare: _share,
                ),
                if (_movie.overview != null && _movie.overview!.isNotEmpty) ...[
                  SectionHeader(
                    title: l10n.overviewTitle,
                    padding: const EdgeInsets.only(
                      top: AppSpace.xl,
                      bottom: AppSpace.xs,
                    ),
                  ),
                  Text(_movie.overview!, style: context.type.body),
                ],
              ],
            ),
          ),
        ),
        if (_movie.cast.isNotEmpty)
          SliverToBoxAdapter(child: _CastRow(cast: _movie.cast)),
        SliverPadding(
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            0,
            AppSpace.gutter,
            AppSpace.xxl,
          ),
          sliver: SliverToBoxAdapter(
            child: MovieReviewsSection(movieId: _movie.id),
          ),
        ),
      ],
    );
  }
}

/// The title fades into the collapsed app bar once the hero scrolls away.
class _CollapsingTitle extends StatelessWidget {
  const _CollapsingTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    final settings = context
        .dependOnInheritedWidgetOfExactType<FlexibleSpaceBarSettings>();
    var visible = 0.0;
    if (settings != null) {
      final range = settings.maxExtent - settings.minExtent;
      visible = range <= 0
          ? 1
          : (1 - (settings.currentExtent - settings.minExtent) / range * 4)
                .clamp(0.0, 1.0);
    }
    return ExcludeSemantics(
      excluding: visible < 0.5,
      child: Opacity(
        opacity: visible,
        child: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
      ),
    );
  }
}

/// App bar buttons over artwork get a dark disc so they stay visible.
class _HeroButton extends StatelessWidget {
  const _HeroButton({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Padding(
      padding: const EdgeInsets.all(4),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: colors.bg.withValues(alpha: 0.6),
          shape: BoxShape.circle,
        ),
        child: IconTheme(
          data: IconThemeData(color: colors.text),
          child: child,
        ),
      ),
    );
  }
}

/// "For you" (the score of people who match your taste) next to TMDb's.
class _Scores extends ConsumerWidget {
  const _Scores({required this.movie});

  final Movie movie;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vote = movie.voteAverage;
    final forYou =
        ref.watch(movieReviewsProvider(movie.id)).value?.forYou != null;
    final tmdb = vote != null && vote > 0;
    if (!forYou && !tmdb) return const SizedBox.shrink();
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (forYou) Expanded(child: ForYouBadge(movieId: movie.id)),
          if (tmdb) ...[
            if (forYou) const SizedBox(width: AppSpace.sm),
            Expanded(
              child: ScoreTile(
                label: 'TMDb',
                capitals: false,
                score: vote,
                caption: context.l10n.tmdbScoreCaption,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.busy,
    required this.myRating,
    required this.onRate,
    required this.onDiary,
    required this.onList,
    required this.onShare,
  });

  final bool busy;
  final int? myRating;
  final VoidCallback onRate;
  final VoidCallback onDiary;
  final VoidCallback onList;
  final VoidCallback onShare;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final rated = myRating;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _Action(
          icon: rated == null ? Icons.star_outline_rounded : Icons.star_rounded,
          label: rated == null ? l10n.rateMovie : l10n.ratingOutOfTen(rated),
          semanticsLabel: rated == null
              ? l10n.rateMovie
              : '${l10n.rateMovie}, ${l10n.ratingSpoken(rated)}',
          highlighted: rated != null,
          onTap: busy ? null : onRate,
        ),
        _Action(
          icon: Icons.edit_calendar_rounded,
          label: l10n.diaryAdd,
          onTap: busy ? null : onDiary,
        ),
        _Action(
          icon: Icons.bookmark_add_rounded,
          label: l10n.addToList,
          onTap: busy ? null : onList,
        ),
        _Action(
          icon: Icons.ios_share_rounded,
          label: l10n.recapShare,
          onTap: onShare,
        ),
      ],
    );
  }
}

class _Action extends StatelessWidget {
  const _Action({
    required this.icon,
    required this.label,
    required this.onTap,
    this.semanticsLabel,
    this.highlighted = false,
  });

  final IconData icon;
  final String label;
  final String? semanticsLabel;
  final VoidCallback? onTap;
  final bool highlighted;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Expanded(
      child: Semantics(
        button: true,
        enabled: onTap != null,
        label: semanticsLabel ?? label,
        excludeSemantics: true,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(AppRadii.md),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: AppSpace.xs),
            child: Column(
              children: [
                Container(
                  width: 52,
                  height: 52,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: highlighted
                        ? colors.primary.withValues(alpha: 0.18)
                        : colors.surfaceRaised,
                    border: Border.all(
                      color: highlighted ? colors.primary : colors.border,
                    ),
                  ),
                  child: Icon(
                    icon,
                    color: highlighted ? colors.primary : colors.text,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  label,
                  textAlign: TextAlign.center,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: onTap == null ? colors.textMuted : colors.text,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _CastRow extends StatelessWidget {
  const _CastRow({required this.cast});

  final List<CastMember> cast;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scaler = MediaQuery.textScalerOf(context);
    const width = 88.0;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(title: context.l10n.castTitle),
        SizedBox(
          height: width + 8 + scaler.scale(1) * (20 + 32) + 8,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: AppSpace.gutter),
            itemCount: cast.length,
            separatorBuilder: (_, _) => const SizedBox(width: AppSpace.sm),
            itemBuilder: (context, index) {
              final person = cast[index];
              return SizedBox(
                width: width,
                child: Semantics(
                  label: [person.name, ?person.character].join(', '),
                  excludeSemantics: true,
                  child: Column(
                    children: [
                      ClipOval(
                        child: SizedBox.square(
                          dimension: width,
                          child: person.profilePath == null
                              ? ColoredBox(
                                  color: context.colors.surfaceRaised,
                                  child: Icon(
                                    Icons.person_rounded,
                                    size: 36,
                                    color: context.colors.textMuted,
                                  ),
                                )
                              : BackdropImage(
                                  path: person.profilePath,
                                  size: 'w185',
                                ),
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        person.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        textAlign: TextAlign.center,
                        style: theme.textTheme.titleSmall,
                      ),
                      if (person.character?.isNotEmpty == true)
                        Text(
                          person.character!,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          textAlign: TextAlign.center,
                          style: theme.textTheme.labelMedium,
                        ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
