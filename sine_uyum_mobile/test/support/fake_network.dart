import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/storage/preferences_storage.dart';
import 'package:sine_uyum_mobile/core/storage/token_storage.dart';

/// Lets a test decide what the server returns (or that there is no network).
class FakeServer implements HttpClientAdapter {
  /// `METHOD /path` → (status, body). 404 when nothing matches.
  final routes = <String, (int, Object?)>{};

  /// `METHOD /path` → an answer built from the query (paged endpoints);
  /// checked before [routes].
  final handlers = <String, (int, Object?) Function(Map<String, dynamic>)>{};
  bool offline = false;
  final requests = <String>[];

  /// Headers of every request, in order.
  final headers = <Map<String, dynamic>>[];

  /// Decoded JSON bodies of every request, in order (null when empty).
  final bodies = <Object?>[];

  /// Query parameters of every request, in order.
  final queries = <Map<String, dynamic>>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final key = '${options.method} ${options.path}';
    requests.add(key);
    headers.add(Map.of(options.headers));
    bodies.add(options.data);
    queries.add(Map.of(options.queryParameters));
    if (offline) {
      throw DioException.connectionError(
        requestOptions: options,
        reason: 'no network (test)',
      );
    }
    final (status, body) =
        handlers[key]?.call(options.queryParameters) ??
        routes[key] ??
        (404, {'title': 'not found'});
    return ResponseBody.fromString(
      body == null ? '' : jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class MemoryTokens extends TokenStorage {
  MemoryTokens({this.access, this.refresh});
  String? access;
  String? refresh;

  @override
  Future<String?> readAccessToken() async => access;
  @override
  Future<String?> readRefreshToken() async => refresh;
  @override
  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    access = accessToken;
    refresh = refreshToken;
  }

  @override
  Future<void> clear() async => access = refresh = null;
}

/// In-memory [PreferencesStorage] (no platform channel).
class MemoryPreferences extends PreferencesStorage {
  MemoryPreferences({this.language, this.theme});
  String? language;
  String? theme;

  @override
  Future<String?> readThemeMode() async => theme;

  @override
  Future<void> writeThemeMode(String mode) async => theme = mode;

  @override
  Future<String?> readLanguage() async => language;

  @override
  Future<void> writeLanguage(String? code) async => language = code;

  /// userId → last dismissed recap year.
  final dismissedRecapYears = <String, int>{};

  @override
  Future<int?> readDismissedRecapYear(String userId) async =>
      dismissedRecapYears[userId];

  @override
  Future<void> writeDismissedRecapYear(String userId, int year) async =>
      dismissedRecapYears[userId] = year;
}

Map<String, Object?> userJson(String id) => {
  'id': id,
  'userName': 'user_$id',
  'roles': ['User'],
};

/// Drives the OS connectivity information from a test.
class FakeConnectivity implements ConnectivitySource {
  FakeConnectivity({this.online = true});
  bool online;
  final _changes = StreamController<bool>.broadcast();

  void set(bool value) {
    online = value;
    _changes.add(value);
  }

  @override
  Future<bool> isOnline() async => online;

  @override
  Stream<bool> get changes => _changes.stream;
}
