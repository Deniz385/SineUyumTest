import '../../core/network/api_client.dart';
import '../models/message.dart';

class MessageRepository {
  MessageRepository(this._client);

  final ApiClient _client;

  Future<List<Conversation>> conversations() async {
    final data = await _client.get<List<dynamic>>('/api/messages');
    return data
        .map((item) =>
            Conversation.fromJson((item as Map).cast<String, dynamic>()))
        .toList();
  }

  Future<List<ChatMessage>> thread(
    String otherUserId, {
    int page = 1,
    int pageSize = 100,
  }) async {
    final data = await _client.get<List<dynamic>>(
      '/api/messages/thread/$otherUserId',
      query: {'page': page, 'pageSize': pageSize},
    );
    return data
        .map((item) =>
            ChatMessage.fromJson((item as Map).cast<String, dynamic>()))
        .toList();
  }

  Future<void> send({
    required String recipientId,
    String? content,
    int? movieId,
    int? watchlistId,
  }) async {
    await _client.post<Map<String, dynamic>>(
      '/api/messages',
      body: {
        'recipientId': recipientId,
        'content': content,
        'movieId': movieId,
        'watchlistId': watchlistId,
      },
    );
  }

  Future<void> markThreadAsRead(String otherUserId) async {
    await _client.post<Map<String, dynamic>>(
      '/api/messages/thread/$otherUserId/mark-as-read',
    );
  }
}
