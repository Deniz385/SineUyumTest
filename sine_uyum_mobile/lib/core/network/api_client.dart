import 'dart:async';

import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../storage/token_storage.dart';
import 'api_exception.dart';

/// Tüm HTTP trafiğinin tek giriş noktası.
///
/// Sorumlulukları:
///  * Her isteğe access token eklemek
///  * 401 alındığında refresh token ile sessizce yeni token almak ve isteği
///    tekrarlamak (kullanıcı tekrar giriş yapmak zorunda kalmaz)
///  * Yenileme de başarısızsa oturumu kapatmak
class ApiClient {
  ApiClient(this._tokenStorage, {Dio? dio})
      : _dio = dio ??
            Dio(
              BaseOptions(
                baseUrl: AppConfig.apiUrl,
                connectTimeout: const Duration(seconds: 15),
                receiveTimeout: const Duration(seconds: 20),
                headers: {'Accept': 'application/json'},
                // Hata kodlarını da yanıt olarak alıp kendimiz yorumluyoruz.
                validateStatus: (status) => status != null && status < 400,
              ),
            ) {
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: _onRequest,
        onError: _onError,
      ),
    );
  }

  final Dio _dio;
  final TokenStorage _tokenStorage;

  /// Oturum geri kazanılamadığında (refresh de başarısız) tetiklenir.
  /// Uygulama katmanı bunu dinleyip kullanıcıyı giriş ekranına yönlendirir.
  void Function()? onSessionExpired;

  /// Eşzamanlı 401'lerde tek bir yenileme isteği atılması için paylaşılan future.
  Future<String?>? _refreshFuture;

  Dio get dio => _dio;

  static const _authPaths = {
    '/api/account/login',
    '/api/account/register',
    '/api/account/refresh',
  };

  Future<void> _onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (!_authPaths.contains(options.path)) {
      final token = await _tokenStorage.readAccessToken();
      if (token != null && token.isNotEmpty) {
        options.headers['Authorization'] = 'Bearer $token';
      }
    }
    handler.next(options);
  }

  Future<void> _onError(
    DioException error,
    ErrorInterceptorHandler handler,
  ) async {
    final response = error.response;
    final isAuthPath = _authPaths.contains(error.requestOptions.path);
    final alreadyRetried = error.requestOptions.extra['__retried'] == true;

    if (response?.statusCode != 401 || isAuthPath || alreadyRetried) {
      return handler.next(error);
    }

    final newToken = await _refreshAccessToken();
    if (newToken == null) {
      await _tokenStorage.clear();
      onSessionExpired?.call();
      return handler.next(error);
    }

    // Yeni token ile orijinal isteği bir kez tekrarla.
    try {
      final options = error.requestOptions;
      options.extra['__retried'] = true;
      options.headers['Authorization'] = 'Bearer $newToken';

      final retried = await _dio.fetch(options);
      return handler.resolve(retried);
    } on DioException catch (retryError) {
      return handler.next(retryError);
    }
  }

  /// Refresh token ile yeni access token alır. Eşzamanlı çağrılar aynı
  /// future'ı paylaşır, böylece tek yenileme isteği atılır (token rotation
  /// nedeniyle ikinci istek zaten geçersiz olurdu).
  Future<String?> _refreshAccessToken() {
    return _refreshFuture ??= _performRefresh().whenComplete(() {
      _refreshFuture = null;
    });
  }

  Future<String?> _performRefresh() async {
    final refreshToken = await _tokenStorage.readRefreshToken();
    if (refreshToken == null || refreshToken.isEmpty) return null;

    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/account/refresh',
        data: {'refreshToken': refreshToken},
      );

      final data = response.data;
      final accessToken = data?['token'] as String?;
      final newRefreshToken = data?['refreshToken'] as String?;

      if (accessToken == null || newRefreshToken == null) return null;

      await _tokenStorage.saveTokens(
        accessToken: accessToken,
        refreshToken: newRefreshToken,
      );
      return accessToken;
    } on DioException {
      return null;
    }
  }

  Future<T> get<T>(String path, {Map<String, dynamic>? query}) async {
    return _unwrap(() => _dio.get<T>(path, queryParameters: query));
  }

  Future<T> post<T>(String path, {Object? body}) async {
    return _unwrap(() => _dio.post<T>(path, data: body));
  }

  Future<T> put<T>(String path, {Object? body}) async {
    return _unwrap(() => _dio.put<T>(path, data: body));
  }

  Future<T> delete<T>(String path) async {
    return _unwrap(() => _dio.delete<T>(path));
  }

  Future<T> _unwrap<T>(Future<Response<T>> Function() request) async {
    try {
      final response = await request();
      return response.data as T;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}
