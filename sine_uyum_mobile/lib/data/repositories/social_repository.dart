import 'package:dio/dio.dart';

import '../../core/network/api_client.dart';
import '../models/app_user.dart';
import '../models/profile.dart';
import '../models/social.dart';

/// Profile, follow and user search operations.
class SocialRepository {
  SocialRepository(this._client);

  final ApiClient _client;

  Future<UserProfile> profile(String userId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/profile/$userId',
    );
    return UserProfile.fromJson(data);
  }

  /// For `profileImageUrl` the server only accepts the current value
  /// (unchanged) or empty (remove); the photo changes via [uploadProfilePhoto].
  Future<void> updateProfile({String? bio, String? profileImageUrl}) async {
    await _client.put<Map<String, dynamic>>(
      '/api/v1/profile',
      body: {
        'bio': bio,
        'profileImageUrl': (profileImageUrl?.trim().isEmpty ?? true)
            ? null
            : profileImageUrl,
      },
    );
  }

  /// Uploads the photo; the server converts it to a 512x512 JPEG and returns
  /// the new URL.
  Future<String?> uploadProfilePhoto(String filePath) async {
    final data = await _client.post<Map<String, dynamic>>(
      '/api/v1/profile/photo',
      body: FormData.fromMap({'file': await MultipartFile.fromFile(filePath)}),
    );
    return data['profileImageUrl'] as String?;
  }

  Future<void> deleteProfilePhoto() async {
    await _client.delete<Map<String, dynamic>>('/api/v1/profile/photo');
  }

  Future<List<UserSearchResult>> searchUsers(String query) async {
    if (query.trim().isEmpty) return const [];
    final data = await _client.get<List<dynamic>>(
      '/api/v1/account/search',
      query: {'query': query},
    );
    return data
        .map(
          (item) =>
              UserSearchResult.fromJson((item as Map).cast<String, dynamic>()),
        )
        .toList();
  }

  Future<CompatibilityResult> compatibility(String userId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/compatibility/$userId',
    );
    return CompatibilityResult.fromJson(data);
  }

  Future<FollowStatus> followStatus(String userId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/follow/$userId/status',
    );
    return FollowStatus.fromJson(data);
  }

  Future<void> follow(String userId) async {
    await _client.post<Map<String, dynamic>>('/api/v1/follow/$userId');
  }

  Future<void> unfollow(String userId) async {
    await _client.delete<Map<String, dynamic>>('/api/v1/follow/$userId');
  }

  Future<PrivacySettings> privacy() async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/account/privacy',
    );
    return PrivacySettings.fromJson(data);
  }

  Future<PrivacySettings> updatePrivacy(PrivacySettings settings) async {
    final data = await _client.put<Map<String, dynamic>>(
      '/api/v1/account/privacy',
      body: settings.toJson(),
    );
    return PrivacySettings.fromJson(data);
  }

  Future<List<BlockedUser>> blockedUsers() async {
    final data = await _client.get<List<dynamic>>('/api/v1/blocks');
    return data
        .map(
          (item) => BlockedUser.fromJson((item as Map).cast<String, dynamic>()),
        )
        .toList();
  }

  Future<void> block(String userId) async {
    await _client.post<Map<String, dynamic>>('/api/v1/blocks/$userId');
  }

  Future<void> unblock(String userId) async {
    await _client.delete<Map<String, dynamic>>('/api/v1/blocks/$userId');
  }

  // --- Social core: reviews, likes, comments, feed ---

  Future<MovieReviews> movieReviews(int movieId, {int page = 1}) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/reviews/movie/$movieId',
      query: {'page': page},
    );
    return MovieReviews.fromJson(data);
  }

  Future<Review> upsertReview(
    int movieId, {
    required String text,
    required bool containsSpoilers,
  }) async {
    final data = await _client.put<Map<String, dynamic>>(
      '/api/v1/reviews/movie/$movieId',
      body: {'text': text, 'containsSpoilers': containsSpoilers},
    );
    return Review.fromJson(data);
  }

  Future<void> deleteReview(int movieId) async {
    await _client.delete<Map<String, dynamic>>(
      '/api/v1/reviews/movie/$movieId',
    );
  }

  /// Sets the like state; returns the server's current count.
  Future<({bool liked, int likeCount})> setLiked(
    int reviewId,
    bool liked,
  ) async {
    final path = '/api/v1/reviews/$reviewId/like';
    final data = liked
        ? await _client.post<Map<String, dynamic>>(path)
        : await _client.delete<Map<String, dynamic>>(path);
    return (
      liked: data['liked'] as bool? ?? liked,
      likeCount: (data['likeCount'] as num?)?.toInt() ?? 0,
    );
  }

  Future<List<ReviewComment>> comments(int reviewId) async {
    final data = await _client.get<List<dynamic>>(
      '/api/v1/reviews/$reviewId/comments',
    );
    return data
        .map((e) => ReviewComment.fromJson((e as Map).cast<String, dynamic>()))
        .toList();
  }

  Future<ReviewComment> addComment(int reviewId, String text) async {
    final data = await _client.post<Map<String, dynamic>>(
      '/api/v1/reviews/$reviewId/comments',
      body: {'text': text},
    );
    return ReviewComment.fromJson(data);
  }

  Future<void> deleteComment(int commentId) async {
    await _client.delete<Map<String, dynamic>>(
      '/api/v1/reviews/comments/$commentId',
    );
  }

  Future<FeedPage> feed({String? cursor}) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/feed',
      query: cursor == null ? null : {'cursor': cursor},
    );
    return FeedPage.fromJson(data);
  }

  Future<FilmIdentity> identity(String userId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/profile/$userId/identity',
    );
    return FilmIdentity.fromJson(data);
  }

  /// This week's taste twin; null when there is no candidate (server: 204).
  Future<TasteTwin?> tasteTwin() async {
    final data = await _client.get<Object?>('/api/v1/taste-twin');
    return data is Map
        ? TasteTwin.fromJson(data.cast<String, dynamic>())
        : null;
  }
}
