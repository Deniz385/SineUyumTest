import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Token'ları işletim sisteminin güvenli deposunda tutar
/// (iOS Keychain / Android EncryptedSharedPreferences).
/// `SharedPreferences` kullanılmaz: root'lu cihazda düz metin okunabilir.
class TokenStorage {
  TokenStorage({FlutterSecureStorage? storage})
    // Android'de şifreli depolama bu sürümde varsayılan; iOS'ta anahtarın
    // cihaz ilk açıldıktan sonra okunabilmesi için first_unlock seçiliyor
    // (arka plandaki token yenilemesi için gerekli).
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
