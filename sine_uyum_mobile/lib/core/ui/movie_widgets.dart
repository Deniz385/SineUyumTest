import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/movie.dart';
import '../../l10n/l10n.dart';
import '../theme/tokens.dart';
import 'poster_image.dart';
import 'rating_input.dart';
import 'skeleton.dart';
import 'state_views.dart';

/// PosterCard widths from the spec.
enum PosterCardSize {
  s(96),
  m(140),
  l(200);

  const PosterCardSize(this.width);

  final double width;
}

/// Poster + title (2 lines max) + optional meta row (year, score,
/// compatibility); opens the movie on tap and scales to 0.98 on press.
class PosterCard extends StatefulWidget {
  const PosterCard({
    super.key,
    required this.movieId,
    required this.title,
    this.posterPath,
    this.size = PosterCardSize.m,
    this.year,
    this.score,
    this.compatibility,
    this.onTap,
    this.width,
  });

  PosterCard.movie(
    Movie movie, {
    Key? key,
    PosterCardSize size = PosterCardSize.m,
    double? width,
    VoidCallback? onTap,
  }) : this(
         key: key,
         movieId: movie.id,
         title: movie.title,
         posterPath: movie.posterPath,
         size: size,
         year: movie.year,
         score: movie.voteAverage,
         width: width,
         onTap: onTap,
       );

  final int movieId;
  final String title;
  final String? posterPath;
  final PosterCardSize size;
  final String? year;

  /// 0–10 (TMDb vote average or a user's rating).
  final num? score;
  final double? compatibility;
  final VoidCallback? onTap;

  /// Overrides the size's width (grids).
  final double? width;

  /// Height of the card at [width] for the current text scale (for rows).
  static double heightFor(BuildContext context, double width) {
    final scaler = MediaQuery.textScalerOf(context);
    final theme = Theme.of(context).textTheme;
    final title = theme.titleSmall!;
    final meta = theme.labelMedium!;
    final titleLine = scaler.scale(title.fontSize!) * title.height!;
    final metaLine = scaler.scale(meta.fontSize!) * meta.height!;
    return width * 1.5 + 8 + titleLine * 2 + 2 + metaLine + 4;
  }

  @override
  State<PosterCard> createState() => _PosterCardState();
}

class _PosterCardState extends State<PosterCard> {
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final width = widget.width ?? widget.size.width;
    final score = widget.score;
    final meta = [
      if (widget.year != null) widget.year!,
      if (score != null && score > 0) '★ ${l10n.decimal(score)}',
    ].join(' · ');

