import '../../core/network/api_client.dart';
import '../models/app_user.dart';
import '../models/profile.dart';

/// Profil, takip ve kullanıcı arama işlemleri.
class SocialRepository {
  SocialRepository(this._client);

  final ApiClient _client;

  Future<UserProfile> profile(String userId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/profile/$userId',
    );
    return UserProfile.fromJson(data);
  }

  Future<void> updateProfile({String? bio, String? profileImageUrl}) async {
    await _client.put<Map<String, dynamic>>(
      '/api/profile',
      body: {
        'bio': bio,
        // Boş string `[Url]` doğrulamasını bozduğu için null gönderilir.
        'profileImageUrl': (profileImageUrl?.trim().isEmpty ?? true)
            ? null
            : profileImageUrl,
      },
    );
  }

  Future<List<UserSearchResult>> searchUsers(String query) async {
    if (query.trim().isEmpty) return const [];
    final data = await _client.get<List<dynamic>>(
      '/api/account/search',
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
      '/api/compatibility/$userId',
    );
    return CompatibilityResult.fromJson(data);
  }

  Future<FollowStatus> followStatus(String userId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/follow/$userId/status',
    );
    return FollowStatus.fromJson(data);
  }

  Future<void> follow(String userId) async {
    await _client.post<Map<String, dynamic>>('/api/follow/$userId');
  }

  Future<void> unfollow(String userId) async {
    await _client.delete<Map<String, dynamic>>('/api/follow/$userId');
  }
}
