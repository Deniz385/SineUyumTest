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
      '/api/account/login',
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
      '/api/account/register',
      body: {'username': username, 'email': email, 'password': password},
    );
  }

  Future<AppUser> me() async {
    final data = await _client.get<Map<String, dynamic>>('/api/account/me');
    return AppUser.fromJson(data);
  }

  Future<void> logout(String refreshToken) async {
    await _client.post<Map<String, dynamic>>(
      '/api/account/logout',
      body: {'refreshToken': refreshToken},
    );
  }

  /// Abonelik aktifleştirildiğinde token yenilenir (abonelik claim'i token
  /// içinde taşındığı için).
  Future<AuthResult> activateSubscription() async {
    final data = await _client.post<Map<String, dynamic>>(
      '/api/subscription/activate',
    );
    return AuthResult.fromJson(data);
  }
}
