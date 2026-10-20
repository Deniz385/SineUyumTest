import '../../core/network/api_client.dart';
import '../models/message.dart';

class MessageRepository {
  MessageRepository(this._client);

  final ApiClient _client;

  Future<List<Conversation>> conversations() async {
    final data = await _client.get<List<dynamic>>('/api/v1/messages');
    return data
        .map(
          (item) =>
              Conversation.fromJson((item as Map).cast<String, dynamic>()),
        )
        .toList();
  }

  Future<List<ChatMessage>> thread(
    String otherUserId, {
    int page = 1,
    int pageSize = 100,
  }) async {
    final data = await _client.get<List<dynamic>>(
      '/api/v1/messages/thread/$otherUserId',
      query: {'page': page, 'pageSize': pageSize},
    );
    return data
        .map(
          (item) => ChatMessage.fromJson((item as Map).cast<String, dynamic>()),
        )
        .toList();
  }

  /// Gönderilen mesajın kendisini döner (thread'deki ile aynı şekil).
  Future<ChatMessage> send({
    required String recipientId,
    String? content,
    int? movieId,
    int? watchlistId,
  }) async {
    final data = await _client.post<Map<String, dynamic>>(
      '/api/v1/messages',
      body: {
        'recipientId': recipientId,
        'content': content,
        'movieId': movieId,
        'watchlistId': watchlistId,
      },
    );
    return ChatMessage.fromJson(data);
  }

  Future<void> markThreadAsRead(String otherUserId) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/messages/thread/$otherUserId/mark-as-read',
    );
  }
}
