import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/features/settings/settings_page.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';
import 'package:sine_uyum_mobile/providers/language_sync_provider.dart';
import 'package:sine_uyum_mobile/providers/locale_provider.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/fake_network.dart';
import 'support/localized_app.dart';

class SignedOut extends SessionNotifier {
  @override
  SessionState build() => const SessionState.signedOut();
}

void main() {
  late FakeServer server;
  late MemoryTokens tokens;
  late MemoryPreferences preferences;

  setUp(() {
    server = FakeServer();
    tokens = MemoryTokens(access: 'access', refresh: 'refresh');
    preferences = MemoryPreferences();
  });

  tearDown(() => L10n.locale = en);

  ProviderContainer make({
    List<Locale> device = const [en],
    String? saved,
    List<Override> overrides = const [],
  }) {
    final container = ProviderContainer(
      overrides: [
        httpAdapterProvider.overrideWithValue(server),
        tokenStorageProvider.overrideWithValue(tokens),
        cacheStoreProvider.overrideWithValue(MemoryCacheStore()),
        connectivitySourceProvider.overrideWithValue(FakeConnectivity()),
        preferencesStorageProvider.overrideWithValue(preferences),
        savedLanguageProvider.overrideWithValue(saved),
        deviceLocalesProvider.overrideWithValue(device),
        ...overrides,
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  Future<void> pump([int rounds = 20]) async {
    for (var i = 0; i < rounds; i++) {
      await Future<void>.delayed(const Duration(milliseconds: 5));
    }
  }

  String? acceptLanguage(int index) =>
      server.headers[index]['Accept-Language'] as String?;

  group('resolving the locale', () {
    test('"system" uses the device language when it is supported', () {
      final container = make(device: const [Locale('tr', 'TR')]);
      expect(container.read(localeProvider), isNull);
      expect(container.read(appLocaleProvider), tr);
    });

    test('"system" falls back to English for an unsupported device '
        'language', () {
      expect(make(device: const [Locale('de')]).read(appLocaleProvider), en);
    });

    test('"system" takes the first supported device language', () {
      final container = make(device: const [Locale('de'), Locale('tr')]);
      expect(container.read(appLocaleProvider), tr);
    });

    test('an explicit choice wins over the device language', () {
      final container = make(device: const [en], saved: 'tr');
      expect(container.read(localeProvider), tr);
      expect(container.read(appLocaleProvider), tr);
    });

    test('an unknown saved code is treated as "system"', () {
      expect(make(saved: 'xx').read(localeProvider), isNull);
    });

    test('context-free strings follow the resolved locale', () {
      final container = make(device: const [Locale('tr')]);
      container.read(appLocaleProvider);
      expect(L10n.current.localeName, 'tr');
      expect(ApiException(null).message, 'Beklenmeyen bir hata oluştu.');
    });
  });

  group('persistence', () {
    test('the choice is saved and restored on the next start', () async {
      final container = make();
      await container.read(localeProvider.notifier).set(tr);
      expect(preferences.language, 'tr');
      expect(container.read(appLocaleProvider), tr);

      // Next start: main() reads the saved code before the first frame.
      final restarted = make(saved: await preferences.readLanguage());
      expect(restarted.read(appLocaleProvider), tr);
    });

    test('"system" clears the saved choice', () async {
      preferences.language = 'tr';
      final container = make(saved: 'tr');
      await container.read(localeProvider.notifier).set(null);
      expect(preferences.language, isNull);
      expect(container.read(appLocaleProvider), en);
    });
  });

  group('API language', () {
    test('every request carries Accept-Language, and it follows a language '
        'change', () async {
      server.routes['GET /api/v1/movies/popular'] = (200, {'results': []});
      final container = make();
      final api = container.read(apiClientProvider);

      await api.get<Map<String, dynamic>>('/api/v1/movies/popular');
      expect(acceptLanguage(0), 'en');

      await container.read(localeProvider.notifier).set(tr);
      await api.get<Map<String, dynamic>>('/api/v1/movies/popular');
      expect(acceptLanguage(1), 'tr');
    });

    test('auth requests carry it too (the server localizes their '
        'errors)', () async {
      final container = make(saved: 'tr');
      server.routes['POST /api/v1/account/login'] = (
        401,
        {'title': 'Invalid credentials.'},
      );
      await expectLater(
        container
            .read(authRepositoryProvider)
            .login(username: 'a', password: 'b'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'Invalid credentials.',
          ),
        ),
      );
      expect(server.requests.single, 'POST /api/v1/account/login');
      expect(acceptLanguage(0), 'tr');
    });

    test('cache keys include the language', () async {
      var language = 'en';
      final cache = ResponseCache(MemoryCacheStore(), language: () => language);
      await cache.useScope('u1');

      final englishKey = cache.keyFor('/api/v1/feed', {'cursor': 'c1'});
      language = 'tr';
      final turkishKey = cache.keyFor('/api/v1/feed', {'cursor': 'c1'});

      expect(englishKey, 'en:/api/v1/feed?cursor=c1');
      expect(turkishKey, 'tr:/api/v1/feed?cursor=c1');
    });

    test(
      'offline, a response cached in another language is not served',
      () async {
        server.routes['GET /api/v1/account/me'] = (200, userJson('u1'));
        server.routes['GET /api/v1/movies/popular'] = (
          200,
          {
            'results': [
              {'id': 1, 'title': 'English title'},
            ],
          },
        );
        final container = make();
        container.listen(sessionProvider, (_, _) {});
        await pump(); // session restored: the cache scope is set
        final api = container.read(apiClientProvider);
        await api.get<Map<String, dynamic>>('/api/v1/movies/popular');
        await pump(3);

        server.offline = true;
        await container.read(localeProvider.notifier).set(tr);
        await expectLater(
          api.get<Map<String, dynamic>>('/api/v1/movies/popular'),
          throwsA(isA<ApiException>()),
        );

        await container.read(localeProvider.notifier).set(en);
        final cached = await api.get<Map<String, dynamic>>(
          '/api/v1/movies/popular',
        );
        expect((cached['results'] as List).single['title'], 'English title');
      },
    );
  });

  group('telling the server the language', () {
    List<Object?> languageUpdates() => [
      for (var i = 0; i < server.requests.length; i++)
        if (server.requests[i] == 'PUT /api/v1/account/language')
          server.bodies[i],
    ];

    Future<void> settle(ProviderContainer container) async {
      await pump();
      await container.read(languageSyncProvider.notifier).settled;
    }

    test('sent once a session is restored, then on every change', () async {
      server.routes['GET /api/v1/account/me'] = (200, userJson('u1'));
      server.routes['PUT /api/v1/account/language'] = (204, null);
      final container = make(device: const [Locale('tr')]);
      container.listen(languageSyncProvider, (_, _) {});
      await settle(container);

      expect(languageUpdates(), [
        {'language': 'tr'},
      ]);

      await container.read(localeProvider.notifier).set(en);
      await settle(container);
      expect(languageUpdates().last, {'language': 'en'});
      expect(languageUpdates(), hasLength(2));
    });

    test('not sent while signed out', () async {
      final container = make(
        overrides: [sessionProvider.overrideWith(SignedOut.new)],
      );
      container.listen(languageSyncProvider, (_, _) {});
      await container.read(localeProvider.notifier).set(tr);
      await settle(container);
      expect(languageUpdates(), isEmpty);
    });

    test('a failed update is ignored', () async {
      server.routes['GET /api/v1/account/me'] = (200, userJson('u1'));
      server.routes['PUT /api/v1/account/language'] = (500, null);
      final container = make();
      container.listen(languageSyncProvider, (_, _) {});
      await settle(container);

      expect(languageUpdates(), hasLength(1));
      expect(container.read(sessionProvider).isAuthenticated, isTrue);
    });
  });

  testWidgets('the settings page switches the app language', (tester) async {
    final container = make(
      overrides: [sessionProvider.overrideWith(SignedOut.new)],
    );
    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: Consumer(
          builder: (context, ref, _) => MaterialApp(
            locale: ref.watch(appLocaleProvider),
            supportedLocales: AppLocalizations.supportedLocales,
            localizationsDelegates: AppLocalizations.localizationsDelegates,
            home: const SettingsPage(),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Settings'), findsOneWidget);

    Finder languageOption(String label) =>
        find.widgetWithText(RadioListTile<String>, label);

    await tester.scrollUntilVisible(languageOption('Türkçe'), 100);
    await tester.ensureVisible(languageOption('Türkçe'));
    await tester.pumpAndSettle();
    expect(languageOption('English'), findsOneWidget);
    await tester.tap(languageOption('Türkçe'));
    await tester.pumpAndSettle();

    expect(find.text('Ayarlar'), findsOneWidget);
    expect(preferences.language, 'tr');

    await tester.ensureVisible(languageOption('Sistem ayarı'));
    await tester.pumpAndSettle();
    await tester.tap(languageOption('Sistem ayarı'));
    await tester.pumpAndSettle();
    expect(find.text('Settings'), findsOneWidget);
    expect(preferences.language, isNull);
  });
}
