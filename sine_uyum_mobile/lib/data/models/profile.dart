import '../../l10n/l10n.dart';
import 'movie.dart';

/// Response of `/api/v1/profile/{userId}`.
class UserProfile {
  const UserProfile({
    required this.id,
    required this.userName,
    this.bio,
    this.profileImageUrl,
    this.ratings = const [],
    required this.statistics,
    this.isRestricted = false,
    this.isBlockedByMe = false,
  });

  final String id;
  final String userName;
  final String? bio;
  final String? profileImageUrl;

  /// The viewer can't see the details (privacy setting, or they blocked the
  /// user); ratings and statistics come back empty.
  final bool isRestricted;

  /// The viewer has blocked this user.
  final bool isBlockedByMe;
  final List<RatedMovie> ratings;
  final ProfileStatistics statistics;

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      id: json['id'] as String? ?? '',
      userName: json['userName'] as String? ?? '',
      bio: json['bio'] as String?,
      profileImageUrl: json['profileImageUrl'] as String?,
      isRestricted: json['isRestricted'] as bool? ?? false,
      isBlockedByMe: json['isBlockedByMe'] as bool? ?? false,
      ratings:
          (json['ratings'] as List?)
              ?.map(
                (e) => RatedMovie.fromJson((e as Map).cast<String, dynamic>()),
              )
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
      topRatedMovies:
          (json['topRatedMovies'] as List?)
              ?.map(
                (e) => RatedMovie.fromJson((e as Map).cast<String, dynamic>()),
              )
              .toList() ??
          const [],
    );
  }
}

/// Response of `/api/v1/compatibility/{userId}`.
class CompatibilityResult {
  const CompatibilityResult({
    this.compatibilityScore = 0,
    this.commonMovieCount = 0,
    this.commonMovies = const [],
    this.insights = const CompatibilityInsights(),
  });

  final double compatibilityScore;
  final int commonMovieCount;
  final List<CommonMovie> commonMovies;
  final CompatibilityInsights insights;

  factory CompatibilityResult.fromJson(Map<String, dynamic> json) {
    return CompatibilityResult(
      compatibilityScore: (json['compatibilityScore'] as num?)?.toDouble() ?? 0,
      commonMovieCount: (json['commonMovieCount'] as num?)?.toInt() ?? 0,
      commonMovies:
          (json['commonMovies'] as List?)
              ?.map(
                (e) => CommonMovie.fromJson((e as Map).cast<String, dynamic>()),
              )
              .toList() ??
          const [],
      insights: CompatibilityInsights.fromJson(
        (json['insights'] as Map?)?.cast<String, dynamic>() ?? const {},
      ),
    );
  }
}

/// Explains the compatibility score: shared/divergent genres, a shared
/// favorite and a conversation-starting "debate movie". Texts are localized
/// by the server.
class CompatibilityInsights {
  const CompatibilityInsights({
    this.summary,
    this.sharedGenres = const [],
    this.divergentGenres = const [],
    this.sharedFavorite,
    this.debateMovie,
  });

  final String? summary;
  final List<String> sharedGenres;
  final List<String> divergentGenres;
  final CommonMovie? sharedFavorite;
  final CommonMovie? debateMovie;

  bool get isEmpty =>
      summary == null && sharedFavorite == null && debateMovie == null;

  static CommonMovie? _movie(Object? json) =>
      json is Map ? CommonMovie.fromJson(json.cast<String, dynamic>()) : null;

  static List<String> _names(Object? json) =>
      (json as List?)?.whereType<String>().toList() ?? const [];

  factory CompatibilityInsights.fromJson(Map<String, dynamic> json) =>
      CompatibilityInsights(
        summary: json['summary'] as String?,
        sharedGenres: _names(json['sharedGenres']),
        divergentGenres: _names(json['divergentGenres']),
        sharedFavorite: _movie(json['sharedFavorite']),
        debateMovie: _movie(json['debateMovie']),
      );
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
      title: json['title'] as String? ?? L10n.current.untitledMovie,
      posterPath: json['posterPath'] as String?,
      currentUserRating: (json['currentUserRating'] as num?)?.toInt() ?? 0,
      targetUserRating: (json['targetUserRating'] as num?)?.toInt() ?? 0,
    );
  }
}

/// Response of `/api/v1/follow/{userId}/status`.
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

/// Who can see the profile details (ratings, lists, compatibility).
enum ProfileVisibility {
  public('Public'),
  following('Following'),
  private('Private');

  const ProfileVisibility(this.apiValue);
  final String apiValue;

  static ProfileVisibility fromApi(Object? value) => values.firstWhere(
    (v) => v.apiValue == value,
    orElse: () => ProfileVisibility.public,
  );
}

/// Who can start a new conversation.
enum MessagePermission {
  everyone('Everyone'),
  following('Following'),
  nobody('Nobody');

