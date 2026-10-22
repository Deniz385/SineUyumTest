import 'movie.dart';

Map<String, dynamic> _map(Object? value) =>
    value is Map ? value.cast<String, dynamic>() : const {};

/// A calendar date without a time (the API's `DateOnly`, "yyyy-MM-dd").
/// Kept as a local midnight [DateTime] so it never shifts across time zones.
DateTime? parseDiaryDate(Object? value) {
  if (value is! String) return null;
  final match = RegExp(r'^(\d{4})-(\d{2})-(\d{2})').firstMatch(value);
  if (match == null) return null;
  return DateTime(
    int.parse(match.group(1)!),
    int.parse(match.group(2)!),
    int.parse(match.group(3)!),
  );
}

/// [date] as the API expects it: "yyyy-MM-dd" (no locale, no time zone).
String formatDiaryDate(DateTime date) =>
    '${date.year.toString().padLeft(4, '0')}-'
    '${date.month.toString().padLeft(2, '0')}-'
    '${date.day.toString().padLeft(2, '0')}';

/// One logged watch of a movie (`/api/v1/diary`). A rewatch is a separate
/// entry; [rating] is the diary owner's current rating of the movie, not
/// tied to the entry.
class DiaryEntry {
  const DiaryEntry({
    required this.id,
    required this.movie,
    required this.watchedOn,
    this.rewatch = false,
    this.note,
    this.rating,
    this.createdAt,
  });

  final int id;

  /// Only id, title and poster are sent.
  final Movie movie;
  final DateTime watchedOn;
  final bool rewatch;
  final String? note;
  final int? rating;
  final DateTime? createdAt;

  /// The entry after an edit (date, rewatch flag and note are replaced).
  DiaryEntry applying(DiaryDraft draft) => DiaryEntry(
    id: id,
    movie: movie,
    watchedOn: draft.watchedOn,
    rewatch: draft.rewatch,
    note: draft.note,
    rating: rating,
    createdAt: createdAt,
  );

  factory DiaryEntry.fromJson(Map<String, dynamic> json) {
    final note = json['note'] as String?;
    return DiaryEntry(
      id: (json['id'] as num?)?.toInt() ?? 0,
      movie: Movie.fromJson(_map(json['movie'])),
      watchedOn: parseDiaryDate(json['watchedOn']) ?? DateTime(1900),
      rewatch: json['rewatch'] as bool? ?? false,
      note: note == null || note.trim().isEmpty ? null : note,
      rating: (json['rating'] as num?)?.toInt(),
      createdAt: json['createdAt'] is String
          ? DateTime.tryParse(json['createdAt'] as String)
          : null,
    );
  }
}

/// `GET /api/v1/diary/user/{userId}`: one page, newest first.
class DiaryEntriesPage {
  const DiaryEntriesPage({this.items = const [], this.totalCount = 0});

  final List<DiaryEntry> items;

  /// All entries matching the query (all pages).
  final int totalCount;

  factory DiaryEntriesPage.fromJson(Map<String, dynamic> json) =>
      DiaryEntriesPage(
        items:
            (json['items'] as List?)
                ?.map((e) => DiaryEntry.fromJson(_map(e)))
                .toList() ??
            const [],
        totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
      );
}

/// `GET /api/v1/diary/user/{userId}/summary`: counts for one year.
class DiarySummary {
  const DiarySummary({
    required this.year,
    this.totalCount = 0,
    this.rewatchCount = 0,
    this.monthCounts = const [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    this.availableYears = const [],
  });

  final int year;
  final int totalCount;
  final int rewatchCount;

  /// Always 12 counts, January first.
  final List<int> monthCounts;

  /// Years that have entries, newest first.
  final List<int> availableYears;

  int countFor(int month) => monthCounts[month - 1];

  factory DiarySummary.fromJson(Map<String, dynamic> json) {
    final counts = List<int>.filled(12, 0);
    for (final item in (json['months'] as List?) ?? const []) {
      final month = (_map(item)['month'] as num?)?.toInt();
      if (month == null || month < 1 || month > 12) continue;
      counts[month - 1] = (_map(item)['count'] as num?)?.toInt() ?? 0;
    }
    return DiarySummary(
      year: (json['year'] as num?)?.toInt() ?? DateTime.now().year,
      totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
      rewatchCount: (json['rewatchCount'] as num?)?.toInt() ?? 0,
      monthCounts: counts,
      availableYears:
          (json['availableYears'] as List?)
              ?.whereType<num>()
              .map((y) => y.toInt())
              .toList() ??
          const [],
    );
  }
}

/// What the add / edit sheet produces. [rating] is only used when adding.
class DiaryDraft {
  const DiaryDraft({
    required this.watchedOn,
    this.rewatch = false,
    this.note,
    this.rating,
  });

  final DateTime watchedOn;
  final bool rewatch;
  final String? note;
  final int? rating;
}
