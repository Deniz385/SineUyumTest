import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/fake_network.dart';

void main() {
  late FakeServer server;
  late MemoryTokens tokens;
  late MemoryCacheStore store;
  late FakeConnectivity connectivity;
  late ProviderContainer container;

  ProviderContainer make({HttpClientAdapter? adapter}) {
    final c = ProviderContainer(
      overrides: [
        httpAdapterProvider.overrideWithValue(adapter ?? server),
        tokenStorageProvider.overrideWithValue(tokens),
        cacheStoreProvider.overrideWithValue(store),
        connectivitySourceProvider.overrideWithValue(connectivity),
      ],
    );
    addTearDown(c.dispose);
    return c;
  }

  setUp(() {
    server = FakeServer();
    tokens = MemoryTokens(access: 'old-access', refresh: 'valid-refresh');
    store = MemoryCacheStore();
    connectivity = FakeConnectivity();
    container = make();
  });

  Future<void> pump([int rounds = 30]) async {
    for (var i = 0; i < rounds; i++) {
      await Future<void>.delayed(const Duration(milliseconds: 5));
    }
  }

  Future<SessionState> start() async {
    container.listen(sessionProvider, (_, _) {});
    container.listen(networkStatusProvider, (_, _) {});
    await pump();
    return container.read(sessionProvider);
  }

  group('session (offline failures)', () {
    test('an offline first start keeps the tokens; signs in once the network '
        'is back', () async {
      server.offline = true;

      var state = await start();
      expect(tokens.access, 'old-access');
      expect(tokens.refresh, 'valid-refresh');
      expect(
        state.status,
        SessionStatus.unknown,
        reason: 'should wait at start',
      );

      // The network comes back.
      server.offline = false;
      server.routes['GET /api/v1/account/me'] = (200, userJson('u1'));
      connectivity.set(false);
      await pump(5);
      connectivity.set(true);
      await pump();

      state = container.read(sessionProvider);
      expect(state.status, SessionStatus.authenticated);
      expect(state.user?.id, 'u1');
    });

    test('opens offline with the saved user if it was opened before', () async {
      server.routes['GET /api/v1/account/me'] = (200, userJson('u1'));
      await start(); // online start: /me is saved

      // The app is closed and reopened offline (same store).
      server.offline = true;
      container = make();
      final state = await start();

      expect(state.status, SessionStatus.authenticated);
      expect(state.user?.id, 'u1');
    });

    test(
      'the session stays open if the network drops during a refresh',
      () async {
        server.routes['GET /api/v1/account/me'] = (401, null);
        container = make(adapter: _RefreshUnreachable(server));
        var expired = false;
        container.read(apiClientProvider).onSessionExpired = () =>
            expired = true;

        final state = await start();

        expect(
          expired,
          isFalse,
          reason: 'a network error was treated as "session over"',
        );
        expect(tokens.refresh, 'valid-refresh');
        expect(state.status, isNot(SessionStatus.unauthenticated));
      },
    );

    test('if the server rejects the refresh token the session ends and the '
        'cache is cleared', () async {
      store.storedScope = 'u1';
      store.entries['u1|/api/v1/messages'] = '{"data":[]}';
      server.routes['GET /api/v1/account/me'] = (401, null);
      server.routes['POST /api/v1/account/refresh'] = (401, null);

      final state = await start();

      expect(state.status, SessionStatus.unauthenticated);
      expect(tokens.refresh, isNull);
      expect(store.entries, isEmpty);
    });
  });

  group('read cache', () {
    late ResponseCache cache;

    setUp(() async {
      server.routes['GET /api/v1/account/me'] = (200, userJson('u1'));
      await start();
      cache = container.read(responseCacheProvider);
    });

    test('returns the last successful response on a network error', () async {
      server.routes['GET /api/v1/messages'] = (
        200,
        [
          {'otherUserId': 'ali', 'lastMessageContent': 'hello'},
        ],
      );
      final api = container.read(apiClientProvider);
      await api.get<List<dynamic>>('/api/v1/messages');
      await pump(3);

      server.offline = true;
      final offline = await api.get<List<dynamic>>('/api/v1/messages');

      expect((offline.single as Map)['lastMessageContent'], 'hello');
      expect(container.read(networkStatusProvider).isOffline, isTrue);
    });

    test('the network is not tried while the device is offline', () async {
      server.routes['GET /api/v1/notification'] = (200, <Object>[]);
      final api = container.read(apiClientProvider);
      await api.get<List<dynamic>>('/api/v1/notification');
      await pump(3);

      connectivity.set(false);
      await pump(3);
      server.requests.clear();
      await api.get<List<dynamic>>('/api/v1/notification');

      expect(server.requests, isEmpty);
    });

    test('endpoints not on the list (like search) are not cached', () async {
      server.routes['GET /api/v1/account/search'] = (200, <Object>[]);
      final api = container.read(apiClientProvider);
      await api.get<List<dynamic>>(
        '/api/v1/account/search',
        query: {'query': 'ay'},
      );
      await pump(3);

      server.offline = true;
      await expectLater(
        api.get<List<dynamic>>(
          '/api/v1/account/search',
          query: {'query': 'ay'},
        ),
        throwsA(isA<ApiException>()),
      );
    });

    test('server responses (404 etc.) are not masked by the cache', () async {
      final api = container.read(apiClientProvider);
      server.routes['GET /api/v1/profile/x'] = (200, {'id': 'x'});
      await api.get<Map<String, dynamic>>('/api/v1/profile/x');
      await pump(3);

      server.routes['GET /api/v1/profile/x'] = (404, {'title': 'not found'});
      await expectLater(
        api.get<Map<String, dynamic>>('/api/v1/profile/x'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 404)),
      );
    });

    test(
      "signing in with another account deletes the previous user's data",
      () async {
        final api = container.read(apiClientProvider);
        server.routes['GET /api/v1/messages'] = (200, <Object>[]);
        await api.get<List<dynamic>>('/api/v1/messages');
        await pump(3);
        expect(store.entries.keys.any((k) => k.startsWith('u1|')), isTrue);

        server.routes['POST /api/v1/account/login'] = (
          200,
          {
            'token': 't2',
            'refreshToken': 'r2',
            'expiresAt': '2030-01-01T00:00:00Z',
            'user': userJson('u2'),
          },
        );
        await container
            .read(sessionProvider.notifier)
            .login(username: 'b', password: 'p');

        expect(store.entries.keys.any((k) => k.startsWith('u1|')), isFalse);
        expect(cache.scope, 'u2');
      },
    );

    test('the cache is cleared on sign-out', () async {
      final api = container.read(apiClientProvider);
      server.routes['GET /api/v1/messages'] = (200, <Object>[]);
      await api.get<List<dynamic>>('/api/v1/messages');
      await pump(3);
      server.routes['POST /api/v1/account/logout'] = (200, {'message': 'ok'});

      await container.read(sessionProvider.notifier).logout();

      expect(store.entries, isEmpty);
      expect(cache.scope, isNull);
    });
  });
}

/// /me reaches the server (401), while the refresh request gets a network
/// error.
class _RefreshUnreachable extends FakeServer {
  _RefreshUnreachable(this.inner);
  final FakeServer inner;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) {
    if (options.path == '/api/v1/account/refresh') {
      throw DioException.connectionError(
        requestOptions: options,
        reason: 'no network (test)',
      );
    }
    return inner.fetch(options, requestStream, cancelFuture);
  }
}
