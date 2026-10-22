import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'l10n/l10n.dart';
import 'providers/language_sync_provider.dart';
import 'providers/locale_provider.dart';
import 'providers/notification_provider.dart';
import 'providers/push_provider.dart';
import 'providers/theme_provider.dart';
import 'widgets/offline_banner.dart';

class SineUyumApp extends ConsumerStatefulWidget {
  const SineUyumApp({super.key});

  @override
  ConsumerState<SineUyumApp> createState() => _SineUyumAppState();
}

class _SineUyumAppState extends ConsumerState<SineUyumApp>
    with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  // The OS language changed: matters when the user follows the system
  // language.
  @override
  void didChangeLocales(List<Locale>? locales) {
    ref.invalidate(deviceLocalesProvider);
  }

  @override
  Widget build(BuildContext context) {
    // The notification provider opens the SignalR connection once signed in;
    // watched at the root so it lives as long as the app.
    ref.watch(notificationsProvider);
    // Push: registers/unregisters the device with FCM based on the session.
    ref.watch(pushProvider);
    // Keeps the server informed of the UI language (notifications, push).
    ref.watch(languageSyncProvider);

    return MaterialApp.router(
      onGenerateTitle: (context) => context.l10n.appName,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ref.watch(themeModeProvider),
      locale: ref.watch(appLocaleProvider),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      routerConfig: ref.watch(routerProvider),
      builder: (context, child) =>
          OfflineBanner(child: child ?? const SizedBox.shrink()),
    );
  }
}
