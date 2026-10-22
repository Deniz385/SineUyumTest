import 'diary.dart';
import 'movie.dart';
import 'profile.dart';
import 'social.dart';

Map<String, dynamic>? _mapOrNull(Object? value) =>
    value is Map ? value.cast<String, dynamic>() : null;

int _int(Object? value) => (value as num?)?.toInt() ?? 0;

/// `GET /api/v1/year-in-review/user/{userId}?year=`: a year in movies, built
/// from the diary and the ratings given that year.
///
/// Texts that need grammar (the identity title and description, genre names,
/// movie titles) come localized from the server; labels, month and weekday
/// names and numbers are formatted here. Every part except [year] may be
/// missing or null; parsing never throws on a partial response.
class YearInReview {
  const YearInReview({
    required this.year,
    this.userName = '',
    this.isMine = false,
    this.ready = false,
    this.minMovies = 3,
    this.availableYears = const [],
    this.totals = const YearTotals(),
    this.monthCounts = const [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    this.busiestMonth,
    this.favoriteWeekday,
    this.averageRating,
    this.previousYearAverage,
    this.identity = const FilmIdentity(
      userName: '',
      title: '',
      titleDescription: '',
    ),
    this.topMovies = const [],
    this.firstMovie,
    this.lastMovie,
    this.mostRewatched,
    this.tasteMate,
  });

  final int year;
  final String userName;

  /// The viewer is the owner (first-person wording).
  final bool isMine;

  /// False when the year has too few movies ([minMovies]) for a recap.
  final bool ready;
  final int minMovies;

  /// Years with any activity, newest first.
  final List<int> availableYears;
  final YearTotals totals;

  /// Always 12 counts, January first.
  final List<int> monthCounts;
  final ({int month, int count})? busiestMonth;

  /// [weekday] is ISO: 1 = Monday ... 7 = Sunday (same as [DateTime.weekday]).
  final ({int weekday, int count})? favoriteWeekday;
  final double? averageRating;
  final double? previousYearAverage;

  /// The year's taste profile: title, top genres, favorite, contrarian take.
  final FilmIdentity identity;

  /// Highest rated movies of the year (up to 5).
  final List<IdentityMovie> topMovies;
  final DatedMovie? firstMovie;
  final DatedMovie? lastMovie;
  final ({Movie movie, int count})? mostRewatched;
  final TasteMate? tasteMate;

  int countFor(int month) => monthCounts[month - 1];

  factory YearInReview.fromJson(Map<String, dynamic> json) {
    final counts = List<int>.filled(12, 0);
    for (final item in (json['months'] as List?) ?? const []) {
      final map = _mapOrNull(item);
      final month = (map?['month'] as num?)?.toInt();
      if (month == null || month < 1 || month > 12) continue;
      counts[month - 1] = _int(map!['count']);
    }

    final busiest = _mapOrNull(json['busiestMonth']);
    final busiestMonth = (busiest?['month'] as num?)?.toInt();
    final weekday = _mapOrNull(json['favoriteWeekday']);
    final weekdayValue = (weekday?['weekday'] as num?)?.toInt();
    final rewatched = _mapOrNull(json['mostRewatched']);
    final rewatchedMovie = _mapOrNull(rewatched?['movie']);

    return YearInReview(
      year: (json['year'] as num?)?.toInt() ?? DateTime.now().year,
      userName: json['userName'] as String? ?? '',
      isMine: json['isMine'] as bool? ?? false,
      ready: json['ready'] as bool? ?? false,
      minMovies: (json['minMovies'] as num?)?.toInt() ?? 3,
      availableYears:
          (json['availableYears'] as List?)
              ?.whereType<num>()
              .map((y) => y.toInt())
              .toList() ??
          const [],
      totals: YearTotals.fromJson(_mapOrNull(json['totals']) ?? const {}),
      monthCounts: counts,
      busiestMonth:
          busiestMonth == null || busiestMonth < 1 || busiestMonth > 12
          ? null
          : (month: busiestMonth, count: _int(busiest!['count'])),
      favoriteWeekday:
          weekdayValue == null || weekdayValue < 1 || weekdayValue > 7
          ? null
          : (weekday: weekdayValue, count: _int(weekday!['count'])),
      averageRating: (json['averageRating'] as num?)?.toDouble(),
      previousYearAverage: (json['previousYearAverage'] as num?)?.toDouble(),
      identity: FilmIdentity.fromJson(_mapOrNull(json['identity']) ?? const {}),
      topMovies: [
        for (final m in (json['topMovies'] as List?) ?? const [])
          ?IdentityMovie.tryParse(m),
      ],
      firstMovie: DatedMovie.tryParse(json['firstMovie']),
      lastMovie: DatedMovie.tryParse(json['lastMovie']),
      mostRewatched: rewatchedMovie == null
          ? null
          : (
              movie: Movie.fromJson(rewatchedMovie),
              count: _int(rewatched!['count']),
            ),
      tasteMate: TasteMate.tryParse(json['tasteMate']),
    );
  }
}

class YearTotals {
  const YearTotals({
    this.movies = 0,
    this.diaryEntries = 0,
    this.rewatches = 0,
    this.ratings = 0,
    this.reviews = 0,
    this.likesReceived = 0,
    this.commentsReceived = 0,
  });

  /// Distinct movies watched (diary) or rated that year.
  final int movies;
  final int diaryEntries;
  final int rewatches;
  final int ratings;
  final int reviews;
  final int likesReceived;
  final int commentsReceived;

  factory YearTotals.fromJson(Map<String, dynamic> json) => YearTotals(
    movies: _int(json['movies']),
    diaryEntries: _int(json['diaryEntries']),
    rewatches: _int(json['rewatches']),
    ratings: _int(json['ratings']),
    reviews: _int(json['reviews']),
    likesReceived: _int(json['likesReceived']),
    commentsReceived: _int(json['commentsReceived']),
  );
}

/// A movie and the day it was watched (first / last of the year).
class DatedMovie {
  const DatedMovie({required this.movie, required this.watchedOn});

  final Movie movie;
  final DateTime watchedOn;

  static DatedMovie? tryParse(Object? json) {
    final map = _mapOrNull(json);
    final movie = _mapOrNull(map?['movie']);
    final watchedOn = parseDiaryDate(map?['watchedOn']);
    if (movie == null || watchedOn == null) return null;
    return DatedMovie(movie: Movie.fromJson(movie), watchedOn: watchedOn);
  }
}

/// The most compatible person the owner follows.
class TasteMate {
  const TasteMate({
    required this.user,
    this.compatibility = 0,
    this.commonCount = 0,
  });

  final Author user;

  /// 0-100.
  final double compatibility;
  final int commonCount;

  static TasteMate? tryParse(Object? json) {
    final map = _mapOrNull(json);
    final user = _mapOrNull(map?['user']);
    if (map == null || user == null) return null;
    return TasteMate(
      user: Author.fromJson(user),
      compatibility: (map['compatibility'] as num?)?.toDouble() ?? 0,
      commonCount: _int(map['commonCount']),
    );
  }
}
