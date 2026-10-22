import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/offline/network_status.dart';
import '../core/push/push_messaging.dart';
import '../core/router/app_router.dart';
import '../data/repositories/device_repository.dart';
import '../features/notifications/notifications_page.dart';
import 'notification_provider.dart';
import 'session_provider.dart';

enum PushStatus {
  /// Firebase isn't configured or the platform isn't supported.
  disabled,

  /// The user didn't grant notification permission.
  permissionDenied,

  /// The device is registered; notifications arrive even when the app is
  /// closed.
  registered,

  /// Signed out, or setup is still in progress.
  idle,
}

final pushMessagingProvider = Provider<PushMessaging>(
  (ref) => FirebasePushMessaging(),
);

final deviceRepositoryProvider = Provider<DeviceRepository>(
  (ref) => DeviceRepository(ref.watch(apiClientProvider)),
);

/// What happens when a notification is tapped. Default: mark it as read and
/// open the related screen. Replaced in tests.
final pushOpenedHandlerProvider = Provider<void Function(PushOpened)>((ref) {
  return (opened) {
    final id = opened.notificationId;
    if (id != null) {
      ref.read(notificationsProvider.notifier).markAsRead(id);
    }
    final route = notificationRouteFor(opened.relatedUrl);
    ref.read(routerProvider).push(route ?? '/notifications');
  };
});

final pushProvider = NotifierProvider<PushNotifier, PushStatus>(
  PushNotifier.new,
);

/// Watches the session to register/unregister the device for push.
///
/// * Sign-in (or the session being restored at start): ask for permission,
///   register the FCM token with the server, re-register when it refreshes.
/// * Sign-out or session expiry: unregister (the endpoint needs no auth), so
///   the device doesn't receive the previous user's notifications.
/// * Tapping a notification: open the related screen, cold start included.
///
/// Notifications arriving in the foreground already reach the list over
/// SignalR; FCM shows no system notification in the foreground, so there are
/// no duplicates.
class PushNotifier extends Notifier<PushStatus> {
  String? _registeredToken;
  StreamSubscription<String>? _tokenRefresh;
  StreamSubscription<PushOpened>? _opened;
  bool _initialHandled = false;

  // If sign-in/sign-out come in quick succession, process steps in order.
  Future<void> _pending = Future.value();

  PushMessaging get _messaging => ref.read(pushMessagingProvider);
  DeviceRepository get _devices => ref.read(deviceRepositoryProvider);

  @override
  PushStatus build() {
    ref.listen<SessionState>(sessionProvider, (previous, next) {
      if (previous?.isAuthenticated == next.isAuthenticated &&
          previous?.user?.id == next.user?.id) {
        return;
      }
      if (next.isAuthenticated) {
        _enqueue(_onSignedIn);
      } else if (next.status == SessionStatus.unauthenticated) {
        _enqueue(_onSignedOut);
      }
    }, fireImmediately: true);

    // If registration failed while offline, retry when the network comes
    // back (otherwise it would only be retried on the next token refresh).
    ref.listen<NetworkStatus>(networkStatusProvider, (previous, next) {
      final cameBack = previous?.isOffline == true && !next.isOffline;
      if (cameBack &&
          state == PushStatus.idle &&
          ref.read(sessionProvider).isAuthenticated) {
        _enqueue(_onSignedIn);
      }
    });

    ref.onDispose(() {
      _tokenRefresh?.cancel();
      _opened?.cancel();
    });

    return PushStatus.idle;
  }

  void _enqueue(Future<void> Function() step) {
    _pending = _pending.then((_) => step()).catchError((Object error) {
      // Push must never break the rest of the app.
      debugPrint('Push setup failed: $error');
    });
  }

  /// Lets tests wait for pending steps to finish.
  @visibleForTesting
  Future<void> get settled => _pending;

  Future<void> _onSignedIn() async {
    if (!await _messaging.initialize()) {
      state = PushStatus.disabled;
      return;
    }

    // The tap listener is set up regardless of permission (it may be granted
    // later).
    _opened ??= _messaging.onOpened.listen(_handleOpened);
    if (!_initialHandled) {
      _initialHandled = true;
      final initial = await _messaging.initialOpened();
      if (initial != null) _handleOpened(initial);
    }

    if (!await _messaging.requestPermission()) {
      state = PushStatus.permissionDenied;
      return;
    }

    // Listen BEFORE registering: even if the first registration fails with
    // a network error, the next token refresh can register the device.
    _tokenRefresh ??= _messaging.onTokenRefresh.listen(
      (fresh) => _enqueue(() async {
        // Refreshes arriving after sign-out aren't registered.
        if (ref.read(sessionProvider).isAuthenticated) await _register(fresh);
      }),
    );

    final token = await _messaging.getToken();
    if (token == null || token.isEmpty) return;
    await _register(token);
  }

  Future<void> _register(String token) async {
    await _devices.register(token: token, platform: _messaging.platform);
    _registeredToken = token;
    state = PushStatus.registered;
  }

  Future<void> _onSignedOut() async {
    final token = _registeredToken;
    _registeredToken = null;
    if (state != PushStatus.disabled) state = PushStatus.idle;
    if (token == null) return;

    try {
      await _devices.unregister(token);
    } catch (error) {
      // If offline, the server moves the token to another user on the next
      // sign-in, or deletes it when FCM reports "UNREGISTERED".
      debugPrint('Device unregistration failed: $error');
    }
  }

  void _handleOpened(PushOpened opened) {
    ref.read(pushOpenedHandlerProvider)(opened);
  }
}
