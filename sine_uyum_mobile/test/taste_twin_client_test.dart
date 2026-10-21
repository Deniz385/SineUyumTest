import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/fake_network.dart';

// Gerçek ApiClient + dio ile: sunucunun 204'ü "zevk ikizi yok" demektir.
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

  test('204 boş gövde → null', () async {
    server.routes['GET /api/v1/taste-twin'] = (204, null);
    expect(await container.read(socialRepositoryProvider).tasteTwin(), isNull);
  });

  test('200 → zevk ikizi', () async {
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
