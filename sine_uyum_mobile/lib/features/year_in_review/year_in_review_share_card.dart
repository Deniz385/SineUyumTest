import 'package:flutter/material.dart';

import 'package:intl/intl.dart';

import '../../data/models/year_in_review.dart';
import '../../l10n/l10n.dart';

/// The recap as a 9:16 story image: laid out at [size] logical pixels and
/// captured at 3x, i.e. 1080×1920 (Instagram / WhatsApp story size).
///
/// [YearInReview.isMine] picks the voice ("Your 2026 in film" vs. "ayse's
/// 2026 in film"), like the film identity card.
class YearInReviewShareCard extends StatelessWidget {
  const YearInReviewShareCard({super.key, required this.recap});

  final YearInReview recap;

  static const size = Size(360, 640);
  static const pixelRatio = 3.0;

  static const _gold = Color(0xFFF5B841); // AppColors.dark.primary
  static const _label = TextStyle(
    color: Color(0xFFF5B841),
    fontSize: 11,
    fontWeight: FontWeight.w700,
    letterSpacing: 1.4,
  );
  static const _value = TextStyle(
    color: Colors.white,
    fontSize: 18,
    fontWeight: FontWeight.w600,
    height: 1.2,
  );

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final mine = recap.isMine;
    final identity = recap.identity;
    final topGenre = identity.topGenres.isEmpty
        ? null
        : identity.topGenres.first.name;
    final favorite =
        identity.favorite?.title ??
        (recap.topMovies.isEmpty ? null : recap.topMovies.first.title);
    final busiest = recap.busiestMonth;

    Widget fact(String label, String value) => Padding(
      padding: const EdgeInsets.only(top: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: _label),
          const SizedBox(height: 3),
          Text(
            value,
            style: _value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );

    return SizedBox.fromSize(
      size: size,
      child: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Color(0xFF3A2A0C), Color(0xFF14161C), Color(0xFF3B1214)],
          ),
        ),
        // Its own text defaults, wherever it is placed.
        child: Material(
          type: MaterialType.transparency,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(28, 36, 28, 28),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(l10n.recapCardHeader, style: _label),
                const SizedBox(height: 6),
                Text(
                  '@${recap.userName}',
                  style: const TextStyle(color: Colors.white70, fontSize: 14),
                ),
                const SizedBox(height: 20),
                Text(
                  mine
                      ? l10n.recapIntroMine(recap.year)
                      : l10n.recapIntroTheirs(recap.userName, recap.year),
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 26,
                    fontWeight: FontWeight.w800,
                    height: 1.15,
                  ),
                ),
                const SizedBox(height: 12),
                Text(
                  l10n.decimal(recap.totals.movies, digits: 0),
                  style: const TextStyle(
                    color: _gold,
                    fontSize: 72,
                    fontWeight: FontWeight.w900,
                    height: 1,
                  ),
                ),
                Text(
                  mine
                      ? l10n.recapIntroMoviesMine(recap.totals.movies)
                      : l10n.recapIntroMoviesTheirs(recap.totals.movies),
                  style: const TextStyle(color: Colors.white, fontSize: 16),
                ),
                if (topGenre != null) fact(l10n.recapCardTopGenre, topGenre),
                if (favorite != null)
                  fact(
                    mine
                        ? l10n.identityFavoriteMine
                        : l10n.identityFavoriteTheirs,
                    favorite,
                  ),
                if (busiest != null)
                  fact(
                    l10n.recapCardBusiestMonth,
                    DateFormat.MMMM(l10n.localeName)
                        .format(DateTime(recap.year, busiest.month)),
                  ),
                const Spacer(),
                if (identity.title.isNotEmpty) ...[
                  Text(l10n.recapCardIdentity, style: _label),
                  const SizedBox(height: 4),
                  Text(
                    identity.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: _gold,
                      fontSize: 28,
                      fontWeight: FontWeight.w800,
                      height: 1.1,
                    ),
                  ),
                  const SizedBox(height: 16),
                ],
                Align(
                  alignment: AlignmentDirectional.centerEnd,
                  child: Text(
                    l10n.appName,
                    style: const TextStyle(
                      color: Colors.white54,
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 1,
                    ),
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
