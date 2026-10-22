import 'dart:ui';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/storage/preferences_storage.dart';
import '../l10n/l10n.dart';

final preferencesStorageProvider = Provider<PreferencesStorage>(
  (ref) => PreferencesStorage(),
);

/// Language code saved by the user, read in `main()` before the first frame
/// (so the app never flashes in another language) and injected as an
/// override. Null: follow the device.
final savedLanguageProvider = Provider<String?>((ref) => null);

/// The user's language choice: one of `AppLocalizations.supportedLocales`,
/// or null for "system default".
final localeProvider = NotifierProvider<LocaleNotifier, Locale?>(
  LocaleNotifier.new,
);

class LocaleNotifier extends Notifier<Locale?> {
  @override
  Locale? build() => L10n.supported(ref.read(savedLanguageProvider));

  /// Applies and persists the choice; [locale] null follows the device.
  Future<void> set(Locale? locale) async {
    final next = locale == null ? null : L10n.supported(locale.languageCode);
    if (next == state) return;
    state = next;
    await ref
        .read(preferencesStorageProvider)
        .writeLanguage(next?.languageCode);
  }
}

/// The device's preferred locales. The app invalidates this when the OS
/// language changes (see `SineUyumApp.didChangeLocales`).
final deviceLocalesProvider = Provider<List<Locale>>(
  (ref) => PlatformDispatcher.instance.locales,
);

/// The locale the app actually renders in: the user's choice, otherwise the
/// device language if supported, otherwise English.
final appLocaleProvider = Provider<Locale>((ref) {
  final locale =
      ref.watch(localeProvider) ??
      L10n.resolve(ref.watch(deviceLocalesProvider));
  // Keep context-free lookups (API error fallbacks, model defaults) in the
  // same language as the UI.
  L10n.locale = locale;
  return locale;
});

/// Language code sent to the API (`Accept-Language`, SignalR `lang`).
///
/// Repository providers watch it, so a language change rebuilds them and
/// every screen refetches its server-localized data (titles, genres...).
final apiLanguageProvider = Provider<String>(
  (ref) => ref.watch(appLocaleProvider).languageCode,
);
