import 'package:flutter/material.dart';

/// Design tokens from `docs/DESIGN-SYSTEM.md` (the source of truth, shared
/// with the web client). Change a value there first, then here.

/// Font families bundled as assets (see pubspec.yaml); no runtime fetching.
abstract final class AppFonts {
  /// Display type: titles, hero text and numbers.
  static const display = 'BricolageGrotesque';

  /// Text type: everything else.
  static const text = 'Inter';
}

/// The 4-point spacing scale.
abstract final class AppSpace {
  static const double xxs = 4;
  static const double xs = 8;
  static const double sm = 12;
  static const double md = 16;
  static const double lg = 20;
  static const double xl = 24;
  static const double xxl = 32;
  static const double x3 = 40;
  static const double x4 = 48;
  static const double x5 = 64;

  /// Screen edge padding on phones.
  static const double gutter = 16;
}

abstract final class AppRadii {
  /// Chips, inputs, small posters.
  static const double sm = 8;

  /// Cards and posters.
  static const double md = 12;

  /// Sheets, dialogs and hero cards.
  static const double lg = 20;

  /// Buttons and badges.
  static const double pill = 999;
}

abstract final class AppMotion {
  static const fast = Duration(milliseconds: 120);
  static const standard = Duration(milliseconds: 200);
  static const emphasized = Duration(milliseconds: 320);

  static const Curve standardCurve = Cubic(0.2, 0, 0, 1);
  static const Curve enterCurve = Cubic(0.05, 0.7, 0.1, 1);

  /// [duration], or zero when the OS asks for reduced motion.
  static Duration of(BuildContext context, Duration duration) =>
      MediaQuery.maybeDisableAnimationsOf(context) == true
      ? Duration.zero
      : duration;

  static bool reduced(BuildContext context) =>
      MediaQuery.maybeDisableAnimationsOf(context) == true;
}

abstract final class AppGradients {
  /// Hero titles, year in review, onboarding: #FFD54F → #FF8A65 → #FF5A5F
  /// at 120°.
  static const marquee = LinearGradient(
    begin: Alignment(-0.87, -0.5),
    end: Alignment(0.87, 0.5),
    colors: [Color(0xFFFFD54F), Color(0xFFFF8A65), Color(0xFFFF5A5F)],
  );
}

/// The colour tokens of one theme, reachable from any widget with
/// `context.colors`.
@immutable
class AppColors extends ThemeExtension<AppColors> {
  const AppColors({
    required this.brightness,
    required this.bg,
    required this.surface,
    required this.surfaceRaised,
    required this.surfaceOverlay,
    required this.border,
    required this.text,
    required this.textSecondary,
    required this.textMuted,
    required this.primary,
    required this.onPrimary,
    required this.accent,
    required this.accentStrong,
    required this.success,
    required this.error,
    required this.info,
    required this.posterHairline,
  });

  final Brightness brightness;
  final Color bg;
  final Color surface;
  final Color surfaceRaised;
  final Color surfaceOverlay;
  final Color border;
  final Color text;
  final Color textSecondary;

  /// Captions; only on [bg], [surface] (and [surfaceRaised] on dark).
  final Color textMuted;
  final Color primary;
  final Color onPrimary;
  final Color accent;
  final Color accentStrong;
  final Color success;
  final Color error;
  final Color info;

  /// The 1px inner line around posters.
  final Color posterHairline;

  bool get isDark => brightness == Brightness.dark;

  static const dark = AppColors(
    brightness: Brightness.dark,
    bg: Color(0xFF0B0C10),
    surface: Color(0xFF14161C),
    surfaceRaised: Color(0xFF1C1F27),
    surfaceOverlay: Color(0xFF262A34),
    border: Color(0xFF2E3340),
    text: Color(0xFFF2F2F5),
    textSecondary: Color(0xFFA9ADBA),
    textMuted: Color(0xFF868B99),
    primary: Color(0xFFF5B841),
    onPrimary: Color(0xFF1A1300),
    accent: Color(0xFFFF5A5F),
    accentStrong: Color(0xFFD93A40),
    success: Color(0xFF3DD68C),
    error: Color(0xFFFF6B70),
    info: Color(0xFF6CB4FF),
    posterHairline: Color(0x0FFFFFFF),
  );

  static const light = AppColors(
    brightness: Brightness.light,
    bg: Color(0xFFF7F6F3),
    surface: Color(0xFFFFFFFF),
    surfaceRaised: Color(0xFFF0EEE9),
    surfaceOverlay: Color(0xFFFFFFFF),
    border: Color(0xFFE4E1DA),
    text: Color(0xFF16171B),
    textSecondary: Color(0xFF545866),
    textMuted: Color(0xFF6B6F7B),
    primary: Color(0xFF9A6200),
    onPrimary: Color(0xFFFFFFFF),
    accent: Color(0xFFC9252C),
    // Not in the light table: the accent itself carries white text at AA.
    accentStrong: Color(0xFFC9252C),
    success: Color(0xFF137A4B),
    error: Color(0xFFC42B31),
    info: Color(0xFF1F63B8),
    posterHairline: Color(0x14000000),
  );

