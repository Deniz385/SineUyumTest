import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/fake_network.dart';

// With the real ApiClient + dio: a 204 from the server means "no taste twin".
void main() {
  late FakeServer server;
  late ProviderContainer container;

  setUp(() {
    server = FakeServer();
    container = ProviderContainer(
      overrides: [
        httpAdapterProvider.overrideWithValue(server),
        tokenStorageProvider.overrideWithValue(
          MemoryTokens(access: 'a', refresh: 'r'),
        ),
        cacheStoreProvider.overrideWithValue(MemoryCacheStore()),
        connectivitySourceProvider.overrideWithValue(FakeConnectivity()),
      ],
    );
    addTearDown(container.dispose);
  });

  test('204 with an empty body → null', () async {
    server.routes['GET /api/v1/taste-twin'] = (204, null);
    expect(await container.read(socialRepositoryProvider).tasteTwin(), isNull);
  });

  test('200 → a taste twin', () async {
    server.routes['GET /api/v1/taste-twin'] = (
      200,
      {
        'user': {'id': 'u9', 'userName': 'zeynep'},
        'compatibility': 92.0,
        'commonMovieCount': 14,
        'isFollowing': false,
      },
    );
    final twin = await container.read(socialRepositoryProvider).tasteTwin();
    expect(twin!.user.userName, 'zeynep');
    expect(twin.commonMovieCount, 14);
  });
}
