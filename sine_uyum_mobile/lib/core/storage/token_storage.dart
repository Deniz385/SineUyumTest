import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Keeps the tokens in the OS secure store
/// (iOS Keychain / Android EncryptedSharedPreferences).
/// Not `SharedPreferences`: on a rooted device that is readable plain text.
class TokenStorage {
  TokenStorage({FlutterSecureStorage? storage})
    // Encrypted storage is the default on Android in this version; on iOS
    // first_unlock makes the key readable after the device's first unlock
    // (needed for token refreshes in the background).
    : _storage =
          storage ??
          const FlutterSecureStorage(
            iOptions: IOSOptions(
              accessibility: KeychainAccessibility.first_unlock,
            ),
          );

  final FlutterSecureStorage _storage;

  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';

  Future<String?> readAccessToken() => _storage.read(key: _accessTokenKey);

  Future<String?> readRefreshToken() => _storage.read(key: _refreshTokenKey);

  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await _storage.write(key: _accessTokenKey, value: accessToken);
    await _storage.write(key: _refreshTokenKey, value: refreshToken);
  }

  Future<void> clear() async {
    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
  }
}
