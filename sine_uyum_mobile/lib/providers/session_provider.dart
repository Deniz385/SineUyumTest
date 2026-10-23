import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_client.dart';
import '../core/network/api_exception.dart';
import '../core/offline/network_status.dart';
import '../core/offline/response_cache.dart';
import '../core/storage/token_storage.dart';
import '../data/models/app_user.dart';
import '../data/repositories/auth_repository.dart';
import 'locale_provider.dart';

enum SessionStatus {
  /// The stored token hasn't been read yet — the splash screen is shown.
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

/// The network layer; null in production (real network), faked in tests.
final httpAdapterProvider = Provider<HttpClientAdapter?>((ref) => null);

/// Offline read cache (files in the app, memory in tests).
final cacheStoreProvider = Provider<CacheStore>((ref) => FileCacheStore());

final responseCacheProvider = Provider<ResponseCache>(
  (ref) => ResponseCache(
    ref.watch(cacheStoreProvider),
    language: () => ref.read(apiLanguageProvider),
  ),
);

final apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient(
    ref.watch(tokenStorageProvider),
    adapter: ref.watch(httpAdapterProvider),
  );
  client.cache = ref.watch(responseCacheProvider);
  // Read per request: a language change applies without rebuilding the client.
  client.languageCode = () => ref.read(apiLanguageProvider);
  client.isDeviceOffline = () => !ref.read(networkStatusProvider).deviceOnline;
  client.onServerReachability = (reachable) =>
      ref.read(networkStatusProvider.notifier).reportServer(reachable);

  // Sign out when the refresh fails too.
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
    // If the session couldn't be verified on an offline start, retry when
    // the network comes back.
    ref.listen<NetworkStatus>(networkStatusProvider, (previous, next) {
      final cameBack = previous?.isOffline == true && !next.isOffline;
      if (cameBack && state.status == SessionStatus.unknown) restore();
    });

    // Reading the token is async: start as "unknown" and update the state
    // once the result arrives.
    Future.microtask(restore);
    return const SessionState.unknown();
  }

  TokenStorage get _storage => ref.read(tokenStorageProvider);
  AuthRepository get _auth => ref.read(authRepositoryProvider);
  ResponseCache get _cache => ref.read(responseCacheProvider);

  /// Restores the session from the stored token at app start.
  ///
  /// Only a definite rejection by the server ends the session. Without a
  /// network the tokens are kept: the app opens with the saved user if there
  /// is one, otherwise it waits on the splash screen and retries when the
  /// network returns. Previously EVERY error deleted the tokens: a user who
  /// opened the app without internet was signed out and had to sign in
  /// again.
  Future<void> restore() async {
    final token = await _storage.readAccessToken();
    if (token == null || token.isEmpty) {
      state = const SessionState.signedOut();
      return;
    }

    // The last user's cache, so /me can be read from it offline.
    await _cache.restoreScope();

    try {
      // If the token has expired the interceptor renews it with the refresh
      // token; if that is invalid too, a 401 comes back and the session ends.
      final user = await _auth.me();
      await _remember(user);
      state = SessionState(status: SessionStatus.authenticated, user: user);
    } on ApiException catch (error) {
      if (error.isNetworkError) return; // wait: the tokens are kept
      await _endSession();
    } catch (_) {
      await _endSession();
    }
  }

  // Makes the signed-in user the cache scope and saves their profile: /me
  // couldn't be cached on its own because the scope is only learned from
  // its response; without this an offline start only worked the third time.
  Future<void> _remember(AppUser user) async {
    await _cache.useScope(user.id);
    final key = _cache.keyFor('/api/v1/account/me', null);
    if (key != null) await _cache.write(key, user.toJson());
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
    // Another account's cache, if any, is deleted.
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
      // The local tokens are deleted even if the server can't revoke them.
      try {
        await _auth.logout(refreshToken);
      } catch (_) {
        // Ignore: signing out must always complete.
      }
    }
    await _endSession();
  }

  /// Refreshes the session's user after a profile update.
  Future<void> refreshUser() async {
    if (!state.isAuthenticated) return;
    try {
      final user = await _auth.me();
      state = SessionState(status: SessionStatus.authenticated, user: user);
    } catch (_) {
      // On a network error the current user is kept.
    }
  }

  /// Ends the first-run welcome (finished or skipped): tells the server,
  /// then reloads the user. If the server can't be told (no network), the
  /// welcome still closes for this session — it must never trap anyone —
  /// and is offered again on the next start.
  Future<void> completeOnboarding() async {
    final user = state.user;
    if (!state.isAuthenticated || user == null) return;
    try {
      await _auth.completeOnboarding();
      final fresh = await _auth.me();
      if (!ref.mounted) return;
      await _remember(fresh);
      state = SessionState(
        status: SessionStatus.authenticated,
        // An older server might still say false right after; trust the POST.
        user: fresh.copyWith(onboardingCompleted: true),
      );
    } catch (_) {
      if (!ref.mounted) return;
      state = SessionState(
        status: SessionStatus.authenticated,
        user: user.copyWith(onboardingCompleted: true),
      );
    }
  }

  void handleSessionExpired() {
    if (state.status == SessionStatus.unauthenticated) return;
    // Session over: the previous user's saved data must not stay on the
    // device.
    _cache.clear();
    state = const SessionState.signedOut();
  }
}
