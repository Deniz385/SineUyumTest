import '../../core/network/api_client.dart';
import '../models/notification.dart';

class NotificationRepository {
  NotificationRepository(this._client);

  final ApiClient _client;

  Future<List<AppNotification>> list() async {
    final data = await _client.get<List<dynamic>>('/api/v1/notification');
    return data
        .map(
          (item) =>
              AppNotification.fromJson((item as Map).cast<String, dynamic>()),
        )
        .toList();
  }

  Future<void> markAsRead(int notificationId) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/notification/$notificationId/mark-as-read',
    );
  }

  Future<void> markAllAsRead() async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/notification/mark-all-as-read',
    );
  }
}
