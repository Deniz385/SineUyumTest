import 'dart:async';

import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../offline/response_cache.dart';
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
  ApiClient(this._tokenStorage, {Dio? dio, HttpClientAdapter? adapter})
    : _dio =
          dio ??
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
    // Testlerde ağ katmanı taklit edilir; BaseOptions ve interceptor'lar
    // gerçek kalır.
    if (adapter != null) _dio.httpClientAdapter = adapter;
    _dio.interceptors.add(
      InterceptorsWrapper(onRequest: _onRequest, onError: _onError),
    );
  }

  final Dio _dio;
  final TokenStorage _tokenStorage;

  /// Oturum geri kazanılamadığında (refresh de başarısız) tetiklenir.
  /// Uygulama katmanı bunu dinleyip kullanıcıyı giriş ekranına yönlendirir.
  void Function()? onSessionExpired;

  /// Çevrimdışı kullanım için okuma yanıtlarının yerel kopyası (opsiyonel).
  ResponseCache? cache;

  /// Cihazın ağ bağlantısı yoksa true; o zaman önbellekli istekler ağı hiç
  /// beklemeden (zaman aşımı 15 sn) doğrudan önbellekten okunur.
  bool Function()? isDeviceOffline;

  /// Her yanıtta (sunucuya ulaşıldı: true) ve ağ hatasında (false) çağrılır.
  void Function(bool reachable)? onServerReachability;

  /// Eşzamanlı 401'lerde tek bir yenileme isteği atılması için paylaşılan future.
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
      // Yenileme isteği sunucuya ULAŞAMADI (ağ yok). Bu bir ret değil:
      // token'lar korunur, oturum kapanmaz. Önceden bu da "oturum bitti"
      // sayılıp token'lar siliniyordu. Çağırana 401 değil ağ hatası iletilir;
      // aksi halde 401'i gören çağıran (ör. açılış) oturumu yine kapatırdı.
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
  ///
  /// Sunucu reddederse null; sunucuya ulaşılamazsa [DioException] fırlatır.
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
      // Yanıt varsa sunucu reddetti (geçersiz/süresi dolmuş refresh token).
      if (error.response != null) return null;
      rethrow;
    }
  }

  Future<T> get<T>(String path, {Map<String, dynamic>? query}) async {
    final cacheKey = cache?.keyFor(path, query);
    if (cacheKey == null) {
      return _unwrap(() => _dio.get<T>(path, queryParameters: query));
    }

    // Cihaz çevrimdışıysa ağı hiç deneme: kayıt varsa anında göster.
    if (isDeviceOffline?.call() ?? false) {
      final cached = await cache!.read(cacheKey);
      if (cached != null) return cached as T;
      throw ApiException(
        'İnternet bağlantısı yok ve bu içerik daha önce kaydedilmemiş.',
        isNetworkError: true,
      );
    }

    try {
      final result = await _unwrap(
        () => _dio.get<T>(path, queryParameters: query),
      );
      // Kaydetme hatası (disk dolu vb.) isteği bozmasın.
      unawaited(cache!.write(cacheKey, result).catchError((_) {}));
      return result;
    } on ApiException catch (error) {
      // Yalnızca ağ hatasında önbelleğe düşülür; sunucunun verdiği yanıtlar
      // (403, 404...) olduğu gibi iletilir.
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
