import '../../core/network/api_client.dart';
import '../models/app_user.dart';

class AuthRepository {
  AuthRepository(this._client);

  final ApiClient _client;

  Future<AuthResult> login({
    required String username,
    required String password,
  }) async {
    final data = await _client.post<Map<String, dynamic>>(
      '/api/v1/account/login',
      body: {'username': username, 'password': password},
    );
    return AuthResult.fromJson(data);
  }

  Future<void> register({
    required String username,
    required String email,
    required String password,
  }) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/account/register',
      body: {'username': username, 'email': email, 'password': password},
    );
  }

  Future<AppUser> me() async {
    final data = await _client.get<Map<String, dynamic>>('/api/v1/account/me');
    return AppUser.fromJson(data);
  }

  Future<void> logout(String refreshToken) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/account/logout',
      body: {'refreshToken': refreshToken},
    );
  }

  /// Tells the server the user's language ("tr", "en"...), used for texts it
  /// generates outside a request (notifications, push).
  Future<void> updateLanguage(String languageCode) async {
    await _client.put<dynamic>(
      '/api/v1/account/language',
      body: {'language': languageCode},
    );
  }

  /// Subscription status (the server is the single source of truth). The
  /// payment-free activation was removed; a subscription is enabled by a
  /// verified payment or by an admin.
  Future<SubscriptionInfo> subscription() async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/subscription',
    );
    return SubscriptionInfo.fromJson(data);
  }
}
