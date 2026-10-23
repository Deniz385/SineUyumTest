/// Review, comment and feed models (the social core).
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

  /// Compatibility between viewer and author (0-100); null on your own review.
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

/// The "for you" score: the average of the ratings given to this movie by
/// people who match your taste, weighted by compatibility.
class ForYouScore {
  const ForYouScore({
    required this.score,
    required this.basedOn,
    this.topMatches = const [],
  });

  final double score;
  final int basedOn;
  final List<({Author author, int rating, double compatibility})> topMatches;

  static ForYouScore? tryParse(Map<String, dynamic>? json) {
    final score = (json?['score'] as num?)?.toDouble();
    if (json == null || score == null) return null;
    return ForYouScore(
      score: score,
      basedOn: (json['basedOn'] as num?)?.toInt() ?? 0,
      topMatches: [
        for (final m in (json['topMatches'] as List? ?? const []))
          if (m is Map)
            (
              author: Author.fromJson(_map(m['author'])),
              rating: (m['rating'] as num?)?.toInt() ?? 0,
              compatibility: (m['compatibility'] as num?)?.toDouble() ?? 0,
            ),
      ],
    );
  }
}

class MovieReviews {
  const MovieReviews({
    this.myRating,
    this.mine,
    this.reviews = const [],
    this.totalCount = 0,
    this.forYou,
  });

  final int? myRating;
  final Review? mine;
  final List<Review> reviews;
  final int totalCount;
  final ForYouScore? forYou;

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
    forYou: ForYouScore.tryParse(_map(json['forYou'])),
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

/// `/api/v1/taste-twin`: this week's taste twin.
class TasteTwin {
  const TasteTwin({
    required this.user,
    this.bio,
    required this.compatibility,
    required this.commonMovieCount,
    this.isFollowing = false,
  });

  final Author user;
  final String? bio;
  final double compatibility;
  final int commonMovieCount;
  final bool isFollowing;

  factory TasteTwin.fromJson(Map<String, dynamic> json) => TasteTwin(
    user: Author.fromJson(_map(json['user'])),
    bio: json['bio'] as String?,
    compatibility: (json['compatibility'] as num?)?.toDouble() ?? 0,
    commonMovieCount: (json['commonMovieCount'] as num?)?.toInt() ?? 0,
    isFollowing: json['isFollowing'] as bool? ?? false,
  );
}

/// A page of `/api/v1/reviews/user/{userId}` (newest first).
class ReviewPage {
  const ReviewPage({
    this.items = const [],
    this.page = 1,
    this.hasMore = false,
  });

  final List<Review> items;
  final int page;
  final bool hasMore;

  factory ReviewPage.fromJson(Map<String, dynamic> json) => ReviewPage(
    items:
        (json['items'] as List?)
            ?.whereType<Map>()
            .map((e) => Review.fromJson(e.cast<String, dynamic>()))
            .toList() ??
        const [],
    page: (json['page'] as num?)?.toInt() ?? 1,
    hasMore: json['hasMore'] as bool? ?? false,
  );
}

/// One person in `/api/v1/people/suggestions`.
class PersonSuggestion {
  const PersonSuggestion({
    required this.user,
    this.bio,
    this.compatibility,
    this.commonMovieCount = 0,
    this.followerCount = 0,
  });

  final Author user;
  final String? bio;

  /// Null for "popular" suggestions (too few common movies to compare).
  final double? compatibility;
  final int commonMovieCount;
  final int followerCount;

  factory PersonSuggestion.fromJson(Map<String, dynamic> json) =>
      PersonSuggestion(
        user: Author.fromJson(_map(json['user'])),
        bio: json['bio'] as String?,
        compatibility: (json['compatibility'] as num?)?.toDouble(),
        commonMovieCount: (json['commonMovieCount'] as num?)?.toInt() ?? 0,
        followerCount: (json['followerCount'] as num?)?.toInt() ?? 0,
      );
}

/// `/api/v1/people/suggestions`: by taste when possible, otherwise the most
/// followed people.
class PeopleSuggestions {
  const PeopleSuggestions({this.byTaste = true, this.items = const []});

  /// false: "popular" (the viewer has too few ratings, or nobody similar).
  final bool byTaste;
  final List<PersonSuggestion> items;

  factory PeopleSuggestions.fromJson(Map<String, dynamic> json) =>
      PeopleSuggestions(
        byTaste: json['basis'] != 'popular',
        items:
            (json['items'] as List?)
                ?.whereType<Map>()
                .map(
                  (e) => PersonSuggestion.fromJson(e.cast<String, dynamic>()),
                )
                .toList() ??
            const [],
      );
}
