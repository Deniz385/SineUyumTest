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

  /// Voting for the same movie again withdraws the vote (a toggle on the API).
  Future<void> vote({required int groupId, required int movieId}) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/event/vote',
      body: {'groupId': groupId, 'movieId': movieId},
    );
  }
}