  /// Soft shadow used for elevation on the light theme (dark uses lighter
  /// surfaces instead).
  List<BoxShadow> get softShadow => isDark
      ? const []
      : const [
          BoxShadow(
            color: Color(0x0F000000),
            blurRadius: 2,
            offset: Offset(0, 1),
          ),
          BoxShadow(
            color: Color(0x0F000000),
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ];

  /// The compatibility scale: < 50 accent, 50–74 primary, ≥ 75 success.
  Color compatibility(double score) {
    if (score >= 75) return success;
    if (score >= 50) return primary;
    return accent;
  }

  @override
  AppColors copyWith({Color? primary, Color? bg, Color? surface}) => AppColors(
    brightness: brightness,
    bg: bg ?? this.bg,
    surface: surface ?? this.surface,
    surfaceRaised: surfaceRaised,
    surfaceOverlay: surfaceOverlay,
    border: border,
    text: text,
    textSecondary: textSecondary,
    textMuted: textMuted,
    primary: primary ?? this.primary,
    onPrimary: onPrimary,
    accent: accent,
    accentStrong: accentStrong,
    success: success,
    error: error,
    info: info,
    posterHairline: posterHairline,
  );

  @override
  AppColors lerp(AppColors? other, double t) {
    if (other == null) return this;
    Color c(Color a, Color b) => Color.lerp(a, b, t)!;
    return AppColors(
      brightness: t < 0.5 ? brightness : other.brightness,
      bg: c(bg, other.bg),
      surface: c(surface, other.surface),
      surfaceRaised: c(surfaceRaised, other.surfaceRaised),
      surfaceOverlay: c(surfaceOverlay, other.surfaceOverlay),
      border: c(border, other.border),
      text: c(text, other.text),
      textSecondary: c(textSecondary, other.textSecondary),
      textMuted: c(textMuted, other.textMuted),
      primary: c(primary, other.primary),
      onPrimary: c(onPrimary, other.onPrimary),
      accent: c(accent, other.accent),
      accentStrong: c(accentStrong, other.accentStrong),
      success: c(success, other.success),
      error: c(error, other.error),
      info: c(info, other.info),
      posterHairline: c(posterHairline, other.posterHairline),
    );
  }
}

/// Text roles from the spec that Material's [TextTheme] has no slot for.
@immutable
class AppTextStyles extends ThemeExtension<AppTextStyles> {
  const AppTextStyles({
    required this.display,
    required this.h1,
    required this.h2,
    required this.h3,
    required this.title,
    required this.body,
    required this.bodySmall,
    required this.caption,
    required this.overline,
    required this.number,
  });

  final TextStyle display;
  final TextStyle h1;
  final TextStyle h2;
  final TextStyle h3;
  final TextStyle title;
  final TextStyle body;
  final TextStyle bodySmall;
  final TextStyle caption;

  /// Caption in capitals; use `l10n.upper` (not `toUpperCase`) so the UI
  /// language applies (Turkish "i" → "İ").
  final TextStyle overline;

  /// Scores and counts: display font with tabular figures. Set the size at
  /// the call site.
  final TextStyle number;

  static AppTextStyles of(AppColors colors) {
    TextStyle style(
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
      color: color ?? colors.text,
    );

    return AppTextStyles(
      display: style(AppFonts.display, 40, 44, FontWeight.w800),
      h1: style(AppFonts.display, 30, 36, FontWeight.w700),
      h2: style(AppFonts.display, 22, 28, FontWeight.w700),
      h3: style(AppFonts.text, 18, 24, FontWeight.w600),
      title: style(AppFonts.text, 16, 22, FontWeight.w600),
      body: style(AppFonts.text, 15, 22, FontWeight.w400),
      bodySmall: style(
        AppFonts.text,
        13,
        18,
        FontWeight.w400,
        color: colors.textSecondary,
      ),
      caption: style(
        AppFonts.text,
        12,
        16,
        FontWeight.w600,
        color: colors.textSecondary,
        letterSpacing: 0.48,
      ),
      overline: style(
        AppFonts.text,
        12,
        16,
        FontWeight.w600,
        color: colors.textSecondary,
        letterSpacing: 0.48,
      ),
      number: TextStyle(
        fontFamily: AppFonts.display,
        fontWeight: FontWeight.w700,
        color: colors.text,
        fontFeatures: const [FontFeature.tabularFigures()],
      ),
    );
  }

  @override
  AppTextStyles copyWith() => this;

  @override
  AppTextStyles lerp(AppTextStyles? other, double t) {
    if (other == null) return this;
    TextStyle l(TextStyle a, TextStyle b) => TextStyle.lerp(a, b, t)!;
    return AppTextStyles(
      display: l(display, other.display),
      h1: l(h1, other.h1),
      h2: l(h2, other.h2),
      h3: l(h3, other.h3),
      title: l(title, other.title),
      body: l(body, other.body),
      bodySmall: l(bodySmall, other.bodySmall),
      caption: l(caption, other.caption),
      overline: l(overline, other.overline),
      number: l(number, other.number),
    );
  }
}

extension AppThemeContext on BuildContext {
  /// The colour tokens of the current theme.
  AppColors get colors =>
      Theme.of(this).extension<AppColors>() ??
      (Theme.of(this).brightness == Brightness.dark
          ? AppColors.dark
          : AppColors.light);

  /// The spec's text roles for the current theme.
  AppTextStyles get type =>
      Theme.of(this).extension<AppTextStyles>() ?? AppTextStyles.of(colors);
}
