import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/core/router/app_router.dart';
import 'package:sine_uyum_mobile/core/theme/app_theme.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';
import 'package:sine_uyum_mobile/providers/locale_provider.dart';
import 'package:sine_uyum_mobile/providers/notification_provider.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';
import 'package:sine_uyum_mobile/providers/theme_provider.dart';

import 'fake_network.dart';

/// No SignalR in tests: an empty, idle notification list.
class QuietNotifications extends NotificationNotifier {
  @override
  NotificationState build() => const NotificationState();
}

Map<String, Object?> movieJson(int id, String title, {String? poster}) => {
  'id': id,
  'title': title,
  'poster_path': poster,
  'release_date': '1999-10-15',
  'vote_average': 8.4,
};

/// The real router, session and repositories over a [FakeServer]: the
/// session is restored from a stored token with `GET /account/me`.
class AppHarness {
  AppHarness({Map<String, Object?>? user}) {
    server.routes['GET /api/v1/account/me'] = (
      200,
      user ?? {...userJson('u1'), 'userName': 'ayse'},
    );
    server.routes['GET /api/v1/feed'] = (200, {'items': <Object>[]});
    server.routes['GET /api/v1/taste-twin'] = (204, null);
    final movies = {
      'results': [
        movieJson(550, 'Fight Club'),
        movieJson(13, 'Forrest Gump'),
        movieJson(680, 'Pulp Fiction'),
      ],
    };
    for (final list in ['popular', 'now_playing', 'top_rated']) {
      server.routes['GET /api/v1/movies/$list'] = (200, movies);
    }
    server.routes['GET /api/v1/people/suggestions'] = (
      200,
      {
        'basis': 'taste',
        'items': [
          {
            'user': {'id': 'u7', 'userName': 'zeynep'},
            'compatibility': 92,
            'commonMovieCount': 4,
            'followerCount': 12,
          },
        ],
      },
    );
    server.routes['GET /api/v1/watchlist'] = (200, <Object>[]);
    server.routes['GET /api/v1/notification'] = (200, <Object>[]);
  }

  final server = FakeServer();
  final tokens = MemoryTokens(access: 'access', refresh: 'refresh');
  final preferences = MemoryPreferences();
  late GoRouter router;

  /// The whole app, for [RepaintBoundary.toImage] screenshots.
  final screenKey = GlobalKey();

  Future<void> pump(
    WidgetTester tester, {
    Locale locale = const Locale('tr'),
    ThemeMode? themeMode,
    double textScale = 1,
    String? initialLocation,
  }) async {
    L10n.locale = locale;
    // A phone (390 × 844 logical pixels).
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          httpAdapterProvider.overrideWithValue(server),
          tokenStorageProvider.overrideWithValue(tokens),
          cacheStoreProvider.overrideWithValue(MemoryCacheStore()),
          connectivitySourceProvider.overrideWithValue(FakeConnectivity()),
          preferencesStorageProvider.overrideWithValue(preferences),
          savedLanguageProvider.overrideWithValue(locale.languageCode),
          savedThemeModeProvider.overrideWithValue(themeMode),
          notificationsProvider.overrideWith(QuietNotifications.new),
        ],
        child: RepaintBoundary(
          key: screenKey,
          child: Consumer(
            builder: (context, ref, _) {
              router = ref.watch(routerProvider);
              return MaterialApp.router(
                debugShowCheckedModeBanner: false,
                theme: AppTheme.light,
                darkTheme: AppTheme.dark,
                themeMode: ref.watch(themeModeProvider),
                locale: ref.watch(appLocaleProvider),
                supportedLocales: AppLocalizations.supportedLocales,
                localizationsDelegates: AppLocalizations.localizationsDelegates,
                routerConfig: router,
                builder: (context, child) => MediaQuery(
                  data: MediaQuery.of(context)
                      .copyWith(textScaler: TextScaler.linear(textScale)),
                  child: child!,
                ),
              );
            },
          ),
        ),
      ),
    );
    await settle(tester);
    if (initialLocation != null) {
      router.go(initialLocation);
      await settle(tester);
    }
  }

  /// Lets the fake server answer and the screens settle. Skeletons shimmer
  /// forever, so this pumps a fixed number of frames instead of settling.
  static Future<void> settle(WidgetTester tester, [int frames = 12]) async {
    for (var i = 0; i < frames; i++) {
      await tester.runAsync(
        () => Future<void>.delayed(const Duration(milliseconds: 5)),
      );
      await tester.pump(const Duration(milliseconds: 100));
    }
  }
}
