import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'locale_provider.dart';

/// Theme saved by the user, read in `main()` before the first frame (so the
/// app never flashes in the other theme) and injected as an override. Null:
/// never chosen.
final savedThemeModeProvider = Provider<ThemeMode?>((ref) => null);

final themeModeProvider = NotifierProvider<ThemeModeNotifier, ThemeMode>(
  ThemeModeNotifier.new,
);

/// Light / dark / system theme. Dark (the cinematic default) until the user
/// picks one; the choice is persisted like the language.
class ThemeModeNotifier extends Notifier<ThemeMode> {
  @override
  ThemeMode build() => ref.read(savedThemeModeProvider) ?? ThemeMode.dark;

  void toggle() => set(switch (state) {
    ThemeMode.light => ThemeMode.dark,
    ThemeMode.dark => ThemeMode.light,
    ThemeMode.system => ThemeMode.dark,
  });

  Future<void> set(ThemeMode mode) async {
    if (mode == state) return;
    state = mode;
    await ref.read(preferencesStorageProvider).writeThemeMode(mode.name);
  }
}

/// The stored value ("light", "dark", "system") as a [ThemeMode]; null for
/// anything else.
ThemeMode? parseThemeMode(String? value) {
  for (final mode in ThemeMode.values) {
    if (mode.name == value) return mode;
  }
  return null;
}
