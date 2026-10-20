import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'providers/notification_provider.dart';
import 'providers/push_provider.dart';
import 'providers/theme_provider.dart';
import 'widgets/offline_banner.dart';

class SineUyumApp extends ConsumerWidget {
  const SineUyumApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Bildirim sağlayıcısı oturum açıldığında SignalR bağlantısını kurar;
    // kök widget'ta izlenerek uygulama ömrü boyunca canlı tutulur.
    ref.watch(notificationsProvider);
    // Push: oturuma göre cihazı FCM için kaydeder/kaydını siler.
    ref.watch(pushProvider);

    return MaterialApp.router(
      title: 'SineUyum',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ref.watch(themeModeProvider),
      routerConfig: ref.watch(routerProvider),
      builder: (context, child) =>
          OfflineBanner(child: child ?? const SizedBox.shrink()),
    );
  }
}
