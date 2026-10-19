import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../data/models/movie.dart';
import 'poster_image.dart';

/// Ana sayfadaki yatay film şeridi.
class MovieCarousel extends StatelessWidget {
  const MovieCarousel({super.key, required this.title, required this.movies});

  final String title;
  final List<Movie> movies;

  @override
  Widget build(BuildContext context) {
    if (movies.isEmpty) return const SizedBox.shrink();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 20, 16, 10),
          child: Text(title, style: Theme.of(context).textTheme.titleMedium),
        ),
        SizedBox(
          height: 220,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            itemCount: movies.length,
            separatorBuilder: (_, _) => const SizedBox(width: 12),
            itemBuilder: (context, index) =>
                MoviePosterCard(movie: movies[index], width: 120),
          ),
        ),
      ],
    );
  }
}

/// Poster + başlık; dokununca film detayına gider.
class MoviePosterCard extends StatelessWidget {
  const MoviePosterCard({super.key, required this.movie, this.width = 120});

  final Movie movie;
  final double width;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: () => context.push('/movie/${movie.id}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            PosterImage(
              posterPath: movie.posterPath,
              width: width,
              height: width * 1.5,
            ),
            const SizedBox(height: 6),
            Text(
              movie.title,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}

/// Arama sonuçları ve liste ekranlarındaki satır biçimi.
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
      leading: PosterImage(posterPath: posterPath, width: 44, height: 66),
      title: Text(title, maxLines: 2, overflow: TextOverflow.ellipsis),
      subtitle: subtitle == null ? null : Text(subtitle!),
      trailing: trailing,
      onTap: onTap ?? () => context.push('/movie/$movieId'),
    );
  }
}

/// 1-10 arası puan seçtiren alt sayfa.
class RatingSheet extends StatefulWidget {
  const RatingSheet({super.key, required this.movieTitle, this.initialRating});

  final String movieTitle;
  final int? initialRating;

  /// Seçilen puanı döner; iptal edilirse null.
  static Future<int?> show(
    BuildContext context, {
    required String movieTitle,
    int? initialRating,
  }) {
    return showModalBottomSheet<int>(
      context: context,
      isScrollControlled: true,
      builder: (_) =>
          RatingSheet(movieTitle: movieTitle, initialRating: initialRating),
    );
  }

  @override
  State<RatingSheet> createState() => _RatingSheetState();
}

class _RatingSheetState extends State<RatingSheet> {
  late double _rating = (widget.initialRating ?? 7).toDouble();

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              widget.movieTitle,
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            Text(
              'Bu filme kaç puan veriyorsun?',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: Slider(
                    value: _rating,
                    min: 1,
                    max: 10,
                    divisions: 9,
                    label: _rating.round().toString(),
                    onChanged: (value) => setState(() => _rating = value),
                  ),
                ),
                SizedBox(
                  width: 40,
                  child: Text(
                    '${_rating.round()}',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(_rating.round()),
              child: const Text('Puanı kaydet'),
            ),
          ],
        ),
      ),
    );
  }
}
