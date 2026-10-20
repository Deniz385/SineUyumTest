/// Derleme zamanı yapılandırması.
///
/// API adresi koda gömülmez; build sırasında verilir:
///   flutter run --dart-define=API_URL=http://10.0.2.2:5074
///
/// Android emülatörde ana makine `10.0.2.2`, iOS simülatörde `localhost`tur.
class AppConfig {
  const AppConfig._();

  static const String apiUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'http://10.0.2.2:5074',
  );

  /// TMDb poster/backdrop adresleri için temel URL.
  /// Sunucu `/api/v1/app/config` ile bunu ezebilir.
  static const String tmdbImageBaseUrl = String.fromEnvironment(
    'TMDB_IMAGE_BASE_URL',
    defaultValue: 'https://image.tmdb.org/t/p/',
  );

  /// Uygulama sürümü — sunucudaki minimum desteklenen sürümle karşılaştırılır.
  static const String appVersion = String.fromEnvironment(
    'APP_VERSION',
    defaultValue: '1.0.0',
  );

  // --- Push bildirimi (Firebase Cloud Messaging) ---
  // Değerler Firebase konsolu → Proje ayarları → "Uygulamalarınız"dan alınır
  // ve build sırasında verilir (google-services.json / plist gerekmez):
  //   --dart-define=FIREBASE_API_KEY=... --dart-define=FIREBASE_PROJECT_ID=...
  //   --dart-define=FIREBASE_MESSAGING_SENDER_ID=...
  //   --dart-define=FIREBASE_ANDROID_APP_ID=... --dart-define=FIREBASE_IOS_APP_ID=...
  // Verilmezse push kapalıdır; uygulama SignalR ile (ön planda) bildirim
  // almaya devam eder.

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
