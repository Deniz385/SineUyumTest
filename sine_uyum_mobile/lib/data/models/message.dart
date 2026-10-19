/// `/api/messages` listesindeki sohbet özeti.
class Conversation {
  const Conversation({
    required this.otherUserId,
    required this.otherUserUsername,
    this.otherUserProfileImageUrl,
    this.lastMessageContent = '',
    this.lastMessageSent,
    this.isRead = true,
  });

  final String otherUserId;
  final String otherUserUsername;
  final String? otherUserProfileImageUrl;
  final String lastMessageContent;
  final DateTime? lastMessageSent;
  final bool isRead;

  factory Conversation.fromJson(Map<String, dynamic> json) {
    return Conversation(
      otherUserId: json['otherUserId'] as String? ?? '',
      otherUserUsername: json['otherUserUsername'] as String? ?? '',
      otherUserProfileImageUrl: json['otherUserProfileImageUrl'] as String?,
      lastMessageContent: json['lastMessageContent'] as String? ?? '',
      lastMessageSent: json['lastMessageSent'] == null
          ? null
          : DateTime.tryParse(json['lastMessageSent'] as String),
      isRead: json['isRead'] as bool? ?? true,
    );
  }
}

/// `/api/messages/thread/{otherUserId}` içindeki tek mesaj.
/// Mesaj düz metin olabilir, bir film önerisi ya da bir izleme listesi
/// paylaşımı taşıyabilir.
class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.senderId,
    required this.senderUsername,
    required this.recipientId,
    this.content,
    this.messageSent,
    this.movie,
    this.watchlist,
  });

  final int id;
  final String senderId;
  final String senderUsername;
  final String recipientId;
  final String? content;
  final DateTime? messageSent;
  final SharedMovie? movie;
  final SharedWatchlist? watchlist;

  factory ChatMessage.fromJson(Map<String, dynamic> json) {
    final movieJson = (json['movie'] as Map?)?.cast<String, dynamic>();
    final watchlistJson = (json['watchlist'] as Map?)?.cast<String, dynamic>();

    return ChatMessage(
      id: (json['id'] as num?)?.toInt() ?? 0,
      senderId: json['senderId'] as String? ?? '',
      senderUsername: json['senderUsername'] as String? ?? '',
      recipientId: json['recipientId'] as String? ?? '',
      content: json['content'] as String?,
      messageSent: json['messageSent'] == null
          ? null
          : DateTime.tryParse(json['messageSent'] as String),
      movie: movieJson == null ? null : SharedMovie.fromJson(movieJson),
      watchlist: watchlistJson == null
          ? null
          : SharedWatchlist.fromJson(watchlistJson),
    );
  }
}

class SharedMovie {
  const SharedMovie({required this.id, required this.title, this.posterPath});

  final int id;
  final String title;
  final String? posterPath;

  factory SharedMovie.fromJson(Map<String, dynamic> json) {
    return SharedMovie(
      id: (json['id'] as num?)?.toInt() ?? 0,
      title: json['title'] as String? ?? 'Başlıksız',
      posterPath: json['posterPath'] as String?,
    );
  }
}

class SharedWatchlist {
  const SharedWatchlist({
    required this.id,
    required this.name,
    this.ownerUsername,
  });

  final int id;
  final String name;
  final String? ownerUsername;

  factory SharedWatchlist.fromJson(Map<String, dynamic> json) {
    return SharedWatchlist(
      id: (json['id'] as num?)?.toInt() ?? 0,
      name: json['name'] as String? ?? 'İsimsiz liste',
      ownerUsername: json['ownerUsername'] as String?,
    );
  }
}
