import 'package:flutter/material.dart';

import 'tokens.dart';

export 'tokens.dart';

/// Material 3 themes built from the design tokens (`docs/DESIGN-SYSTEM.md`):
/// cinematic dark by default, warm "paper" light as the alternative.
class AppTheme {
  const AppTheme._();

  static ThemeData get dark => build(AppColors.dark);
  static ThemeData get light => build(AppColors.light);

  static ColorScheme _scheme(AppColors c) {
    final primaryContainer = Color.alphaBlend(
      c.primary.withValues(alpha: c.isDark ? 0.18 : 0.14),
      c.surface,
    );
    final accentContainer = Color.alphaBlend(
      c.accent.withValues(alpha: c.isDark ? 0.18 : 0.12),
      c.surface,
    );
    final infoContainer = Color.alphaBlend(
      c.info.withValues(alpha: c.isDark ? 0.18 : 0.12),
      c.surface,
    );
    return ColorScheme(
      brightness: c.brightness,
      primary: c.primary,
      onPrimary: c.onPrimary,
      primaryContainer: primaryContainer,
      onPrimaryContainer: c.text,
      secondary: c.accent,
      onSecondary: Colors.white,
      secondaryContainer: c.surfaceRaised,
      onSecondaryContainer: c.text,
      tertiary: c.info,
      onTertiary: c.isDark ? const Color(0xFF00172E) : Colors.white,
      tertiaryContainer: infoContainer,
      onTertiaryContainer: c.text,
      error: c.error,
      onError: c.isDark ? const Color(0xFF2B0003) : Colors.white,
      errorContainer: accentContainer,
      onErrorContainer: c.text,
      surface: c.surface,
      onSurface: c.text,
      onSurfaceVariant: c.textSecondary,
      surfaceDim: c.bg,
      surfaceBright: c.surfaceOverlay,
      surfaceContainerLowest: c.bg,
      surfaceContainerLow: c.surface,
      surfaceContainer: c.surface,
      surfaceContainerHigh: c.surfaceRaised,
      surfaceContainerHighest: c.surfaceOverlay,
      outline: c.border,
      outlineVariant: c.border,
      shadow: Colors.black,
      scrim: Colors.black,
      inverseSurface: c.isDark
          ? AppColors.light.surface
          : AppColors.dark.surface,
      onInverseSurface: c.isDark ? AppColors.light.text : AppColors.dark.text,
      inversePrimary: c.isDark
          ? AppColors.light.primary
          : AppColors.dark.primary,
      surfaceTint: Colors.transparent,
    );
  }

  static TextTheme _textTheme(AppColors c) {
    TextStyle s(
      String family,
      double size,
      double lineHeight,
      FontWeight weight, {
      Color? color,
      double letterSpacing = 0,
    }) => TextStyle(
      fontFamily: family,
      fontSize: size,
      height: lineHeight / size,
      fontWeight: weight,
      letterSpacing: letterSpacing,
      color: color ?? c.text,
    );
    const d = AppFonts.display;
    const t = AppFonts.text;
    return TextTheme(
      displayLarge: s(d, 40, 44, FontWeight.w800),
      displayMedium: s(d, 34, 40, FontWeight.w800),
      displaySmall: s(d, 30, 36, FontWeight.w700),
      headlineLarge: s(d, 30, 36, FontWeight.w700),
      headlineMedium: s(d, 26, 32, FontWeight.w700),
      headlineSmall: s(d, 22, 28, FontWeight.w700),
      titleLarge: s(t, 18, 24, FontWeight.w600),
      titleMedium: s(t, 16, 22, FontWeight.w600),
      titleSmall: s(t, 14, 20, FontWeight.w600),
      bodyLarge: s(t, 16, 24, FontWeight.w400),
      bodyMedium: s(t, 15, 22, FontWeight.w400),
      bodySmall: s(t, 13, 18, FontWeight.w400, color: c.textSecondary),
      labelLarge: s(t, 14, 20, FontWeight.w600, letterSpacing: 0.1),
      labelMedium: s(
        t,
        12,
        16,
        FontWeight.w600,
        color: c.textSecondary,
        letterSpacing: 0.48,
      ),
      labelSmall: s(
        t,
        11,
        16,
        FontWeight.w600,
        color: c.textSecondary,
        letterSpacing: 0.44,
      ),
    );
  }

