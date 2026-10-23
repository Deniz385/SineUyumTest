import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app.dart';
import 'core/storage/preferences_storage.dart';
import 'l10n/l10n.dart';
import 'providers/locale_provider.dart';
import 'providers/theme_provider.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // DateFormat needs each locale's data loaded before formatting with it.
  for (final locale in AppLocalizations.supportedLocales) {
    await initializeDateFormatting(locale.toLanguageTag());
  }

  // Read the saved language and theme before the first frame so the app
  // doesn't briefly render in the device language or the other theme.
  final preferences = PreferencesStorage();
  final savedLanguage = await preferences.readLanguage();
  final savedTheme = parseThemeMode(await preferences.readThemeMode());

  runApp(
    ProviderScope(
      overrides: [
        preferencesStorageProvider.overrideWithValue(preferences),
        savedLanguageProvider.overrideWithValue(savedLanguage),
        savedThemeModeProvider.overrideWithValue(savedTheme),
      ],
      child: const SineUyumApp(),
    ),
  );
}
