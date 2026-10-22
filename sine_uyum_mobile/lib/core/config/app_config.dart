/// Build-time configuration.
///
/// The API URL is not hard-coded; it is passed at build time:
///   flutter run --dart-define=API_URL=http://10.0.2.2:5074
///
/// The host machine is `10.0.2.2` on the Android emulator and `localhost` on
/// the iOS simulator.
class AppConfig {
  const AppConfig._();

  static const String apiUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'http://10.0.2.2:5074',
  );

  /// Media uploaded to the server (e.g. profile photos) comes as a relative
  /// path (`/media/avatars/...`) and is joined with the API URL. Older records
  /// may hold a full URL, which is used as-is.
  static String? resolveMediaUrl(String? url) {
    if (url == null || url.isEmpty) return null;
    return url.startsWith('/') ? '$apiUrl$url' : url;
  }

  /// Base URL for TMDb poster/backdrop images.
  /// The server may override it via `/api/v1/app/config`.
  static const String tmdbImageBaseUrl = String.fromEnvironment(
    'TMDB_IMAGE_BASE_URL',
    defaultValue: 'https://image.tmdb.org/t/p/',
  );

  /// App version, compared with the minimum version supported by the server.
  static const String appVersion = String.fromEnvironment(
    'APP_VERSION',
    defaultValue: '1.0.0',
  );

  // --- Push notifications (Firebase Cloud Messaging) ---
  // Values come from Firebase console → Project settings → "Your apps" and
  // are passed at build time (no google-services.json / plist needed):
  //   --dart-define=FIREBASE_API_KEY=... --dart-define=FIREBASE_PROJECT_ID=...
  //   --dart-define=FIREBASE_MESSAGING_SENDER_ID=...
  //   --dart-define=FIREBASE_ANDROID_APP_ID=... --dart-define=FIREBASE_IOS_APP_ID=...
  // Without them push is off; the app still receives notifications over
  // SignalR (in the foreground).

  static const String firebaseApiKey = String.fromEnvironment(
    'FIREBASE_API_KEY',
  );
  static const String firebaseProjectId = String.fromEnvironment(
    'FIREBASE_PROJECT_ID',
  );
  static const String firebaseMessagingSenderId = String.fromEnvironment(
    'FIREBASE_MESSAGING_SENDER_ID',
  );
  static const String firebaseAndroidAppId = String.fromEnvironment(
    'FIREBASE_ANDROID_APP_ID',
  );
  static const String firebaseIosAppId = String.fromEnvironment(
    'FIREBASE_IOS_APP_ID',
  );
}
