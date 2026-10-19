import 'movie.dart';

/// `/api/profile/{userId}` yanıtı.
class UserProfile {
  const UserProfile({
    required this.id,
    required this.userName,
    this.bio,
    this.profileImageUrl,
    this.ratings = const [],
    required this.statistics,
  });

  final String id;
  final String userName;
  final String? bio;
  final String? profileImageUrl;
  final List<RatedMovie> ratings;
  final ProfileStatistics statistics;

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      id: json['id'] as String? ?? '',
      userName: json['userName'] as String? ?? '',
      bio: json['bio'] as String?,
      profileImageUrl: json['profileImageUrl'] as String?,
      ratings: (json['ratings'] as List?)
              ?.map((e) =>
                  RatedMovie.fromJson((e as Map).cast<String, dynamic>()))
              .toList() ??
          const [],
      statistics: ProfileStatistics.fromJson(
        (json['statistics'] as Map?)?.cast<String, dynamic>() ?? const {},
      ),
    );
  }
}

class ProfileStatistics {
  const ProfileStatistics({
    this.totalRatings = 0,
    this.averageRating = 0,
    this.totalMoviesInWatchlists = 0,
    this.topRatedMovies = const [],
  });

  final int totalRatings;
  final double averageRating;
  final int totalMoviesInWatchlists;
  final List<RatedMovie> topRatedMovies;

  factory ProfileStatistics.fromJson(Map<String, dynamic> json) {
    return ProfileStatistics(
      totalRatings: (json['totalRatings'] as num?)?.toInt() ?? 0,
      averageRating: (json['averageRating'] as num?)?.toDouble() ?? 0,
      totalMoviesInWatchlists:
          (json['totalMoviesInWatchlists'] as num?)?.toInt() ?? 0,
      topRatedMovies: (json['topRatedMovies'] as List?)
              ?.map((e) =>
                  RatedMovie.fromJson((e as Map).cast<String, dynamic>()))
              .toList() ??
          const [],
    );
  }
}

/// `/api/compatibility/{userId}` yanıtı.
class CompatibilityResult {
  const CompatibilityResult({
    this.compatibilityScore = 0,
    this.commonMovieCount = 0,
    this.commonMovies = const [],
  });

  final double compatibilityScore;
  final int commonMovieCount;
  final List<CommonMovie> commonMovies;

  factory CompatibilityResult.fromJson(Map<String, dynamic> json) {
    return CompatibilityResult(
      compatibilityScore:
          (json['compatibilityScore'] as num?)?.toDouble() ?? 0,
      commonMovieCount: (json['commonMovieCount'] as num?)?.toInt() ?? 0,
      commonMovies: (json['commonMovies'] as List?)
              ?.map((e) =>
                  CommonMovie.fromJson((e as Map).cast<String, dynamic>()))
              .toList() ??
          const [],
    );
  }
}

class CommonMovie {
  const CommonMovie({
    required this.movieId,
    required this.title,
    this.posterPath,
    required this.currentUserRating,
    required this.targetUserRating,
  });

  final int movieId;
  final String title;
  final String? posterPath;
  final int currentUserRating;
  final int targetUserRating;

  factory CommonMovie.fromJson(Map<String, dynamic> json) {
    return CommonMovie(
      movieId: (json['movieId'] as num?)?.toInt() ?? 0,
      title: json['title'] as String? ?? 'Başlıksız',
      posterPath: json['posterPath'] as String?,
      currentUserRating: (json['currentUserRating'] as num?)?.toInt() ?? 0,
      targetUserRating: (json['targetUserRating'] as num?)?.toInt() ?? 0,
    );
  }
}

/// `/api/follow/{userId}/status` yanıtı.
class FollowStatus {
  const FollowStatus({
    this.isFollowing = false,
    this.followerCount = 0,
    this.followingCount = 0,
  });

  final bool isFollowing;
  final int followerCount;
  final int followingCount;

  factory FollowStatus.fromJson(Map<String, dynamic> json) {
    return FollowStatus(
      isFollowing: json['isFollowing'] as bool? ?? false,
      followerCount: (json['followerCount'] as num?)?.toInt() ?? 0,
      followingCount: (json['followingCount'] as num?)?.toInt() ?? 0,
    );
  }
}
