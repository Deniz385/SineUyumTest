import 'dart:convert';
import 'dart:io';

import 'package:path_provider/path_provider.dart';

/// Çevrimdışı kullanım için okuma yanıtlarının yerel kopyası.
///
/// * Yalnızca [isCacheable] listesindeki GET uçları saklanır (arama ve öneri
///   gibi sınırsız büyüyen ya da çevrimdışı anlamsız olanlar saklanmaz).
/// * Aynı anda tek kullanıcının verisi tutulur: başka hesapla girişte, çıkışta
///   ve oturum düşünce silinir — cihazı sonra kullanan kişi önceki kullanıcının
///   mesajlarını göremesin.
class ResponseCache {
  ResponseCache(this._store);

  final CacheStore _store;
  String? _scope;

  static final _cacheable = <RegExp>[
    RegExp(r'^/api/v1/account/me$'),
    RegExp(r'^/api/v1/movies/(popular|now_playing|\d+)$'),
    RegExp(r'^/api/v1/messages$'),
    RegExp(r'^/api/v1/messages/thread/[^/]+$'),
    RegExp(r'^/api/v1/watchlist$'),
    RegExp(r'^/api/v1/watchlist/(public/)?\d+$'),
    RegExp(r'^/api/v1/profile/[^/]+$'),
    RegExp(r'^/api/v1/compatibility/[^/]+$'),
    RegExp(r'^/api/v1/follow/[^/]+/status$'),
    RegExp(r'^/api/v1/notification$'),
    RegExp(r'^/api/v1/event/my-status$'),
  ];

  static bool isCacheable(String path) =>
      _cacheable.any((r) => r.hasMatch(path));

  /// Kimin verisinin tutulduğu; oturum yokken null (hiçbir şey saklanmaz).
  String? get scope => _scope;

  // Önbellek bir iyileştirmedir: depolama hatası (disk dolu, izin) giriş
  // yapmayı ya da isteği asla bozmamalı; hata "önbellek yok" gibi davranır.
  static Future<T?> _safe<T>(Future<T> Function() op) async {
    try {
      return await op();
    } catch (_) {
      return null;
    }
  }

  /// Uygulama açılışında son kullanıcının kapsamını geri yükler (oturum henüz
  /// doğrulanmadan çevrimdışı /me okunabilsin diye).
  Future<void> restoreScope() async =>
      _scope ??= await _safe<String?>(_store.readScope);

  /// Oturum açan kullanıcıyı kapsam yapar. Farklı bir kullanıcıysa önceki
  /// kullanıcının tüm verisi silinir.
  Future<void> useScope(String userId) async {
    final stored = _scope ?? await _safe<String?>(_store.readScope);
    if (stored != null && stored != userId) await _safe(_store.clearAll);
    _scope = userId;
    await _safe(() => _store.writeScope(userId));
  }

  /// Çıkış / oturum sonu: tüm veri ve kapsam silinir.
  Future<void> clear() async {
    _scope = null;
    await _safe(_store.clearAll);
  }

  /// Saklanabilir istekse anahtar, değilse null.
  String? keyFor(String path, Map<String, dynamic>? query) {
    if (_scope == null || !isCacheable(path)) return null;
    if (query == null || query.isEmpty) return path;
    final entries = query.entries.toList()
      ..sort((a, b) => a.key.compareTo(b.key));
    return '$path?${entries.map((e) => '${e.key}=${e.value}').join('&')}';
  }

  Future<Object?> read(String key) async {
    final scope = _scope;
    if (scope == null) return null;
    final raw = await _safe(() => _store.read(scope, key));
    if (raw == null) return null;
    try {
      return (jsonDecode(raw) as Map<String, dynamic>)['data'];
    } on FormatException {
      return null; // bozuk kayıt: yok say
    }
  }

  Future<void> write(String key, Object? data) async {
    final scope = _scope;
    if (scope == null) return;
    await _safe(
      () => _store.write(
        scope,
        key,
        jsonEncode({'savedAt': DateTime.now().toIso8601String(), 'data': data}),
      ),
    );
  }
}