  static ThemeData build(AppColors c) {
    final scheme = _scheme(c);
    final text = _textTheme(c);
    final pill = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(AppRadii.pill),
    );
    const buttonPadding = EdgeInsets.symmetric(horizontal: 20, vertical: 12);
    const minButton = Size(64, 48);
    final focusRing = BorderSide(color: c.primary, width: 2);
    final inputRadius = BorderRadius.circular(AppRadii.sm);

    return ThemeData(
      useMaterial3: true,
      brightness: c.brightness,
      colorScheme: scheme,
      textTheme: text,
      fontFamily: AppFonts.text,
      scaffoldBackgroundColor: c.bg,
      canvasColor: c.bg,
      dividerColor: c.border,
      splashFactory: InkSparkle.splashFactory,
      materialTapTargetSize: MaterialTapTargetSize.padded,
      visualDensity: VisualDensity.standard,
      extensions: [c, AppTextStyles.of(c)],
      iconTheme: IconThemeData(color: c.textSecondary, size: 24),
      appBarTheme: AppBarTheme(
        backgroundColor: c.bg,
        foregroundColor: c.text,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleSpacing: AppSpace.gutter,
        iconTheme: IconThemeData(color: c.text),
        actionsIconTheme: IconThemeData(color: c.text),
        titleTextStyle: TextStyle(
          fontFamily: AppFonts.display,
          fontSize: 22,
          height: 28 / 22,
          fontWeight: FontWeight.w700,
          color: c.text,
        ),
      ),
      cardTheme: CardThemeData(
        color: c.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        shadowColor: c.isDark ? Colors.transparent : const Color(0x14000000),
        clipBehavior: Clip.antiAlias,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          side: BorderSide(color: c.border.withValues(alpha: 0.7)),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: c.primary,
          foregroundColor: c.onPrimary,
          disabledBackgroundColor: c.isDark ? c.surfaceOverlay : c.border,
          disabledForegroundColor: c.isDark ? c.textMuted : c.textSecondary,
          minimumSize: minButton,
          padding: buttonPadding,
          shape: pill,
          textStyle: text.labelLarge,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: c.text,
          minimumSize: minButton,
          padding: buttonPadding,
          shape: pill,
          side: BorderSide(color: c.border),
          textStyle: text.labelLarge,
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: c.primary,
          minimumSize: const Size(48, 48),
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          shape: pill,
          textStyle: text.labelLarge,
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: c.surfaceRaised,
          foregroundColor: c.text,
          elevation: 0,
          minimumSize: minButton,
          padding: buttonPadding,
          shape: pill,
          textStyle: text.labelLarge,
        ),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(
          minimumSize: const Size(48, 48),
          foregroundColor: c.text,
        ),
      ),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: c.primary,
        foregroundColor: c.onPrimary,
        elevation: 2,
        shape: pill,
        extendedTextStyle: text.labelLarge,
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: c.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        height: 68,
        indicatorColor: c.primary.withValues(alpha: c.isDark ? 0.18 : 0.14),
        indicatorShape: pill,
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        iconTheme: WidgetStateProperty.resolveWith(
          (states) => IconThemeData(
            size: 24,
            color: states.contains(WidgetState.selected)
                ? c.primary
                : c.textSecondary,
          ),
        ),
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => text.labelMedium!.copyWith(
            letterSpacing: 0.1,
            color: states.contains(WidgetState.selected)
                ? c.text
                : c.textSecondary,
          ),
        ),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: c.surfaceRaised,
        selectedColor: c.primary.withValues(alpha: c.isDark ? 0.2 : 0.16),
        disabledColor: c.surface,
        side: BorderSide(color: c.border),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.sm),
        ),
        labelStyle: text.labelLarge!.copyWith(color: c.text),
        secondaryLabelStyle: text.labelLarge!.copyWith(color: c.text),
        checkmarkColor: c.primary,
        iconTheme: IconThemeData(color: c.textSecondary, size: 18),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: c.text,
        unselectedLabelColor: c.textSecondary,
        indicatorColor: c.primary,
        indicatorSize: TabBarIndicatorSize.label,
        dividerColor: c.border,
        labelStyle: text.titleSmall,
        unselectedLabelStyle: text.titleSmall!.copyWith(
          fontWeight: FontWeight.w500,
        ),
        indicator: UnderlineTabIndicator(
          borderSide: BorderSide(color: c.primary, width: 3),
          borderRadius: const BorderRadius.vertical(top: Radius.circular(3)),
        ),
        overlayColor: WidgetStatePropertyAll(c.primary.withValues(alpha: 0.08)),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: ButtonStyle(
          minimumSize: const WidgetStatePropertyAll(Size(48, 44)),
          visualDensity: VisualDensity.standard,
          textStyle: WidgetStatePropertyAll(text.labelLarge),
          side: WidgetStatePropertyAll(BorderSide(color: c.border)),
          shape: WidgetStatePropertyAll(pill),
          backgroundColor: WidgetStateProperty.resolveWith(
            (states) => states.contains(WidgetState.selected)
                ? c.primary.withValues(alpha: c.isDark ? 0.2 : 0.14)
                : Colors.transparent,
          ),
          foregroundColor: WidgetStateProperty.resolveWith(
            (states) => states.contains(WidgetState.selected)
                ? c.text
                : c.textSecondary,
          ),
        ),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: c.surfaceOverlay,
        modalBackgroundColor: c.surfaceOverlay,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        dragHandleColor: c.border,
        elevation: c.isDark ? 0 : 8,
        modalBarrierColor: Colors.black.withValues(alpha: 0.6),
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(
            top: Radius.circular(AppRadii.lg),
          ),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: c.surfaceOverlay,
        surfaceTintColor: Colors.transparent,
        elevation: c.isDark ? 0 : 8,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.lg),
        ),
        titleTextStyle: text.headlineSmall,
        contentTextStyle: text.bodyMedium!.copyWith(color: c.textSecondary),
      ),
      popupMenuTheme: PopupMenuThemeData(
        color: c.surfaceOverlay,
        surfaceTintColor: Colors.transparent,
        elevation: c.isDark ? 0 : 6,
        textStyle: text.bodyMedium,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          side: BorderSide(color: c.border),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: c.surfaceOverlay,
        contentTextStyle: text.bodyMedium,
        actionTextColor: c.primary,
        elevation: c.isDark ? 0 : 6,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          side: BorderSide(color: c.border),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: c.surfaceRaised,
        hintStyle: text.bodyMedium!.copyWith(color: c.textMuted),
        labelStyle: text.bodyMedium!.copyWith(color: c.textSecondary),
        floatingLabelStyle: text.bodyMedium!.copyWith(color: c.primary),
        helperStyle: text.bodySmall,
        prefixIconColor: c.textSecondary,
        suffixIconColor: c.textSecondary,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 14,
        ),
        border: OutlineInputBorder(
          borderRadius: inputRadius,
          borderSide: BorderSide(color: c.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: inputRadius,
          borderSide: BorderSide(color: c.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: inputRadius,
          borderSide: focusRing,
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: inputRadius,
          borderSide: BorderSide(color: c.error),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: inputRadius,
          borderSide: BorderSide(color: c.error, width: 2),
        ),
      ),
      listTileTheme: ListTileThemeData(
        contentPadding: const EdgeInsets.symmetric(
          horizontal: AppSpace.gutter,
          vertical: 4,
        ),
        minTileHeight: 56,
        iconColor: c.textSecondary,
        textColor: c.text,
        titleTextStyle: text.titleMedium,
        subtitleTextStyle: text.bodySmall,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
        ),
      ),
      dividerTheme: DividerThemeData(color: c.border, thickness: 1, space: 1),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: c.primary,
        linearTrackColor: c.isDark ? c.surfaceOverlay : c.border,
        circularTrackColor: Colors.transparent,
      ),
      sliderTheme: SliderThemeData(
        activeTrackColor: c.primary,
        inactiveTrackColor: c.surfaceOverlay,
        thumbColor: c.primary,
      ),
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? c.onPrimary
              : c.textSecondary,
        ),
        trackColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? c.primary
              : c.surfaceOverlay,
        ),
        trackOutlineColor: WidgetStatePropertyAll(c.border),
      ),
      radioTheme: RadioThemeData(
        fillColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? c.primary
              : c.textSecondary,
        ),
      ),
      checkboxTheme: CheckboxThemeData(
        fillColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? c.primary
              : Colors.transparent,
        ),
        checkColor: WidgetStatePropertyAll(c.onPrimary),
        side: BorderSide(color: c.textSecondary, width: 2),
      ),
      badgeTheme: BadgeThemeData(
        backgroundColor: c.accent,
        textColor: Colors.white,
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(
          color: c.surfaceOverlay,
          borderRadius: BorderRadius.circular(AppRadii.sm),
          border: Border.all(color: c.border),
        ),
        textStyle: text.bodySmall!.copyWith(color: c.text),
      ),
    );
  }
}
