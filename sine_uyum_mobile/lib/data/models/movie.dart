/// TMDb'den gelen film özeti. API bu yanıtları olduğu gibi proxy'lediği için
/// alan adları TMDb'nin snake_case biçimindedir; kendi veritabanımızdan gelen
/// kayıtlarda ise camelCase olur. İki biçim de desteklenir.
class Movie {
  const Movie({
    required this.id,
    required this.title,
    this.posterPath,
    this.backdropPath,
    this.overview,
    this.releaseDate,
    this.voteAverage,
    this.genres = const [],
    this.runtime,
  });

  final int id;
  final String title;
  final String? posterPath;
  final String? backdropPath;
  final String? overview;
  final String? releaseDate;
  final double? voteAverage;
  final List<String> genres;
  final int? runtime;

  String? get year {
    if (releaseDate == null || releaseDate!.length < 4) return null;
    return releaseDate!.substring(0, 4);
  }

  factory Movie.fromJson(Map<String, dynamic> json) {
    return Movie(
      id: (json['id'] as num?)?.toInt() ?? 0,
      title: json['title'] as String? ?? json['name'] as String? ?? 'Başlıksız',
      posterPath: json['posterPath'] as String? ?? json['poster_path'] as String?,
      backdropPath:
          json['backdropPath'] as String? ?? json['backdrop_path'] as String?,
      overview: json['overview'] as String?,
      releaseDate:
          json['releaseDate'] as String? ?? json['release_date'] as String?,
      voteAverage: (json['voteAverage'] as num?)?.toDouble() ??
          (json['vote_average'] as num?)?.toDouble(),
      genres: (json['genres'] as List?)
              ?.map((g) =>
                  g is Map ? (g['name']?.toString() ?? '') : g.toString())
              .where((name) => name.isNotEmpty)
              .toList() ??
          const [],
      runtime: (json['runtime'] as num?)?.toInt(),
    );
  }

  Map<String, dynamic> toRatingPayload() => {
        'id': id,
        'title': title,
        'posterPath': posterPath,
      };
}

/// Kullanıcının bir filme verdiği puan (profil ekranında listelenir).
class RatedMovie {
  const RatedMovie({
    required this.movieId,
    required this.rating,
    required this.title,
    this.posterPath,
  });

  final int movieId;
  final int rating;
  final String title;
  final String? posterPath;

  factory RatedMovie.fromJson(Map<String, dynamic> json) {
    return RatedMovie(
      movieId: (json['movieId'] as num?)?.toInt() ?? 0,
      rating: (json['rating'] as num?)?.toInt() ?? 0,
      title: json['title'] as String? ?? 'Başlıksız',
      posterPath: json['posterPath'] as String?,
    );
  }
}
