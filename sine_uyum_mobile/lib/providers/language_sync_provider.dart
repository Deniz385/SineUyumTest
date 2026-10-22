import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'locale_provider.dart';
import 'session_provider.dart';

/// Tells the server the UI language (`PUT /api/v1/account/language`) so the
/// texts it generates outside a request (notifications, push) use it too.
///
/// Sent once whenever a user becomes signed in (login or a restored session)
/// and again on every language change while signed in. Best-effort: a
/// failure is ignored (requests still carry `Accept-Language`), and it is
/// sent again on the next sign-in or change.
///
/// Watched by the root widget so it lives as long as the app.
final languageSyncProvider = NotifierProvider<LanguageSyncNotifier, void>(
  LanguageSyncNotifier.new,
);

class LanguageSyncNotifier extends Notifier<void> {
  // Sends one at a time, in order, so the last language wins.
  Future<void> _pending = Future.value();

  @override
  void build() {
    ref.listen<SessionState>(sessionProvider, (previous, next) {
      final signedIn =
          next.isAuthenticated &&
          (previous?.isAuthenticated != true ||
              previous?.user?.id != next.user?.id);
      if (signedIn) _send();
    }, fireImmediately: true);

    ref.listen<String>(apiLanguageProvider, (previous, next) {
      if (previous != next && ref.read(sessionProvider).isAuthenticated) {
        _send();
      }
    });
  }

  /// Lets tests wait for pending updates to finish.
  @visibleForTesting
  Future<void> get settled => _pending;

  void _send() {
    _pending = _pending.then((_) async {
      if (!ref.mounted) return;
      final language = ref.read(apiLanguageProvider);
      try {
        await ref.read(authRepositoryProvider).updateLanguage(language);
      } catch (error) {
        debugPrint('Language update failed: $error');
      }
    });
  }
}
