/// İnceleme, yorum ve akış modelleri (sosyal çekirdek).
class Author {
  const Author({required this.id, required this.userName, this.imageUrl});

  final String id;
  final String userName;
  final String? imageUrl;

  factory Author.fromJson(Map<String, dynamic>? json) => Author(
    id: json?['id'] as String? ?? '',
    userName: json?['userName'] as String? ?? '',
    imageUrl: json?['profileImageUrl'] as String?,
  );
}

class ReviewMovie {
  const ReviewMovie({required this.id, required this.title, this.posterPath});

  final int id;
  final String title;
  final String? posterPath;

  factory ReviewMovie.fromJson(Map<String, dynamic>? json) => ReviewMovie(
    id: (json?['id'] as num?)?.toInt() ?? 0,
    title: json?['title'] as String? ?? '',
    posterPath: json?['posterPath'] as String?,
  );
}

DateTime? _date(Object? value) =>
    value is String ? DateTime.tryParse(value) : null;

Map<String, dynamic>? _map(Object? value) =>
    value is Map ? value.cast<String, dynamic>() : null;

class Review {
  const Review({
    required this.id,
    required this.movie,
    required this.author,
    this.rating,
    required this.text,
    this.containsSpoilers = false,
    this.createdAt,
    this.likeCount = 0,
    this.commentCount = 0,
    this.likedByMe = false,
    this.isMine = false,
    this.authorCompatibility,
  });

  final int id;
  final ReviewMovie movie;
  final Author author;
  final int? rating;
  final String text;
  final bool containsSpoilers;
  final DateTime? createdAt;
  final int likeCount;
  final int commentCount;
  final bool likedByMe;
  final bool isMine;

  /// Görüntüleyenle yazarın uyumu (0-100); kendi incelemesinde null.
  final double? authorCompatibility;

  Review copyWith({bool? likedByMe, int? likeCount, int? commentCount}) =>
      Review(
        id: id,
        movie: movie,
        author: author,
        rating: rating,
        text: text,
        containsSpoilers: containsSpoilers,
        createdAt: createdAt,
        likeCount: likeCount ?? this.likeCount,
        commentCount: commentCount ?? this.commentCount,
        likedByMe: likedByMe ?? this.likedByMe,
        isMine: isMine,
        authorCompatibility: authorCompatibility,
      );

  factory Review.fromJson(Map<String, dynamic> json) => Review(
    id: (json['id'] as num?)?.toInt() ?? 0,
    movie: ReviewMovie.fromJson(_map(json['movie'])),
    author: Author.fromJson(_map(json['author'])),
    rating: (json['rating'] as num?)?.toInt(),
    text: json['text'] as String? ?? '',
    containsSpoilers: json['containsSpoilers'] as bool? ?? false,
    createdAt: _date(json['createdAt']),
    likeCount: (json['likeCount'] as num?)?.toInt() ?? 0,
    commentCount: (json['commentCount'] as num?)?.toInt() ?? 0,
    likedByMe: json['likedByMe'] as bool? ?? false,
    isMine: json['isMine'] as bool? ?? false,
    authorCompatibility: (json['authorCompatibility'] as num?)?.toDouble(),
  );
}

class MovieReviews {
  const MovieReviews({
    this.myRating,
    this.mine,
    this.reviews = const [],
    this.totalCount = 0,
  });

  final int? myRating;
  final Review? mine;
  final List<Review> reviews;
  final int totalCount;

  factory MovieReviews.fromJson(Map<String, dynamic> json) => MovieReviews(
    myRating: (json['myRating'] as num?)?.toInt(),
    mine: _map(json['mine']) == null
        ? null
        : Review.fromJson(_map(json['mine'])!),
    reviews:
        (json['reviews'] as List?)
            ?.map((e) => Review.fromJson((e as Map).cast<String, dynamic>()))
            .toList() ??
        const [],
    totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
  );
}

class ReviewComment {
  const ReviewComment({
    required this.id,
    required this.author,
    required this.text,
    this.canDelete = false,
  });

  final int id;
  final Author author;
  final String text;
  final bool canDelete;

  factory ReviewComment.fromJson(Map<String, dynamic> json) => ReviewComment(
    id: (json['id'] as num?)?.toInt() ?? 0,
    author: Author.fromJson(_map(json['author'])),
    text: json['text'] as String? ?? '',
    canDelete: json['canDelete'] as bool? ?? false,
  );
}

class FeedItem {
  const FeedItem({
    required this.activityAt,
    required this.author,
    required this.movie,
    required this.rating,
    this.review,
    this.authorCompatibility,
  });

  final DateTime? activityAt;
  final Author author;
  final ReviewMovie movie;
  final int rating;
  final Review? review;
  final double? authorCompatibility;

  factory FeedItem.fromJson(Map<String, dynamic> json) => FeedItem(
    activityAt: _date(json['activityAt']),
    author: Author.fromJson(_map(json['author'])),
    movie: ReviewMovie.fromJson(_map(json['movie'])),
    rating: (json['rating'] as num?)?.toInt() ?? 0,
    review: _map(json['review']) == null
        ? null
        : Review.fromJson(_map(json['review'])!),
    authorCompatibility: (json['authorCompatibility'] as num?)?.toDouble(),
  );
}

class FeedPage {
  const FeedPage({this.items = const [], this.nextCursor});

  final List<FeedItem> items;
  final String? nextCursor;

  factory FeedPage.fromJson(Map<String, dynamic> json) => FeedPage(
    items:
        (json['items'] as List?)
            ?.map((e) => FeedItem.fromJson((e as Map).cast<String, dynamic>()))
            .toList() ??
        const [],
    nextCursor: json['nextCursor'] as String?,
  );
}
