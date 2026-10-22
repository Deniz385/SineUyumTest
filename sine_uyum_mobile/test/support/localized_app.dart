import 'package:flutter/material.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';

const tr = Locale('tr');
const en = Locale('en');

/// A [MaterialApp] with the app's localizations, rendering in [locale].
///
/// Also points the context-free strings ([L10n.current], used by API error
/// fallbacks and model defaults) at [locale], as the real app does.
MaterialApp localizedApp({
  required Widget home,
  Locale locale = tr,
  TransitionBuilder? builder,
}) {
  L10n.locale = locale;
  return MaterialApp(
    locale: locale,
    supportedLocales: AppLocalizations.supportedLocales,
    localizationsDelegates: AppLocalizations.localizationsDelegates,
    builder: builder,
    home: home,
  );
}

/// Router variant of [localizedApp].
MaterialApp localizedRouterApp({
  required RouterConfig<Object> routerConfig,
  Locale locale = tr,
}) {
  L10n.locale = locale;
  return MaterialApp.router(
    locale: locale,
    supportedLocales: AppLocalizations.supportedLocales,
    localizationsDelegates: AppLocalizations.localizationsDelegates,
    routerConfig: routerConfig,
  );
}

/// Loads date formatting data for every supported locale (main.dart does
/// the same in the app).
Future<void> initializeAllDateFormatting() async {
  for (final locale in AppLocalizations.supportedLocales) {
    await initializeDateFormatting(locale.toLanguageTag());
  }
}
