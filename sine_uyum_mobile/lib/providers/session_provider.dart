import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_client.dart';
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

final apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient(ref.watch(tokenStorageProvider));

  // Yenileme de başarısız olduğunda oturumu kapat.
  client.onSessionExpired = () {
    ref.read(sessionProvider.notifier).handleSessionExpired();
  };

  return client;
});

final authRepositoryProvider =
    Provider<AuthRepository>((ref) => AuthRepository(ref.watch(apiClientProvider)));

final sessionProvider =
    NotifierProvider<SessionNotifier, SessionState>(SessionNotifier.new);

class SessionNotifier extends Notifier<SessionState> {
  @override
  SessionState build() {
    // Token'ı okumak asenkron; önce "bilinmiyor" durumuyla başlayıp
    // sonucu geldiğinde durumu güncelliyoruz.
    Future.microtask(restore);
    return const SessionState.unknown();
  }

  TokenStorage get _storage => ref.read(tokenStorageProvider);
  AuthRepository get _auth => ref.read(authRepositoryProvider);

  /// Uygulama açılışında saklı token ile oturumu geri yükler.
  Future<void> restore() async {
    final token = await _storage.readAccessToken();
    if (token == null || token.isEmpty) {
      state = const SessionState.signedOut();
      return;
    }

    try {
      // Token süresi dolmuşsa interceptor refresh token ile yeniler;
      // o da geçersizse 401 döner ve oturum kapanır.
      final user = await _auth.me();
      state = SessionState(status: SessionStatus.authenticated, user: user);
    } catch (_) {
      await _storage.clear();
      state = const SessionState.signedOut();
    }
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
    await _auth.register(
      username: username,
      email: email,
      password: password,
    );
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
    await _storage.clear();
    state = const SessionState.signedOut();
  }

  /// Abonelik aktifleştirildiğinde token ve kullanıcı bilgisi yenilenir.
  Future<void> activateSubscription() async {
    final result = await _auth.activateSubscription();
    await _storage.saveTokens(
      accessToken: result.token,
      refreshToken: result.refreshToken,
    );
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
    state = const SessionState.signedOut();
  }
}
