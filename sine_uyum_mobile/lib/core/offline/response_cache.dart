import 'dart:convert';
import 'dart:io';

import 'package:path_provider/path_provider.dart';

/// Local copy of read responses for offline use.
///
/// * Only the GET endpoints listed in [isCacheable] are stored (search,
///   suggestions and the like grow without bound or are pointless offline).
/// * Only one user's data is kept at a time: it is deleted when another
///   account signs in, on sign-out and when the session ends, so the next
///   person using the device cannot read the previous user's messages.
/// * Keys include the request language. The server localizes responses
///   (titles, genres, messages) by `Accept-Language`, so after a language
///   switch an offline read must not serve a copy in the old language. This
///   keeps both copies (bounded by the store's entry limit) instead of
///   wiping the whole cache, which would leave nothing to show offline.
class ResponseCache {
  ResponseCache(this._store, {this._language});

  final CacheStore _store;
  final String Function()? _language;
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
    RegExp(r'^/api/v1/subscription$'),
    RegExp(r'^/api/v1/account/privacy$'),
    RegExp(r'^/api/v1/blocks$'),
    RegExp(r'^/api/v1/feed$'),
    RegExp(r'^/api/v1/reviews/movie/\d+$'),
    // A diary (pages and yearly summary) can be read back offline; the
    // providers refetch after every change, which refreshes the copy.
    RegExp(r'^/api/v1/diary/user/[^/]+(/summary)?$'),
  ];

  static bool isCacheable(String path) =>
      _cacheable.any((r) => r.hasMatch(path));

  /// Whose data is kept; null when signed out (nothing is stored).
  String? get scope => _scope;

  // The cache is an optimisation: a storage error (disk full, permissions)
  // must never break signing in or a request; it behaves like a cache miss.
  static Future<T?> _safe<T>(Future<T> Function() op) async {
    try {
      return await op();
    } catch (_) {
      return null;
    }
  }

  /// Restores the last user's scope at app start (so /me can be read offline
  /// before the session is verified).
  Future<void> restoreScope() async =>
      _scope ??= await _safe<String?>(_store.readScope);

  /// Makes the signed-in user the scope. If it is a different user, all of
  /// the previous user's data is deleted.
  Future<void> useScope(String userId) async {
    final stored = _scope ?? await _safe<String?>(_store.readScope);
    if (stored != null && stored != userId) await _safe(_store.clearAll);
    _scope = userId;
    await _safe(() => _store.writeScope(userId));
  }

  /// Sign-out / end of session: all data and the scope are deleted.
  Future<void> clear() async {
    _scope = null;
    await _safe(_store.clearAll);
  }

  /// The key for a cacheable request (language-prefixed, e.g.
  /// `en:/api/v1/feed`), otherwise null.
  String? keyFor(String path, Map<String, dynamic>? query) {
    if (_scope == null || !isCacheable(path)) return null;
    final language = _language?.call();
    final prefix = language == null || language.isEmpty ? '' : '$language:';
    if (query == null || query.isEmpty) return '$prefix$path';
    final entries = query.entries.toList()
      ..sort((a, b) => a.key.compareTo(b.key));
    return '$prefix$path?${entries.map((e) => '${e.key}=${e.value}').join('&')}';
  }

  Future<Object?> read(String key) async {
    final scope = _scope;
    if (scope == null) return null;
    final raw = await _safe(() => _store.read(scope, key));
    if (raw == null) return null;
    try {
      return (jsonDecode(raw) as Map<String, dynamic>)['data'];
    } on FormatException {
      return null; // corrupt entry: ignore
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

/// Persistent storage: the file system in the app, memory in tests.
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

/// One file per entry, in the app's cache directory: it is excluded from
/// backups (iCloud/Android) and the OS may delete it when space is needed,
/// which is the right semantics for a cache. The number of entries is
/// capped; beyond that the oldest are deleted.
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

  // The key is turned into a stable, filename-safe hash (FNV-1a 64-bit).
  // The key itself is also stored in the entry and checked on read, to
  // guard against collisions.
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
      return null; // an unreadable entry counts as a cache miss
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
