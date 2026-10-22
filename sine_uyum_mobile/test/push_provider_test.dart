import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/push/push_messaging.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/repositories/device_repository.dart';
import 'package:sine_uyum_mobile/providers/push_provider.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/fake_network.dart';

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
    if (failRegister) throw Exception('no network');
    registered.add((token, platform));
  }

  @override
  Future<void> unregister(String token) async => unregistered.add(token);
}

/// Drives the session state from a test without touching storage/network.
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
  late FakeConnectivity connectivity;

  setUp(() {
    connectivity = FakeConnectivity();
    messaging = FakeMessaging();
    devices = FakeDevices();
    handled = [];
    container = ProviderContainer(
      overrides: [
        sessionProvider.overrideWith(FakeSession.new),
        pushMessagingProvider.overrideWithValue(messaging),
        deviceRepositoryProvider.overrideWithValue(devices),
        pushOpenedHandlerProvider.overrideWithValue(handled.add),
        connectivitySourceProvider.overrideWithValue(connectivity),
      ],
    );
    // The app watches the provider in the root widget; keep it alive here too.
    container.listen(pushProvider, (_, _) {});
  });

  tearDown(() => container.dispose());

  FakeSession session() =>
      container.read(sessionProvider.notifier) as FakeSession;

  Future<void> settle() async {
    // Let the event loop deliver stream events, then wait for the queued
    // steps to finish.
    await Future<void>.delayed(Duration.zero);
    await container.read(pushProvider.notifier).settled;
  }

  PushStatus status() => container.read(pushProvider);

  test(
    'on sign-in, permission is requested and the device registered',
    () async {
      session().signIn('u1');
      await settle();

      expect(devices.registered, [('token-1', 'android')]);
      expect(status(), PushStatus.registered);
    },
  );

  test('nothing is registered when Firebase is not configured', () async {
    messaging.configured = false;
    session().signIn('u1');
    await settle();

    expect(devices.registered, isEmpty);
    expect(status(), PushStatus.disabled);
  });

  test('nothing is registered without permission', () async {
    messaging.permission = false;
    session().signIn('u1');
    await settle();

    expect(devices.registered, isEmpty);
    expect(status(), PushStatus.permissionDenied);
  });

  test('a refreshed token is registered', () async {
    session().signIn('u1');
    await settle();

    messaging.refresh.add('token-2');
    await settle();

    expect(devices.registered.map((r) => r.$1), ['token-1', 'token-2']);
  });

  test('sign-out unregisters; later refreshes are not registered', () async {
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

  test('sign-out without a registration does not call the server', () async {
    session().signOut();
    await settle();
    expect(devices.unregistered, isEmpty);
  });

  test('the device is registered again for another account', () async {
    session().signIn('u1');
    await settle();
    session().signOut();
    await settle();
    session().signIn('u2');
    await settle();

    expect(devices.registered.map((r) => r.$1), ['token-1', 'token-1']);
    expect(devices.unregistered, ['token-1']);
  });

  test('the cold-start notification is opened once', () async {
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

  test('a notification tapped in the background is handled', () async {
    session().signIn('u1');
    await settle();

    messaging.opened.add(const PushOpened(relatedUrl: '/profile/u3'));
    await settle();

    expect(handled.map((o) => o.relatedUrl), ['/profile/u3']);
  });

  test('a registration network failure does not affect the app', () async {
    devices.failRegister = true;
    session().signIn('u1');
    await settle();

    expect(status(), isNot(PushStatus.registered));
    // Later steps are still processed.
    devices.failRegister = false;
    messaging.refresh.add('token-2');
    await settle();
    expect(devices.registered.map((r) => r.$1), ['token-2']);
  });

  test(
    'a registration that failed offline is retried when back online',
    () async {
      container.listen(networkStatusProvider, (_, _) {});
      devices.failRegister = true;
      session().signIn('u1');
      await settle();
      expect(status(), PushStatus.idle);

      devices.failRegister = false;
      connectivity.set(false);
      await settle();
      connectivity.set(true);
      await settle();

      expect(devices.registered.map((r) => r.$1), ['token-1']);
      expect(status(), PushStatus.registered);
    },
  );

  group('PushOpened.fromData', () {
    test("parses the server's string fields", () {
      final opened = PushOpened.fromData({
        'relatedUrl': '/my-event',
        'notificationId': '42',
      });
      expect(opened.relatedUrl, '/my-event');
      expect(opened.notificationId, 42);
    });

    test('a missing/invalid id becomes null', () {
      expect(PushOpened.fromData({}).notificationId, isNull);
      expect(
        PushOpened.fromData({'notificationId': 'x'}).notificationId,
        isNull,
      );
    });
  });
}
