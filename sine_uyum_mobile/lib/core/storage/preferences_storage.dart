import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Small user preferences that must survive restarts (the app language and
/// which yearly recap banner was dismissed).
///
/// Uses the same secure storage as the tokens: it is already a dependency,
/// and one short value does not justify adding shared_preferences. Unlike
/// the tokens, these values are kept on sign-out.
class PreferencesStorage {
  PreferencesStorage({FlutterSecureStorage? storage})
    : _storage =
          storage ??
          const FlutterSecureStorage(
            iOptions: IOSOptions(
              accessibility: KeychainAccessibility.first_unlock,
            ),
          );

  final FlutterSecureStorage _storage;

  static const _languageKey = 'app_language';

  /// Saved language code ("tr", "en"...), or null to follow the device.
  /// A storage failure counts as "nothing saved" and never blocks startup.
  Future<String?> readLanguage() async {
    try {
      return await _storage.read(key: _languageKey);
    } catch (_) {
      return null;
    }
  }

  /// [code] null means "follow the device". Failures are ignored: the choice
  /// still applies for this session.
  Future<void> writeLanguage(String? code) async {
    try {
      if (code == null) {
        await _storage.delete(key: _languageKey);
      } else {
        await _storage.write(key: _languageKey, value: code);
      }
    } catch (_) {}
  }

  static String _recapKey(String userId) => 'recap_banner_dismissed_$userId';

  /// The last recap year whose feed banner [userId] dismissed, or null.
  /// Only the latest matters: the banner is offered for one year at a time.
  Future<int?> readDismissedRecapYear(String userId) async {
    try {
      return int.tryParse(await _storage.read(key: _recapKey(userId)) ?? '');
    } catch (_) {
      return null;
    }
  }

  /// Failures are ignored: the banner stays hidden for this session.
  Future<void> writeDismissedRecapYear(String userId, int year) async {
    try {
      await _storage.write(key: _recapKey(userId), value: '$year');
    } catch (_) {}
  }
}
