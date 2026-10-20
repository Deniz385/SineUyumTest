import '../../core/network/api_client.dart';
import '../models/event.dart';

class EventRepository {
  EventRepository(this._client);

  final ApiClient _client;

  Future<EventState> myStatus() async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/event/my-status',
    );
    return EventState.fromJson(data);
  }

  Future<void> join(int eventId) async {
    await _client.post<Map<String, dynamic>>('/api/v1/event/$eventId/join');
  }

  /// Aynı filme tekrar oy vermek oyu geri çeker (API tarafında toggle).
  Future<void> vote({required int groupId, required int movieId}) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/event/vote',
      body: {'groupId': groupId, 'movieId': movieId},
    );
  }
}
