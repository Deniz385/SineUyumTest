import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/push/push_messaging.dart';
import '../core/router/app_router.dart';
import '../data/repositories/device_repository.dart';
import '../features/notifications/notifications_page.dart';
import 'notification_provider.dart';
import 'session_provider.dart';

enum PushStatus {
  /// Firebase yapılandırılmamış ya da platform desteklemiyor.
  disabled,

  /// Kullanıcı bildirim iznini vermedi.
  permissionDenied,

  /// Cihaz sunucuya kaydedildi; uygulama kapalıyken de bildirim gelir.
  registered,

  /// Oturum yok ya da henüz kurulum sürüyor.
  idle,
}

final pushMessagingProvider = Provider<PushMessaging>(
  (ref) => FirebasePushMessaging(),
);

final deviceRepositoryProvider = Provider<DeviceRepository>(
  (ref) => DeviceRepository(ref.watch(apiClientProvider)),
);

/// Bildirime dokunulunca ne yapılacağı. Varsayılan: bildirimi okundu
/// işaretle ve ilgili ekrana git. Testlerde değiştirilir.
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

/// Oturumu izleyerek cihazı push için kaydeder/kaydını siler.
///
/// * Giriş (ya da açılışta oturumun geri yüklenmesi): izin iste, FCM token'ını
///   sunucuya kaydet, token yenilenince yeniden kaydet.
/// * Çıkış ya da oturum süresinin dolması: kaydı sil (uç kimlik doğrulama
///   istemez), ki cihaz önceki kullanıcının bildirimlerini almasın.
/// * Bildirime dokunma: soğuk açılış dahil ilgili ekrana git.
///
/// Uygulama ön plandayken gelen bildirimler zaten SignalR ile listeye düşer;
/// FCM ön planda sistem bildirimi göstermediği için çift bildirim olmaz.
class PushNotifier extends Notifier<PushStatus> {
  String? _registeredToken;
  StreamSubscription<String>? _tokenRefresh;
  StreamSubscription<PushOpened>? _opened;
  bool _initialHandled = false;

  // Giriş/çıkış hızlı ardışık gelirse adımlar sırayla işlensin.
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

    ref.onDispose(() {
      _tokenRefresh?.cancel();
      _opened?.cancel();
    });

    return PushStatus.idle;
  }

  void _enqueue(Future<void> Function() step) {
    _pending = _pending.then((_) => step()).catchError((Object error) {
      // Push hiçbir zaman uygulamanın geri kalanını bozmamalı.
      debugPrint('Push kurulumu başarısız: $error');
    });
  }

  /// Testlerin bekleyen adımların bitmesini beklemesi için.
  @visibleForTesting
  Future<void> get settled => _pending;

  Future<void> _onSignedIn() async {
    if (!await _messaging.initialize()) {
      state = PushStatus.disabled;
      return;
    }

    // Dokunma dinleyicisi izinden bağımsız kurulur (izin sonradan verilebilir).
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

    // Kayıttan ÖNCE dinlenir: ilk kayıt ağ hatasıyla düşse bile sonraki
    // token yenilemesi cihazı kaydedebilsin.
    _tokenRefresh ??= _messaging.onTokenRefresh.listen(
      (fresh) => _enqueue(() async {
        // Oturum kapandıktan sonra gelen yenilemeler kaydedilmez.
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
      // Çevrimdışıysa sunucu, token'ı bir sonraki girişte başka kullanıcıya
      // taşır ya da FCM "UNREGISTERED" dediğinde siler.
      debugPrint('Cihaz kaydı silinemedi: $error');
    }
  }

  void _handleOpened(PushOpened opened) {
    ref.read(pushOpenedHandlerProvider)(opened);
  }
}
