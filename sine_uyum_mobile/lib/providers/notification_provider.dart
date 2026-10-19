import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../core/config/app_config.dart';
import '../data/models/notification.dart';
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
        NotificationNotifier.new);

/// Bildirimleri REST ile çeker, SignalR ile canlı dinler.
///
/// NOT: SignalR yalnızca uygulama ön plandayken çalışır. Uygulama kapalıyken
/// bildirim gönderebilmek için FCM/APNs entegrasyonu gerekir
/// (bkz. docs/ANALIZ-VE-YOL-HARITASI.md → B7).
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
          .map((item) =>
              item.id == notificationId ? item.copyWith(isRead: true) : item)
          .toList(),
    );
    try {
      await ref.read(notificationRepositoryProvider).markAsRead(notificationId);
    } catch (_) {
      await load();
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
        final notification =
            AppNotification.fromJson(payload.cast<String, dynamic>());
        state = state.copyWith(items: [notification, ...state.items]);
      } else {
        // Biçim beklenmedikse listeyi yeniden çekmek güvenli olanı.
        load();
      }
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
