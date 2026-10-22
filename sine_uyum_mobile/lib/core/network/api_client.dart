import 'dart:async';

import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../offline/response_cache.dart';
import '../storage/token_storage.dart';
import 'api_exception.dart';

/// Single entry point for all HTTP traffic.
///
/// Responsibilities:
///  * Attach the access token and the UI language (`Accept-Language`) to
///    every request
///  * On a 401, silently get a new token with the refresh token and retry
///    the request (the user does not have to sign in again)
///  * Sign the user out if the refresh fails too
class ApiClient {
  ApiClient(this._tokenStorage, {Dio? dio, HttpClientAdapter? adapter})
    : _dio =
          dio ??
          Dio(
            BaseOptions(
              baseUrl: AppConfig.apiUrl,
              connectTimeout: const Duration(seconds: 15),
              receiveTimeout: const Duration(seconds: 20),
              headers: {'Accept': 'application/json'},
              // Error statuses are received as responses and interpreted here.
              validateStatus: (status) => status != null && status < 400,
            ),
          ) {
    // Tests fake the network layer; BaseOptions and interceptors stay real.
    if (adapter != null) _dio.httpClientAdapter = adapter;
    _dio.interceptors.add(
      InterceptorsWrapper(onRequest: _onRequest, onError: _onError),
    );
  }

  final Dio _dio;
  final TokenStorage _tokenStorage;

  /// Called when the session cannot be recovered (the refresh failed too).
  /// The app layer listens and sends the user to the sign-in screen.
  void Function()? onSessionExpired;

  /// Local copy of read responses for offline use (optional).
  ResponseCache? cache;

  /// True when the device has no network; cached requests are then served
  /// from the cache right away instead of waiting for the 15 s timeout.
  bool Function()? isDeviceOffline;

  /// Called on every response (server reached: true) and on network errors
  /// (false).
  void Function(bool reachable)? onServerReachability;

  /// Language code for `Accept-Language` ("tr", "en"...). Read per request so
  /// a language change applies immediately. The server localizes its
  /// messages and data accordingly (falling back to English).
  String Function()? languageCode;

  /// Shared future so that concurrent 401s trigger a single refresh request.
  Future<String?>? _refreshFuture;

  Dio get dio => _dio;

  static const _authPaths = {
    '/api/v1/account/login',
    '/api/v1/account/register',
    '/api/v1/account/refresh',
  };

  Future<void> _onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final language = languageCode?.call();
    if (language != null && language.isNotEmpty) {
      options.headers['Accept-Language'] = language;
    }
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

    final String? newToken;
    try {
      newToken = await _refreshAccessToken();
    } on DioException catch (refreshError) {
      // The refresh request never REACHED the server (no network). That is
      // not a rejection: the tokens are kept and the session stays open.
      // This used to count as "session over" and delete the tokens. The
      // caller gets a network error instead of the 401; otherwise a caller
      // seeing the 401 (e.g. app start) would still sign the user out.
      return handler.next(
        DioException(
          requestOptions: error.requestOptions,
          type: refreshError.type,
          error: refreshError.error,
          message: refreshError.message,
        ),
      );
    }
    if (newToken == null) {
      await _tokenStorage.clear();
      onSessionExpired?.call();
      return handler.next(error);
    }

    // Retry the original request once with the new token.
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

  /// Gets a new access token with the refresh token. Concurrent calls share
  /// one future, so a single refresh request is sent (with token rotation a
  /// second request would be invalid anyway).
  ///
  /// Returns null if the server rejects it; throws [DioException] if the
  /// server cannot be reached.
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
        '/api/v1/account/refresh',
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
    } on DioException catch (error) {
      // A response means the server rejected it (invalid/expired token).
      if (error.response != null) return null;
      rethrow;
    }
  }

  Future<T> get<T>(String path, {Map<String, dynamic>? query}) async {
    final cacheKey = cache?.keyFor(path, query);
    if (cacheKey == null) {
      return _unwrap(() => _dio.get<T>(path, queryParameters: query));
    }

    // Device offline: don't try the network, show the saved copy at once.
    if (isDeviceOffline?.call() ?? false) {
      final cached = await cache!.read(cacheKey);
      if (cached != null) return cached as T;
      throw ApiException(
        null,
        isNetworkError: true,
        kind: ApiErrorKind.offlineNotCached,
      );
    }

    try {
      final result = await _unwrap(
        () => _dio.get<T>(path, queryParameters: query),
      );
      // A failed save (disk full etc.) must not break the request.
      unawaited(cache!.write(cacheKey, result).catchError((_) {}));
      return result;
    } on ApiException catch (error) {
      // Only network errors fall back to the cache; server responses
      // (403, 404...) are passed on as they are.
      if (!error.isNetworkError) rethrow;
      final cached = await cache!.read(cacheKey);
      if (cached == null) rethrow;
      return cached as T;
    }
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
      onServerReachability?.call(true);
      return response.data as T;
    } on DioException catch (error) {
      final exception = ApiException.fromDio(error);
      onServerReachability?.call(!exception.isNetworkError);
      throw exception;
    }
  }
}
