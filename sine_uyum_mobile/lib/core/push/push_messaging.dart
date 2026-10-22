import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

import '../config/app_config.dart';

/// Data delivered when a push notification is tapped (the server's `data`
/// field).
class PushOpened {
  const PushOpened({this.relatedUrl, this.notificationId});

  factory PushOpened.fromData(Map<String, dynamic> data) => PushOpened(
    relatedUrl: data['relatedUrl'] as String?,
    notificationId: int.tryParse('${data['notificationId'] ?? ''}'),
  );

  final String? relatedUrl;
  final int? notificationId;
}

/// A thin layer over Firebase Messaging. The notification logic talks to this
/// interface, so unit tests (which have no Firebase) can use a fake.
abstract class PushMessaging {
  /// Initializes Firebase. Returns `false` (push stays off) when it is not
  /// configured or the platform is unsupported.
  Future<bool> initialize();

  /// Asks for notification permission (Android 13+, iOS). `true` if granted.
  Future<bool> requestPermission();

  Future<String?> getToken();

  Stream<String> get onTokenRefresh;

  /// The notification tapped while the app was closed (cold start), or `null`.
  Future<PushOpened?> initialOpened();

  /// Notifications tapped while the app was in the background.
  Stream<PushOpened> get onOpened;

  /// Platform name reported to the server.
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
      // A bad configuration must not crash the app; push stays off.
      debugPrint('Firebase could not be initialized, push disabled: $error');
      return _initialized = false;
    }
  }

  @override
  Future<bool> requestPermission() async {
    final settings = await FirebaseMessaging.instance.requestPermission();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  // firebase_messaging 17 introduced a Firebase Installation ID (FID) based
  // register()/onRegistered API in place of the registration token and
  // marked these as deprecated. The FID flow is very new (it needs an extra
  // manifest flag on Android) and won't be adopted before it is verified on
  // a real device. The token API is still supported; the FCM v1
  // `message.token` the server uses accepts both tokens and FIDs during the
  // transition, so switching later is a client-only change (see roadmap B7).
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
