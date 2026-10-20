import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_client.dart';
import '../core/network/api_exception.dart';
import '../core/offline/network_status.dart';
import '../core/offline/response_cache.dart';
import '../core/storage/token_storage.dart';
import '../data/models/app_user.dart';
import '../data/repositories/auth_repository.dart';

enum SessionStatus {
  /// Depodaki token henüz okunmadı — açılış ekranı gösterilir.
  unknown,
  authenticated,
  unauthenticated,
}

class SessionState {
  const SessionState({required this.status, this.user});

  const SessionState.unknown() : this(status: SessionStatus.unknown);
  const SessionState.signedOut() : this(status: SessionStatus.unauthenticated);

  final SessionStatus status;
  final AppUser? user;

  bool get isAuthenticated => status == SessionStatus.authenticated;
}

final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

/// Ağ katmanı; üretimde null (gerçek ağ), testlerde taklit edilir.
final httpAdapterProvider = Provider<HttpClientAdapter?>((ref) => null);

/// Çevrimdışı okuma önbelleği (uygulamada dosya, testlerde bellek).
final cacheStoreProvider = Provider<CacheStore>((ref) => FileCacheStore());

final responseCacheProvider = Provider<ResponseCache>(
  (ref) => ResponseCache(ref.watch(cacheStoreProvider)),
);

final apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient(
    ref.watch(tokenStorageProvider),
    adapter: ref.watch(httpAdapterProvider),
  );
  client.cache = ref.watch(responseCacheProvider);
  client.isDeviceOffline = () => !ref.read(networkStatusProvider).deviceOnline;
  client.onServerReachability = (reachable) =>
      ref.read(networkStatusProvider.notifier).reportServer(reachable);

  // Yenileme de başarısız olduğunda oturumu kapat.
  client.onSessionExpired = () {
    ref.read(sessionProvider.notifier).handleSessionExpired();
  };

  return client;
});

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(ref.watch(apiClientProvider)),
);

final sessionProvider = NotifierProvider<SessionNotifier, SessionState>(
  SessionNotifier.new,
);

class SessionNotifier extends Notifier<SessionState> {
  @override
  SessionState build() {
    // Çevrimdışı açılışta oturum doğrulanamadıysa ağ dönünce yeniden dene.
    ref.listen<NetworkStatus>(networkStatusProvider, (previous, next) {
      final cameBack = previous?.isOffline == true && !next.isOffline;
      if (cameBack && state.status == SessionStatus.unknown) restore();
    });

    // Token'ı okumak asenkron; önce "bilinmiyor" durumuyla başlayıp
    // sonucu geldiğinde durumu güncelliyoruz.
    Future.microtask(restore);
    return const SessionState.unknown();
  }

  TokenStorage get _storage => ref.read(tokenStorageProvider);
  AuthRepository get _auth => ref.read(authRepositoryProvider);
  ResponseCache get _cache => ref.read(responseCacheProvider);

  /// Uygulama açılışında saklı token ile oturumu geri yükler.
  ///
  /// Yalnızca sunucunun kesin reddi oturumu kapatır. Ağ yoksa token'lar
  /// korunur: kayıtlı kullanıcı bilgisi varsa onunla açılır, yoksa açılış
  /// ekranında beklenir ve ağ dönünce yeniden denenir. Önceden HER hata
  /// token'ları siliyordu: uygulamayı internetsiz açan kullanıcı çıkışa
  /// düşüyor ve yeniden giriş yapmak zorunda kalıyordu.
  Future<void> restore() async {
    final token = await _storage.readAccessToken();
    if (token == null || token.isEmpty) {
      state = const SessionState.signedOut();
      return;
    }

    // Son kullanıcının önbelleği: çevrimdışı /me buradan okunabilsin.
    await _cache.restoreScope();

    try {
      // Token süresi dolmuşsa interceptor refresh token ile yeniler;
      // o da geçersizse 401 döner ve oturum kapanır.
      final user = await _auth.me();
      await _remember(user);
      state = SessionState(status: SessionStatus.authenticated, user: user);
    } on ApiException catch (error) {
      if (error.isNetworkError) return; // bekle: token'lar duruyor
      await _endSession();
    } catch (_) {
      await _endSession();
    }
  }

  // Oturum açan kullanıcıyı önbellek kapsamı yapar ve profilini saklar:
  // /me, kapsam ancak kendi yanıtından öğrenildiği için kendiliğinden
  // saklanamıyordu; bu olmadan çevrimdışı açılış ancak üçüncü seferde
  // çalışırdı.
  Future<void> _remember(AppUser user) async {
    await _cache.useScope(user.id);
    await _cache.write('/api/v1/account/me', user.toJson());
  }

  Future<void> _endSession() async {
    await _storage.clear();
    await _cache.clear();
    state = const SessionState.signedOut();
  }

  Future<void> login({
    required String username,
    required String password,
  }) async {
    final result = await _auth.login(username: username, password: password);
    await _storage.saveTokens(
      accessToken: result.token,
      refreshToken: result.refreshToken,
    );
    // Başka bir hesabın önbelleği varsa silinir.
    await _remember(result.user);
    state = SessionState(
      status: SessionStatus.authenticated,
      user: result.user,
    );
  }

  Future<void> register({
    required String username,
    required String email,
    required String password,
  }) async {
    await _auth.register(username: username, email: email, password: password);
    await login(username: username, password: password);
  }

  Future<void> logout() async {
    final refreshToken = await _storage.readRefreshToken();
    if (refreshToken != null && refreshToken.isNotEmpty) {
      // Sunucu tarafında iptal edilemese bile yerel token'ları siliyoruz.
      try {
        await _auth.logout(refreshToken);
      } catch (_) {
        // Yoksay: çıkış her durumda tamamlanmalı.
      }
    }
    await _endSession();
  }

  /// Abonelik aktifleştirildiğinde token ve kullanıcı bilgisi yenilenir.
  Future<void> activateSubscription() async {
    final result = await _auth.activateSubscription();
    await _storage.saveTokens(
      accessToken: result.token,
      refreshToken: result.refreshToken,
    );
    await _remember(result.user);
    state = SessionState(
      status: SessionStatus.authenticated,
      user: result.user,
    );
  }

  /// Profil güncellendikten sonra oturumdaki kullanıcıyı tazeler.
  Future<void> refreshUser() async {
    if (!state.isAuthenticated) return;
    try {
      final user = await _auth.me();
      state = SessionState(status: SessionStatus.authenticated, user: user);
    } catch (_) {
      // Ağ hatasında mevcut kullanıcı bilgisi korunur.
    }
  }

  void handleSessionExpired() {
    if (state.status == SessionStatus.unauthenticated) return;
    // Oturum bitti: önceki kullanıcının kayıtlı verisi cihazda kalmasın.
    _cache.clear();
    state = const SessionState.signedOut();
  }
}
