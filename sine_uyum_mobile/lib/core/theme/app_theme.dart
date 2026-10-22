import 'package:flutter/material.dart';

/// Material 3 themes aligned with the web client's colour palette
/// (`sine-uyum-web/src/theme.js`).
class AppTheme {
  const AppTheme._();

  static const Color _lightPrimary = Color(0xFF1976D2);
  static const Color _lightSecondary = Color(0xFFDC004E);
  static const Color _darkPrimary = Color(0xFF64B5F6);
  static const Color _darkSecondary = Color(0xFFF48FB1);

  static ThemeData get light => _build(
    ColorScheme.fromSeed(
      seedColor: _lightPrimary,
      brightness: Brightness.light,
    ).copyWith(
      primary: _lightPrimary,
      secondary: _lightSecondary,
      surface: Colors.white,
    ),
    scaffoldBackground: const Color(0xFFF4F6F8),
  );

  static ThemeData get dark => _build(
    ColorScheme.fromSeed(
      seedColor: _darkPrimary,
      brightness: Brightness.dark,
    ).copyWith(
      primary: _darkPrimary,
      secondary: _darkSecondary,
      surface: const Color(0xFF1E1E1E),
    ),
    scaffoldBackground: const Color(0xFF121212),
  );

  static ThemeData _build(
    ColorScheme scheme, {
    required Color scaffoldBackground,
  }) {
    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: scaffoldBackground,
      appBarTheme: AppBarTheme(
        backgroundColor: scheme.surface,
        foregroundColor: scheme.onSurface,
        elevation: 0,
        scrolledUnderElevation: 2,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: scheme.surface,
        elevation: 1,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        margin: EdgeInsets.zero,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide.none,
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
      ),
      snackBarTheme: const SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
      ),
      listTileTheme: const ListTileThemeData(
        contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      ),
    );
  }
}