    return Semantics(
      button: true,
      label: [widget.title, ?widget.year].join(', '),
      excludeSemantics: true,
      onTap: widget.onTap ?? () => context.push('/movie/${widget.movieId}'),
      child: Listener(
        onPointerDown: (_) => setState(() => _pressed = true),
        onPointerUp: (_) => setState(() => _pressed = false),
        onPointerCancel: (_) => setState(() => _pressed = false),
        child: AnimatedScale(
          scale: _pressed && !AppMotion.reduced(context) ? 0.98 : 1,
          duration: AppMotion.fast,
          curve: AppMotion.standardCurve,
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap:
                widget.onTap ?? () => context.push('/movie/${widget.movieId}'),
            child: SizedBox(
              width: width,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Stack(
                    children: [
                      PosterImage(
                        posterPath: widget.posterPath,
                        width: width,
                        size: width > 150 ? 'w342' : 'w185',
                      ),
                      if (widget.compatibility != null)
                        Positioned(
                          left: 6,
                          top: 6,
                          child: CompatibilityBadge(
                            score: widget.compatibility!,
                          ),
                        ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    widget.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: widget.size == PosterCardSize.l
                        ? theme.textTheme.titleMedium
                        : theme.textTheme.titleSmall,
                  ),
                  if (meta.isNotEmpty) ...[
                    const SizedBox(height: 2),
                    Text(
                      meta,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.labelMedium,
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A horizontal, snap-scrolling carousel of [PosterCard]s with a section
/// title and "See all". [movies] null: still loading (skeleton); [error]
/// set: a compact retry.
class MovieRow extends StatelessWidget {
  const MovieRow({
    super.key,
    required this.title,
    required this.movies,
    this.size = PosterCardSize.m,
    this.onSeeAll,
    this.error,
    this.onRetry,
  });

  final String title;
  final List<Movie>? movies;
  final PosterCardSize size;
  final VoidCallback? onSeeAll;
  final Object? error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final list = movies;
    if (error == null && list != null && list.isEmpty) {
      return const SizedBox.shrink();
    }
    final width = size.width;
    const gap = AppSpace.sm;
    final height = PosterCard.heightFor(context, width);

    final Widget body;
    if (error != null) {
      body = ErrorView(message: error.toString(), onRetry: onRetry);
    } else if (list == null) {
      body = SizedBox(
        height: height,
        child: SkeletonPosterRow(posterWidth: width),
      );
    } else {
      body = SizedBox(
        height: height,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          physics: _SnapPhysics(itemExtent: width + gap),
          padding: const EdgeInsets.symmetric(horizontal: AppSpace.gutter),
          itemCount: list.length,
          separatorBuilder: (_, _) => const SizedBox(width: gap),
          itemBuilder: (context, index) =>
              PosterCard.movie(list[index], size: size),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(title: title, onSeeAll: list == null ? null : onSeeAll),
        body,
      ],
    );
  }
}

/// Settles a horizontal list on whole items.
class _SnapPhysics extends ScrollPhysics {
  const _SnapPhysics({required this.itemExtent, super.parent});

  final double itemExtent;

  @override
  _SnapPhysics applyTo(ScrollPhysics? ancestor) =>
      _SnapPhysics(itemExtent: itemExtent, parent: buildParent(ancestor));

  @override
  Simulation? createBallisticSimulation(
    ScrollMetrics position,
    double velocity,
  ) {
    if ((velocity <= 0 && position.pixels <= position.minScrollExtent) ||
        (velocity >= 0 && position.pixels >= position.maxScrollExtent)) {
      return super.createBallisticSimulation(position, velocity);
    }
    final tolerance = toleranceFor(position);
    var page = position.pixels / itemExtent;
    if (velocity < -tolerance.velocity) {
      page -= 0.5;
    } else if (velocity > tolerance.velocity) {
      page += 0.5;
    }
    final target = (page.roundToDouble() * itemExtent).clamp(
      position.minScrollExtent,
      position.maxScrollExtent,
    );
    if ((target - position.pixels).abs() < tolerance.distance) return null;
    return ScrollSpringSimulation(
      spring,
      position.pixels,
      target,
      velocity,
      tolerance: tolerance,
    );
  }

  @override
  bool get allowImplicitScrolling => false;
}

/// Row layout used by search results and list screens.
class MovieListTile extends StatelessWidget {
  const MovieListTile({
    super.key,
    required this.movieId,
    required this.title,
    this.posterPath,
    this.subtitle,
    this.trailing,
    this.onTap,
  });

  final int movieId;
  final String title;
  final String? posterPath;
  final String? subtitle;
  final Widget? trailing;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: PosterImage(
        posterPath: posterPath,
        width: 44,
        size: 'w185',
        borderRadius: AppRadii.sm,
      ),
      title: Text(title, maxLines: 2, overflow: TextOverflow.ellipsis),
      subtitle: subtitle == null ? null : Text(subtitle!),
      trailing: trailing,
      onTap: onTap ?? () => context.push('/movie/$movieId'),
    );
  }
}

/// Bottom sheet for picking a 1–10 rating with [RatingInput].
class RatingSheet extends StatefulWidget {
  const RatingSheet({
    super.key,
    required this.movieTitle,
    this.initialRating,
    this.posterPath,
  });

  final String movieTitle;
  final int? initialRating;
  final String? posterPath;

  /// Returns the chosen rating; null if dismissed.
  static Future<int?> show(
    BuildContext context, {
    required String movieTitle,
    int? initialRating,
    String? posterPath,
  }) {
    return showModalBottomSheet<int>(
      context: context,
      isScrollControlled: true,
      builder: (_) => RatingSheet(
        movieTitle: movieTitle,
        initialRating: initialRating,
        posterPath: posterPath,
      ),
    );
  }

  @override
  State<RatingSheet> createState() => _RatingSheetState();
}

class _RatingSheetState extends State<RatingSheet> {
  late int? _rating = widget.initialRating;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
          AppSpace.lg,
          0,
          AppSpace.lg,
          AppSpace.lg,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                if (widget.posterPath != null) ...[
                  PosterImage(
                    posterPath: widget.posterPath,
                    width: 48,
                    size: 'w185',
                    borderRadius: AppRadii.sm,
                  ),
                  const SizedBox(width: AppSpace.md),
                ],
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(widget.movieTitle, style: context.type.h2),
                      const SizedBox(height: 4),
                      Text(
                        l10n.ratingSheetPrompt,
                        style: theme.textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpace.xl),
            Center(
              child: FittedBox(
                fit: BoxFit.scaleDown,
                child: RatingInput(
                  value: _rating,
                  onChanged: (value) => setState(() => _rating = value),
                ),
              ),
            ),
            const SizedBox(height: AppSpace.xs),
            Center(
              child: Text(
                l10n.ratingInputHint,
                style: theme.textTheme.labelMedium,
              ),
            ),
            const SizedBox(height: AppSpace.xl),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: _rating == null
                    ? null
                    : () => Navigator.of(context).pop(_rating),
                child: Text(l10n.saveRating),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
