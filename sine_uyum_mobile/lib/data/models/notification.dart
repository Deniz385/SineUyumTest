/// A `/api/v1/notification` record. The SignalR `ReceiveNotification` event
/// sends the same shape. [message] is localized by the server.
class AppNotification {
  const AppNotification({
    required this.id,
    required this.message,
    this.relatedUrl,
    this.isRead = false,
    this.createdAt,
  });

  final int id;
  final String message;
  final String? relatedUrl;
  final bool isRead;
  final DateTime? createdAt;

  AppNotification copyWith({bool? isRead}) => AppNotification(
    id: id,
    message: message,
    relatedUrl: relatedUrl,
    isRead: isRead ?? this.isRead,
    createdAt: createdAt,
  );

  factory AppNotification.fromJson(Map<String, dynamic> json) {
    return AppNotification(
      id: (json['id'] as num?)?.toInt() ?? 0,
      message: json['message'] as String? ?? '',
      relatedUrl: json['relatedUrl'] as String?,
      isRead: json['isRead'] as bool? ?? false,
      createdAt: json['createdAt'] == null
          ? null
          : DateTime.tryParse(json['createdAt'] as String),
    );
  }
}
