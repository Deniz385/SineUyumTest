import 'package:flutter/widgets.dart';
import 'package:intl/intl.dart';

import 'app_localizations.dart';

export 'app_localizations.dart';

/// Shorthand for `AppLocalizations.of(context)`.
extension L10nContext on BuildContext {
  AppLocalizations get l10n => AppLocalizations.of(this);
}

/// Locale-aware number formatting shared by the screens.
extension L10nNumbers on AppLocalizations {
  /// `7.4` → "7.4" (en) / "7,4" (tr).
  String decimal(num value, {int digits = 1}) =>
      NumberFormat.decimalPatternDigits(
        locale: localeName,
        decimalDigits: digits,
      ).format(value);

  /// Capitals in the UI language: Dart's `toUpperCase` ignores the locale,
  /// so Turkish "i" would become "I" instead of "İ" (overline labels).
  String upper(String text) {
    final language = localeName.split(RegExp('[-_]')).first;
    if (language == 'tr' || language == 'az') {
      return text.replaceAll('i', 'İ').replaceAll('ı', 'I').toUpperCase();
    }
    return text.toUpperCase();
  }

  /// [value] is on a 0-100 scale: `82` → "82%" (en) / "%82" (tr).
  String percent(num value, {int digits = 0}) {
    final format = NumberFormat.percentPattern(localeName)
      ..minimumFractionDigits = digits
      ..maximumFractionDigits = digits;
    return format.format(value / 100);
  }
}

/// Strings for code that runs outside the widget tree and has no
/// [BuildContext] (e.g. `ApiException` fallback messages, model defaults).
///
/// The app keeps [locale] in sync with the locale it renders in
/// (see `appLocaleProvider`); until then English is used.
abstract final class L10n {
  static const fallbackLocale = Locale('en');

  static Locale _locale = fallbackLocale;
  static AppLocalizations _current = lookupAppLocalizations(fallbackLocale);

  static Locale get locale => _locale;

  /// Strings in the current app language.
  static AppLocalizations get current => _current;

  static set locale(Locale value) {
    if (value == _locale) return;
    _locale = value;
    _current = lookupAppLocalizations(value);
    // Formatters created without an explicit locale follow the app too.
    Intl.defaultLocale = value.toLanguageTag();
  }

  /// Supported locale matching [code] (e.g. "tr"), or null.
  static Locale? supported(String? code) {
    for (final locale in AppLocalizations.supportedLocales) {
      if (locale.languageCode == code) return locale;
    }
    return null;
  }

  /// First of the device's [preferred] locales the app supports; English
  /// when none matches.
  static Locale resolve(Iterable<Locale> preferred) {
    for (final locale in preferred) {
      final match = supported(locale.languageCode);
      if (match != null) return match;
    }
    return fallbackLocale;
  }
}
