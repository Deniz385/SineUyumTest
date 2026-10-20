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
}