  const MessagePermission(this.apiValue);
  final String apiValue;

  static MessagePermission fromApi(Object? value) => values.firstWhere(
    (v) => v.apiValue == value,
    orElse: () => MessagePermission.everyone,
  );
}

/// `/api/v1/account/privacy` (GET/PUT).
class PrivacySettings {
  const PrivacySettings({
    this.profileVisibility = ProfileVisibility.public,
    this.messagePermission = MessagePermission.everyone,
    this.hiddenFromSearch = false,
    this.showReadReceipts = true,
  });

  final ProfileVisibility profileVisibility;
  final MessagePermission messagePermission;
  final bool hiddenFromSearch;

  /// Read receipts are mutual: turning them off also hides other people's.
  final bool showReadReceipts;

  PrivacySettings copyWith({
    ProfileVisibility? profileVisibility,
    MessagePermission? messagePermission,
    bool? hiddenFromSearch,
    bool? showReadReceipts,
  }) => PrivacySettings(
    profileVisibility: profileVisibility ?? this.profileVisibility,
    messagePermission: messagePermission ?? this.messagePermission,
    hiddenFromSearch: hiddenFromSearch ?? this.hiddenFromSearch,
    showReadReceipts: showReadReceipts ?? this.showReadReceipts,
  );

  factory PrivacySettings.fromJson(Map<String, dynamic> json) =>
      PrivacySettings(
        profileVisibility: ProfileVisibility.fromApi(json['profileVisibility']),
        messagePermission: MessagePermission.fromApi(json['messagePermission']),
        hiddenFromSearch: json['hiddenFromSearch'] as bool? ?? false,
        showReadReceipts: json['showReadReceipts'] as bool? ?? true,
      );

  Map<String, dynamic> toJson() => {
    'profileVisibility': profileVisibility.apiValue,
    'messagePermission': messagePermission.apiValue,
    'hiddenFromSearch': hiddenFromSearch,
    'showReadReceipts': showReadReceipts,
  };
}

/// An item of `/api/v1/blocks`.
class BlockedUser {
  const BlockedUser({
    required this.id,
    required this.userName,
    this.profileImageUrl,
  });

  final String id;
  final String userName;
  final String? profileImageUrl;

  factory BlockedUser.fromJson(Map<String, dynamic> json) => BlockedUser(
    id: json['id'] as String? ?? '',
    userName: json['userName'] as String? ?? '',
    profileImageUrl: json['profileImageUrl'] as String?,
  );
}

/// `/api/v1/profile/{userId}/identity`: the shareable film identity. The
/// title, description and genre names are localized by the server.
class FilmIdentity {
  const FilmIdentity({
    required this.userName,
    this.totalRatings = 0,
    this.averageRating = 0,
    required this.title,
    required this.titleDescription,
    this.topGenres = const [],
    this.contrarianTake,
    this.favorite,
  });

  final String userName;
  final int totalRatings;
  final double averageRating;
  final String title;
  final String titleDescription;

  /// [share] is a percentage among the movies with a known genre.
  final List<({String name, int count, int share, double average})> topGenres;
  final IdentityMovie? contrarianTake;
  final IdentityMovie? favorite;

  factory FilmIdentity.fromJson(Map<String, dynamic> json) => FilmIdentity(
    userName: json['userName'] as String? ?? '',
    totalRatings: (json['totalRatings'] as num?)?.toInt() ?? 0,
    averageRating: (json['averageRating'] as num?)?.toDouble() ?? 0,
    title: json['title'] as String? ?? '',
    titleDescription: json['titleDescription'] as String? ?? '',
    topGenres: [
      for (final g in (json['topGenres'] as List? ?? const []))
        if (g is Map)
          (
            name: g['name'] as String? ?? '',
            count: (g['count'] as num?)?.toInt() ?? 0,
            share: (g['share'] as num?)?.toInt() ?? 0,
            average: (g['average'] as num?)?.toDouble() ?? 0,
          ),
    ],
    contrarianTake: IdentityMovie.tryParse(json['contrarianTake']),
    favorite: IdentityMovie.tryParse(json['favorite']),
  );
}

class IdentityMovie {
  const IdentityMovie({
    required this.movieId,
    required this.title,
    required this.rating,
    this.posterPath,
    this.tmdbAverage,
  });

  final int movieId;
  final String title;
  final String? posterPath;
  final int rating;
  final double? tmdbAverage;

  static IdentityMovie? tryParse(Object? json) => json is Map
      ? IdentityMovie(
          movieId: (json['movieId'] as num?)?.toInt() ?? 0,
          title: json['title'] as String? ?? '',
          posterPath: json['posterPath'] as String?,
          rating: (json['rating'] as num?)?.toInt() ?? 0,
          tmdbAverage: (json['tmdbAverage'] as num?)?.toDouble(),
        )
      : null;
}
