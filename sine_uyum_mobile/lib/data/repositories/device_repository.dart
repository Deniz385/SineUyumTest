import '../../core/network/api_client.dart';

/// Push bildirimi için cihaz (FCM token) kaydı.
class DeviceRepository {
  DeviceRepository(this._client);

  final ApiClient _client;

  /// Tekrar çağrılması güvenlidir; sunucu aynı token'ı günceller.
  Future<void> register({
    required String token,
    required String platform,
  }) async {
    await _client.post<dynamic>(
      '/api/v1/devices',
      body: {'token': token, 'platform': platform},
    );
  }

  /// Oturum gerektirmez: oturumu düşmüş cihaz da bildirim almayı
  /// durdurabilsin diye sunucuda kimlik doğrulamasız.
  Future<void> unregister(String token) async {
    await _client.post<dynamic>(
      '/api/v1/devices/unregister',
      body: {'token': token},
    );
  }
}
