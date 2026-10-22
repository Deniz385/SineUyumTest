import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../core/config/app_config.dart';
import '../core/offline/network_status.dart';
import '../data/models/message.dart';
import '../data/models/notification.dart';
import 'chat_provider.dart';
import 'locale_provider.dart';
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

/// Fetches notifications over REST and listens for live ones over SignalR.
///
/// SignalR only runs while the app is in the foreground; when it is closed,
/// notifications arrive via FCM (see push_provider.dart). The same connection
/// also carries live chat messages ("ReceiveMessage" → [ChatEvents]).
///
/// Notification texts are localized by the server; the hub URL carries the
/// UI language (`lang`), and a language change reconnects and reloads.
class NotificationNotifier extends Notifier<NotificationState> {
  HubConnection? _connection;
  Timer? _retryTimer;
  int _failures = 0;

  // Retry delays when the connection fails (the last one repeats).
  static const _retryDelays = [5, 15, 30, 60];

  @override
  NotificationState build() {
    // Connect/disconnect as the session changes.
    final session = ref.watch(sessionProvider);

    ref.onDispose(_disconnect);

    // When the network comes back, connect right away and refresh whatever
    // was missed.
    ref.listen<NetworkStatus>(networkStatusProvider, (previous, next) {
      if (previous?.isOffline == true && !next.isOffline) {
        _retryTimer?.cancel();
        _connect();
        _resync();
      }
    });

    // New language: reconnect so the hub gets the new `lang`, and reload the
    // list in that language.
    ref.listen<String>(apiLanguageProvider, (previous, next) {
      if (previous == next || !ref.read(sessionProvider).isAuthenticated) {
        return;
      }
      _disconnect();
      _connect();
      load();
    });

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
    // Optimistic update: the user doesn't wait.
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

  /// A notification from SignalR. If that conversation is open on screen the
  /// message is already visible: the notification is added as read and also
  /// marked on the server.
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

  /// When a conversation opens, marks its unread notifications as read.
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
    final language = Uri.encodeQueryComponent(ref.read(apiLanguageProvider));

    final connection = HubConnectionBuilder()
        .withUrl(
          '${AppConfig.apiUrl}/notificationHub?lang=$language',
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
        // Unexpected shape: refetching the list is the safe option.
        load();
      }
    });

    // Live chat (M11).
    connection.on('ReceiveMessage', (arguments) {
      final payload = arguments?.firstOrNull;
      if (payload is! Map) return;
      ref
          .read(chatEventsProvider)
          .addMessage(ChatMessage.fromJson(payload.cast<String, dynamic>()));
      ref.invalidate(conversationsProvider);
    });

    // Read receipts (M10).
    connection.on('MessagesRead', (arguments) {
      final payload = arguments?.firstOrNull;
      if (payload is! Map) return;
      final read = MessagesRead.tryParse(payload.cast<String, dynamic>());
      if (read != null) ref.read(chatEventsProvider).addRead(read);
    });

    // Events may have been missed while disconnected: notifications and the
    // open conversation are refreshed from the server.
    connection.onreconnected(({connectionId}) => _resync());

    // Automatic reconnect gives up after ~40 s (4 attempts) and the
    // connection closes for good. Previously _connection stayed set, so it
    // never connected again and live notifications/chat were dead until the
    // app restarted. Now it is released and retried.
    connection.onclose(({error}) {
      if (!identical(_connection, connection)) return; // closed on purpose
      _connection = null;
      _scheduleRetry();
    });

    _connection = connection;

    try {
      await connection.start();
      if (_failures > 0) _resync(); // missed during the outage
      _failures = 0;
    } catch (error) {
      // The app must keep working without notifications.
      debugPrint('SignalR connection failed: $error');
      if (identical(_connection, connection)) _connection = null;
      _scheduleRetry();
    }
  }

  void _scheduleRetry() {
    if (!ref.mounted || !ref.read(sessionProvider).isAuthenticated) return;
    final seconds = _retryDelays[_failures.clamp(0, _retryDelays.length - 1)];
    _failures++;
    _retryTimer?.cancel();
    _retryTimer = Timer(Duration(seconds: seconds), () {
      if (ref.mounted) _connect();
    });
  }

  /// After an outage: refreshes notifications, the open conversation and the
  /// conversation list.
  void _resync() {
    if (!ref.mounted || !ref.read(sessionProvider).isAuthenticated) return;
    load();
    ref.read(chatEventsProvider).markReconnected();
    ref.invalidate(conversationsProvider);
  }

  void _disconnect() {
    _retryTimer?.cancel();
    _retryTimer = null;
    _failures = 0;
    final connection = _connection;
    _connection = null;
    connection?.stop();
  }
}