/// Kalıcı depolama. Uygulamada dosya sistemi, testlerde bellek.
abstract class CacheStore {
  Future<String?> read(String scope, String key);
  Future<void> write(String scope, String key, String value);
  Future<void> clearAll();
  Future<String?> readScope();
  Future<void> writeScope(String scope);
}

class MemoryCacheStore implements CacheStore {
  final entries = <String, String>{};
  String? storedScope;

  @override
  Future<String?> read(String scope, String key) async =>
      entries['$scope|$key'];

  @override
  Future<void> write(String scope, String key, String value) async =>
      entries['$scope|$key'] = value;

  @override
  Future<void> clearAll() async {
    entries.clear();
    storedScope = null;
  }

  @override
  Future<String?> readScope() async => storedScope;

  @override
  Future<void> writeScope(String scope) async => storedScope = scope;
}

/// Her kayıt bir dosya. Uygulamanın önbellek dizininde tutulur: yedeklemeye
/// (iCloud/Android) girmez ve işletim sistemi yer gerekince silebilir — önbellek
/// için doğru anlam. Kayıt sayısı sınırlıdır; aşılınca en eskiler silinir.
class FileCacheStore implements CacheStore {
  FileCacheStore({Future<Directory> Function()? root, this.maxEntries = 300})
    : _rootFn = root ?? _defaultRoot;

  final Future<Directory> Function() _rootFn;
  final int maxEntries;
  Future<Directory>? _root;

  static Future<Directory> _defaultRoot() async =>
      Directory('${(await getApplicationCacheDirectory()).path}/api_cache');

  Future<Directory> get _dir async {
    final dir = await (_root ??= _rootFn());
    if (!await dir.exists()) await dir.create(recursive: true);
    return dir;
  }

  // Anahtar dosya adına güvenli, sabit bir özete çevrilir (FNV-1a 64 bit).
  // Çakışmaya karşı anahtarın kendisi de kayda yazılıp okurken doğrulanır.
  static String _hash(String input) {
    var hash = 0xcbf29ce484222325;
    for (final unit in utf8.encode(input)) {
      hash ^= unit;
      hash = (hash * 0x100000001b3) & 0xFFFFFFFFFFFFFFFF;
    }
    return hash.toUnsigned(64).toRadixString(16).padLeft(16, '0');
  }

  Future<File> _file(String scope, String key) async =>
      File('${(await _dir).path}/${_hash('$scope|$key')}.json');

  @override
  Future<String?> read(String scope, String key) async {
    try {
      final file = await _file(scope, key);
      if (!await file.exists()) return null;
      final record =
          jsonDecode(await file.readAsString()) as Map<String, dynamic>;
      if (record['k'] != '$scope|$key') return null;
      return record['v'] as String?;
    } catch (_) {
      return null; // okunamayan kayıt önbellek ıskası sayılır
    }
  }

  @override
  Future<void> write(String scope, String key, String value) async {
    final file = await _file(scope, key);
    final temp = File('${file.path}.tmp');
    await temp.writeAsString(
      jsonEncode({'k': '$scope|$key', 'v': value}),
      flush: true,
    );
    await temp.rename(file.path);
    await _evictOldest();
  }

  Future<void> _evictOldest() async {
    final files = (await _dir)
        .listSync()
        .whereType<File>()
        .where((f) => f.path.endsWith('.json'))
        .toList();
    if (files.length <= maxEntries) return;
    files.sort((a, b) => a.lastModifiedSync().compareTo(b.lastModifiedSync()));
    for (final f in files.take(files.length - maxEntries)) {
      try {
        f.deleteSync();
      } catch (_) {}
    }
  }

  @override
  Future<void> clearAll() async {
    final dir = await (_root ??= _rootFn());
    if (await dir.exists()) await dir.delete(recursive: true);
  }

  @override
  Future<String?> readScope() async {
    final file = File('${(await _dir).path}/scope');
    return await file.exists() ? (await file.readAsString()).trim() : null;
  }

  @override
  Future<void> writeScope(String scope) async =>
      File('${(await _dir).path}/scope').writeAsString(scope, flush: true);
}
