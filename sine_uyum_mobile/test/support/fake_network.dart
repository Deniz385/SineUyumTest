import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/storage/token_storage.dart';

/// Sunucunun ne döneceğini (ya da ağın hiç olmadığını) testten belirlemek için.
class FakeServer implements HttpClientAdapter {
  /// `METHOD /yol` → (durum, gövde). Eşleşme yoksa 404.
  final routes = <String, (int, Object?)>{};
  bool offline = false;
  final requests = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final key = '${options.method} ${options.path}';
    requests.add(key);
    if (offline) {
      throw DioException.connectionError(
        requestOptions: options,
        reason: 'ağ yok (test)',
      );
    }
    final (status, body) = routes[key] ?? (404, {'title': 'yok'});
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

Map<String, Object?> userJson(String id) => {
  'id': id,
  'userName': 'kullanici_$id',
  'roles': ['User'],
};

/// İşletim sisteminin bağlantı bilgisini testten sürmek için.
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
