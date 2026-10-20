import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/push/push_messaging.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/repositories/device_repository.dart';
import 'package:sine_uyum_mobile/providers/push_provider.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

class FakeMessaging implements PushMessaging {
  bool configured = true;
  bool permission = true;
  String? token = 'token-1';
  PushOpened? initial;
  final refresh = StreamController<String>.broadcast();
  final opened = StreamController<PushOpened>.broadcast();
  int initialCalls = 0;

  @override
  Future<bool> initialize() async => configured;

  @override
  Future<bool> requestPermission() async => permission;

  @override
  Future<String?> getToken() async => token;

  @override
  Stream<String> get onTokenRefresh => refresh.stream;

  @override
  Future<PushOpened?> initialOpened() async {
    initialCalls++;
    return initial;
  }

  @override
  Stream<PushOpened> get onOpened => opened.stream;

  @override
  String get platform => 'android';
}

class FakeDevices implements DeviceRepository {
  final registered = <(String, String)>[];
  final unregistered = <String>[];
  bool failRegister = false;

  @override
  Future<void> register({
    required String token,
    required String platform,
  }) async {
    if (failRegister) throw Exception('ağ yok');
    registered.add((token, platform));
  }

  @override
  Future<void> unregister(String token) async => unregistered.add(token);
}

/// Depoya/ağa dokunmadan oturum durumunu testten sürmek için.
class FakeSession extends SessionNotifier {
  @override
  SessionState build() => const SessionState.unknown();

  void signIn(String userId) => state = SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(id: userId, userName: userId),
  );

  void signOut() => state = const SessionState.signedOut();
}

void main() {
  late FakeMessaging messaging;
  late FakeDevices devices;
  late List<PushOpened> handled;
  late ProviderContainer container;

  setUp(() {
    messaging = FakeMessaging();
    devices = FakeDevices();
    handled = [];
    container = ProviderContainer(
      overrides: [
        sessionProvider.overrideWith(FakeSession.new),
        pushMessagingProvider.overrideWithValue(messaging),
        deviceRepositoryProvider.overrideWithValue(devices),
        pushOpenedHandlerProvider.overrideWithValue(handled.add),
      ],
    );
    // Sağlayıcı uygulamada kök widget'ta izlenir; testte de canlı tutulur.
    container.listen(pushProvider, (_, _) {});
  });

  tearDown(() => container.dispose());

  FakeSession session() =>
      container.read(sessionProvider.notifier) as FakeSession;

  Future<void> settle() async {
    // Olay döngüsünün akış olaylarını teslim etmesine izin ver, sonra
    // sıradaki adımların bitmesini bekle.
    await Future<void>.delayed(Duration.zero);
    await container.read(pushProvider.notifier).settled;
  }

  PushStatus status() => container.read(pushProvider);

  test('girişte izin alınır ve cihaz kaydedilir', () async {
    session().signIn('u1');
    await settle();

    expect(devices.registered, [('token-1', 'android')]);
    expect(status(), PushStatus.registered);
  });

  test('Firebase yapılandırılmamışsa hiçbir şey kaydedilmez', () async {
    messaging.configured = false;
    session().signIn('u1');
    await settle();

    expect(devices.registered, isEmpty);
    expect(status(), PushStatus.disabled);
  });

  test('izin verilmezse kayıt yapılmaz', () async {
    messaging.permission = false;
    session().signIn('u1');
    await settle();

    expect(devices.registered, isEmpty);
    expect(status(), PushStatus.permissionDenied);
  });

  test('token yenilenince yeni token kaydedilir', () async {
    session().signIn('u1');
    await settle();

    messaging.refresh.add('token-2');
    await settle();

    expect(devices.registered.map((r) => r.$1), ['token-1', 'token-2']);
  });

  test('çıkışta kayıt silinir, sonraki yenilemeler kaydedilmez', () async {
    session().signIn('u1');
    await settle();
    messaging.refresh.add('token-2');
    await settle();

    session().signOut();
    await settle();
    expect(devices.unregistered, ['token-2']);
    expect(status(), PushStatus.idle);

    messaging.refresh.add('token-3');
    await settle();
    expect(devices.registered.map((r) => r.$1), isNot(contains('token-3')));
  });

  test('hiç kayıt yokken çıkış sunucuyu çağırmaz', () async {
    session().signOut();
    await settle();
    expect(devices.unregistered, isEmpty);
  });

  test('başka hesapla girişte cihaz yeniden kaydedilir', () async {
    session().signIn('u1');
    await settle();
    session().signOut();
    await settle();
    session().signIn('u2');
    await settle();

    expect(devices.registered.map((r) => r.$1), ['token-1', 'token-1']);
    expect(devices.unregistered, ['token-1']);
  });

  test('soğuk açılıştaki bildirime bir kez gidilir', () async {
    messaging.initial = const PushOpened(
      relatedUrl: '/messages/u9',
      notificationId: 5,
    );
    session().signIn('u1');
    await settle();
    session().signOut();
    await settle();
    session().signIn('u1');
    await settle();

    expect(handled, hasLength(1));
    expect(handled.single.relatedUrl, '/messages/u9');
    expect(handled.single.notificationId, 5);
    expect(messaging.initialCalls, 1);
  });

  test('arka plandayken dokunulan bildirim işlenir', () async {
    session().signIn('u1');
    await settle();

    messaging.opened.add(const PushOpened(relatedUrl: '/profile/u3'));
    await settle();

    expect(handled.map((o) => o.relatedUrl), ['/profile/u3']);
  });

  test('kayıt ağ hatasıyla düşerse uygulama etkilenmez', () async {
    devices.failRegister = true;
    session().signIn('u1');
    await settle();

    expect(status(), isNot(PushStatus.registered));
    // Sonraki adımlar hâlâ işlenir.
    devices.failRegister = false;
    messaging.refresh.add('token-2');
    await settle();
    expect(devices.registered.map((r) => r.$1), ['token-2']);
  });

  group('PushOpened.fromData', () {
    test('sunucunun string alanlarını çözer', () {
      final opened = PushOpened.fromData({
        'relatedUrl': '/my-event',
        'notificationId': '42',
      });
      expect(opened.relatedUrl, '/my-event');
      expect(opened.notificationId, 42);
    });

    test('eksik/bozuk id null olur', () {
      expect(PushOpened.fromData({}).notificationId, isNull);
      expect(
        PushOpened.fromData({'notificationId': 'x'}).notificationId,
        isNull,
      );
    });
  });
}
