import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../core/config/app_config.dart';
import '../data/models/message.dart';
import '../data/models/notification.dart';
import 'chat_provider.dart';
import 'repository_providers.dart';
import 'session_provider.dart';

class NotificationState {
  const NotificationState({
    this.items = const [],
    this.isLoading = false,
    this.error,
  });

  final List<AppNotification> items;
  final bool isLoading;
  final String? error;

  int get unreadCount => items.where((item) => !item.isRead).length;

  NotificationState copyWith({
    List<AppNotification>? items,
    bool? isLoading,
    String? error,
  }) {
    return NotificationState(
      items: items ?? this.items,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

final notificationsProvider =
    NotifierProvider<NotificationNotifier, NotificationState>(
      NotificationNotifier.new,
    );

/// Bildirimleri REST ile çeker, SignalR ile canlı dinler.
///
/// SignalR yalnızca uygulama ön plandayken çalışır; uygulama kapalıyken
/// bildirimler FCM ile gelir (bkz. push_provider.dart). Aynı bağlantı canlı
/// sohbet mesajlarını da taşır ("ReceiveMessage" → [ChatEvents]).
class NotificationNotifier extends Notifier<NotificationState> {
  HubConnection? _connection;

  @override
  NotificationState build() {
    // Oturum durumu değiştikçe bağlantıyı kur/kapat.
    final session = ref.watch(sessionProvider);

    ref.onDispose(_disconnect);

    if (session.isAuthenticated) {
      Future.microtask(() async {
        await load();
        await _connect();
      });
    } else {
      Future.microtask(_disconnect);
      return const NotificationState();
    }

    return const NotificationState(isLoading: true);
  }

  Future<void> load() async {
    state = state.copyWith(isLoading: true);
    try {
      final items = await ref.read(notificationRepositoryProvider).list();
      state = NotificationState(items: items);
    } catch (error) {
      state = state.copyWith(isLoading: false, error: error.toString());
    }
  }

  Future<void> markAsRead(int notificationId) async {
    // İyimser güncelleme: kullanıcı beklemesin.
    state = state.copyWith(
      items: state.items
          .map(
            (item) =>
                item.id == notificationId ? item.copyWith(isRead: true) : item,
          )
          .toList(),
    );
    try {
      await ref.read(notificationRepositoryProvider).markAsRead(notificationId);
    } catch (_) {
      await load();
    }
  }

  /// SignalR'dan gelen bildirim. O sohbet ekranda açıksa mesaj zaten
  /// görülüyor: bildirim okundu olarak eklenir ve sunucuda da işaretlenir.
  @visibleForTesting
  void onNotification(AppNotification notification) {
    final active = ref.read(activeConversationProvider);
    if (active != null && notification.relatedUrl == '/messages/$active') {
      state = state.copyWith(
        items: [notification.copyWith(isRead: true), ...state.items],
      );
      ref
          .read(notificationRepositoryProvider)
          .markAsRead(notification.id)
          .catchError((_) {});
      return;
    }
    state = state.copyWith(items: [notification, ...state.items]);
  }

  /// Sohbet açılınca o sohbetin okunmamış bildirimlerini okundu işaretler.
  Future<void> markConversationRead(String otherUserId) async {
    final ids = state.items
        .where((n) => !n.isRead && n.relatedUrl == '/messages/$otherUserId')
        .map((n) => n.id)
        .toList();
    for (final id in ids) {
      await markAsRead(id);
    }
  }

  Future<void> markAllAsRead() async {
    state = state.copyWith(
      items: state.items.map((item) => item.copyWith(isRead: true)).toList(),
    );
    try {
      await ref.read(notificationRepositoryProvider).markAllAsRead();
    } catch (_) {
      await load();
    }
  }

  Future<void> _connect() async {
    if (_connection != null) return;

    final storage = ref.read(tokenStorageProvider);

    final connection = HubConnectionBuilder()
        .withUrl(
          '${AppConfig.apiUrl}/notificationHub',
          options: HttpConnectionOptions(
            accessTokenFactory: () async =>
                await storage.readAccessToken() ?? '',
          ),
        )
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveNotification', (arguments) {
      final payload = arguments?.firstOrNull;
      if (payload is Map) {
        onNotification(
          AppNotification.fromJson(payload.cast<String, dynamic>()),
        );
      } else {
        // Biçim beklenmedikse listeyi yeniden çekmek güvenli olanı.
        load();
      }
    });

    // Canlı sohbet (M11).
    connection.on('ReceiveMessage', (arguments) {
      final payload = arguments?.firstOrNull;
      if (payload is! Map) return;
      ref
          .read(chatEventsProvider)
          .addMessage(ChatMessage.fromJson(payload.cast<String, dynamic>()));
      ref.invalidate(conversationsProvider);
    });

    // Bağlantı koptuğu sürede gelenler kaçmış olabilir: bildirimler ve açık
    // sohbet sunucudan tazelenir.
    connection.onreconnected(({connectionId}) {
      load();
      ref.read(chatEventsProvider).markReconnected();
      ref.invalidate(conversationsProvider);
    });

    _connection = connection;

    try {
      await connection.start();
    } catch (error) {
      // Bildirimler olmadan da uygulama çalışmaya devam etmeli.
      debugPrint('SignalR bağlantısı kurulamadı: $error');
      _connection = null;
    }
  }

  void _disconnect() {
    final connection = _connection;
    _connection = null;
    connection?.stop();
  }
}
