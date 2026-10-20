import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

import '../config/app_config.dart';

/// Push bildirimine dokunulduğunda gelen veri (sunucudaki `data` alanı).
class PushOpened {
  const PushOpened({this.relatedUrl, this.notificationId});

  factory PushOpened.fromData(Map<String, dynamic> data) => PushOpened(
    relatedUrl: data['relatedUrl'] as String?,
    notificationId: int.tryParse('${data['notificationId'] ?? ''}'),
  );

  final String? relatedUrl;
  final int? notificationId;
}

/// Firebase Messaging üzerinde ince bir katman. Bildirim mantığı bu arayüzle
/// konuşur; böylece Firebase'in olmadığı birim testlerinde sahtesi kullanılır.
abstract class PushMessaging {
  /// Firebase'i başlatır. Yapılandırma yoksa ya da platform desteklemiyorsa
  /// `false` döner ve push kapalı kalır.
  Future<bool> initialize();

  /// Bildirim izni ister (Android 13+, iOS). Verildiyse `true`.
  Future<bool> requestPermission();

  Future<String?> getToken();

  Stream<String> get onTokenRefresh;

  /// Uygulama kapalıyken dokunulan bildirim (soğuk açılış), yoksa `null`.
  Future<PushOpened?> initialOpened();

  /// Uygulama arka plandayken dokunulan bildirimler.
  Stream<PushOpened> get onOpened;

  /// Sunucuya bildirilecek platform adı.
  String get platform;
}

class FirebasePushMessaging implements PushMessaging {
  bool? _initialized;

  static bool get isConfigured =>
      AppConfig.firebaseApiKey.isNotEmpty &&
      AppConfig.firebaseProjectId.isNotEmpty &&
      AppConfig.firebaseMessagingSenderId.isNotEmpty &&
      _appId.isNotEmpty;

  static String get _appId => defaultTargetPlatform == TargetPlatform.iOS
      ? AppConfig.firebaseIosAppId
      : AppConfig.firebaseAndroidAppId;

  @override
  String get platform =>
      defaultTargetPlatform == TargetPlatform.iOS ? 'ios' : 'android';

  @override
  Future<bool> initialize() async {
    if (_initialized != null) return _initialized!;

    final supported =
        !kIsWeb &&
        (defaultTargetPlatform == TargetPlatform.android ||
            defaultTargetPlatform == TargetPlatform.iOS);
    if (!supported || !isConfigured) return _initialized = false;

    try {
      await Firebase.initializeApp(
        options: FirebaseOptions(
          apiKey: AppConfig.firebaseApiKey,
          appId: _appId,
          messagingSenderId: AppConfig.firebaseMessagingSenderId,
          projectId: AppConfig.firebaseProjectId,
        ),
      );
      return _initialized = true;
    } catch (error) {
      // Yanlış yapılandırma uygulamayı düşürmemeli; push kapalı kalır.
      debugPrint('Firebase başlatılamadı, push kapalı: $error');
      return _initialized = false;
    }
  }

  @override
  Future<bool> requestPermission() async {
    final settings = await FirebaseMessaging.instance.requestPermission();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  // firebase_messaging 17, kayıt token'ı yerine Firebase Installation ID
  // (FID) tabanlı register()/onRegistered API'sini getirdi ve bunları
  // "deprecated" işaretledi. FID akışı çok yeni (Android'de ek manifest
  // bayrağı istiyor) ve gerçek cihazda doğrulanmadan geçilmeyecek. Token
  // API'si hâlâ destekleniyor; sunucunun kullandığı FCM v1 `message.token`
  // geçiş süresince hem token'ı hem FID'i kabul ediyor, yani ileride geçiş
  // yalnızca istemci değişikliği olacak (bkz. yol haritası B7).
  @override
  // ignore: deprecated_member_use
  Future<String?> getToken() => FirebaseMessaging.instance.getToken();

  @override
  Stream<String> get onTokenRefresh =>
      // ignore: deprecated_member_use
      FirebaseMessaging.instance.onTokenRefresh;

  @override
  Future<PushOpened?> initialOpened() async {
    final message = await FirebaseMessaging.instance.getInitialMessage();
    return message == null ? null : PushOpened.fromData(message.data);
  }

  @override
  Stream<PushOpened> get onOpened => FirebaseMessaging.onMessageOpenedApp.map(
    (message) => PushOpened.fromData(message.data),
  );
}
