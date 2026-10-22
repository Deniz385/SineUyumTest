import '../../core/network/api_client.dart';

/// Device (FCM token) registration for push notifications.
class DeviceRepository {
  DeviceRepository(this._client);

  final ApiClient _client;

  /// Safe to call repeatedly; the server updates the same token.
  Future<void> register({
    required String token,
    required String platform,
  }) async {
    await _client.post<dynamic>(
      '/api/v1/devices',
      body: {'token': token, 'platform': platform},
    );
  }

  /// Needs no session: unauthenticated on the server so that a device whose
  /// session has expired can still stop receiving notifications.
  Future<void> unregister(String token) async {
    await _client.post<dynamic>(
      '/api/v1/devices/unregister',
      body: {'token': token},
    );
  }
}
