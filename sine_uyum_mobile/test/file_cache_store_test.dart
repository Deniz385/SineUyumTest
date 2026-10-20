import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';

void main() {
  late Directory dir;
  late FileCacheStore store;

  setUp(() async {
    dir = await Directory.systemTemp.createTemp('api_cache_test');
    store = FileCacheStore(root: () async => dir, maxEntries: 3);
  });

  tearDown(() async {
    if (await dir.exists()) await dir.delete(recursive: true);
  });

  test('yazılan kayıt geri okunur, kapsam kalıcıdır', () async {
    await store.write('u1', '/api/v1/messages', '{"data":[1]}');
    await store.writeScope('u1');

    // Yeni örnek = uygulama yeniden açıldı.
    final reopened = FileCacheStore(root: () async => dir);
    expect(await reopened.read('u1', '/api/v1/messages'), '{"data":[1]}');
    expect(await reopened.readScope(), 'u1');
    expect(await reopened.read('u2', '/api/v1/messages'), isNull);
  });

  test('sınır aşılınca en eski kayıtlar silinir', () async {
    for (var i = 0; i < 5; i++) {
      await store.write('u1', '/k$i', 'v$i');
      // Değişiklik zamanlarının ayrışması için.
      await Future<void>.delayed(const Duration(milliseconds: 20));
    }

    expect(await store.read('u1', '/k0'), isNull);
    expect(await store.read('u1', '/k1'), isNull);
    expect(await store.read('u1', '/k4'), 'v4');
    final files = dir.listSync().where((f) => f.path.endsWith('.json'));
    expect(files.length, 3);
  });

  test('bozuk dosya ıska sayılır, hata fırlatmaz', () async {
    await store.write('u1', '/k', 'v');
    for (final f in dir.listSync().whereType<File>()) {
      if (f.path.endsWith('.json')) f.writeAsStringSync('{bozuk');
    }
    expect(await store.read('u1', '/k'), isNull);
  });

  test('clearAll her şeyi ve kapsamı siler', () async {
    await store.write('u1', '/k', 'v');
    await store.writeScope('u1');

    await store.clearAll();

    expect(await store.read('u1', '/k'), isNull);
    expect(await store.readScope(), isNull);
  });

  test('ResponseCache bozuk JSON içeriğini yok sayar', () async {
    final cache = ResponseCache(store);
    await cache.useScope('u1');
    await store.write('u1', '/api/v1/messages', 'json-degil');
    expect(await cache.read('/api/v1/messages'), isNull);
  });
}
