import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/ui/rating_input.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/features/onboarding/welcome_page.dart';

import 'support/app_harness.dart';
import 'support/fake_network.dart';
import 'support/localized_app.dart';

Map<String, Object?> newUser({bool completed = false}) => {
  ...userJson('u1'),
  'userName': 'ayse',
  'onboardingCompleted': completed,
};

AppHarness welcomeHarness() {
  final app = AppHarness(user: newUser());
  app.server.routes['GET /api/v1/movies/top_rated'] = (
    200,
    {
      'results': [
        for (final (id, title) in [
          (238, 'The Godfather'),
          (278, 'The Shawshank Redemption'),
          (424, "Schindler's List"),
          (129, 'Spirited Away'),
          (496243, 'Parasite'),
          (155, 'The Dark Knight'),
        ])
          movieJson(id, title),
      ],
    },
  );
  app.server.routes['POST /api/v1/ratings'] = (200, {'message': 'ok'});
  app.server.routes['POST /api/v1/account/onboarding/complete'] = (
    200,
    {'onboardingCompleted': true},
  );
  return app;
}

/// After the POST, /me says the welcome is done.
void serverCompletes(AppHarness app) {
  app.server.routes['GET /api/v1/account/me'] = (200, newUser(completed: true));
}

void main() {
  setUpAll(initializeAllDateFormatting);

  group('user model', () {
    test('onboardingCompleted is read, and missing means done', () {
      expect(AppUser.fromJson(newUser()).onboardingCompleted, isFalse);
      expect(AppUser.fromJson(userJson('u1')).onboardingCompleted, isTrue);
      final roundTrip = AppUser.fromJson(AppUser.fromJson(newUser()).toJson());
      expect(roundTrip.onboardingCompleted, isFalse);
      expect(
        AppUser.fromJson(newUser())
            .copyWith(onboardingCompleted: true)
            .onboardingCompleted,
        isTrue,
      );
    });
  });

  group('first-run welcome', () {
    testWidgets('signed in with onboardingCompleted false → /welcome (tr)', (
      tester,
    ) async {
      final app = welcomeHarness();
      await app.pump(tester);

      expect(app.router.state.matchedLocation, '/welcome');
      expect(find.byType(WelcomePage), findsOneWidget);
      expect(find.text('Senin gibi izleyen insanları bul'), findsOneWidget);
      expect(find.text('Hadi başlayalım'), findsOneWidget);

      // Other screens are not reachable before it is done.
      app.router.go('/home');
      await AppHarness.settle(tester);
      expect(app.router.state.matchedLocation, '/welcome');
    });

    testWidgets('never shown once completed (or by an older server)', (
      tester,
    ) async {
      final app = AppHarness(user: newUser(completed: true));
      await app.pump(tester);
      expect(app.router.state.matchedLocation, '/home');
      app.router.go('/welcome');
      await AppHarness.settle(tester);
      expect(app.router.state.matchedLocation, '/home');

      // A server without the field: no welcome either.
      final old = AppHarness(user: userJson('u2'));
      await old.pump(tester);
      expect(old.router.state.matchedLocation, '/home');
    });

    testWidgets('the language can be chosen on the first step', (tester) async {
      final app = welcomeHarness();
      await app.pump(tester);
      await tester.tap(find.text('English'));
      await AppHarness.settle(tester);
      expect(
        find.text('Find the people who watch like you do'),
        findsOneWidget,
      );
      expect(app.preferences.language, 'en');
    });

    testWidgets('rating five films unlocks Continue, then people and Home '
        '(en)', (tester) async {
      final app = welcomeHarness();
      await app.pump(tester, locale: en);

      await tester.tap(find.text("Let's start"));
      await AppHarness.settle(tester);
      expect(find.text('Rate a few films'), findsOneWidget);
      expect(find.text('0 / 5'), findsOneWidget);
      // Top rated and popular films, without repeats.
      expect(app.server.requests, contains('GET /api/v1/movies/top_rated'));
      expect(app.server.requests, contains('GET /api/v1/movies/popular'));
      final topRated = app.server.requests.indexOf(
        'GET /api/v1/movies/top_rated',
      );
      expect(app.server.queries[topRated], {'page': 1});

      FilledButton continueButton() => tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Continue'),
      );

      final titles = [
        'The Godfather',
        'The Shawshank Redemption',
        "Schindler's List",
        'Spirited Away',
        'Parasite',
      ];
      for (final (i, title) in titles.indexed) {
        expect(continueButton().onPressed, isNull, reason: 'after $i');
        await tester.tap(find.text(title));
        await AppHarness.settle(tester, 6);
        // RatingInput in the sheet: the 4th star's right half = 8.
        final stars = tester.getTopLeft(find.byType(RatingStars).last);
        await tester.tapAt(stars + const Offset(40 * 3 + 30, 20));
        await tester.pump();
        await tester.tap(find.text('Save rating'));
        await AppHarness.settle(tester, 6);
        expect(find.text('${i + 1} / 5'), findsOneWidget);
      }
      expect(continueButton().onPressed, isNotNull);

      final ratingBodies = [
        for (final (i, r) in app.server.requests.indexed)
          if (r == 'POST /api/v1/ratings') app.server.bodies[i],
      ];
      expect(ratingBodies, hasLength(5));
      expect(ratingBodies.first, {'movieId': 238, 'rating': 8});

      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await AppHarness.settle(tester);
      expect(find.text('People like you'), findsOneWidget);
      expect(find.text('zeynep'), findsOneWidget);
      expect(find.text('92% compatible · 4 films in common'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await AppHarness.settle(tester);
      expect(find.text("You're all set"), findsOneWidget);
      expect(
        app.server.requests,
        isNot(contains('POST /api/v1/account/onboarding/complete')),
      );

      serverCompletes(app);
      await tester.tap(find.text('Go to Home'));
      await AppHarness.settle(tester);
      expect(
        app.server.requests,
        contains('POST /api/v1/account/onboarding/complete'),
      );
      expect(app.router.state.matchedLocation, '/home');
    });

    testWidgets('Skip completes the welcome and goes Home (tr)', (
      tester,
    ) async {
      final app = welcomeHarness();
      await app.pump(tester);
      await tester.tap(find.text('Hadi başlayalım'));
      await AppHarness.settle(tester);

      serverCompletes(app);
      await tester.tap(find.text('Geç'));
      await AppHarness.settle(tester);
      expect(
        app.server.requests.where(
          (r) => r == 'POST /api/v1/account/onboarding/complete',
        ),
        hasLength(1),
      );
      expect(app.router.state.matchedLocation, '/home');

      // It doesn't come back.
      app.router.go('/welcome');
      await AppHarness.settle(tester);
      expect(app.router.state.matchedLocation, '/home');
    });

    testWidgets('an unreachable server never traps the user', (tester) async {
      final app = welcomeHarness();
      app.server.routes.remove('POST /api/v1/account/onboarding/complete');
      await app.pump(tester, locale: en);
      await tester.tap(find.text('Skip'));
      await AppHarness.settle(tester);
      expect(app.router.state.matchedLocation, '/home');
    });
  });
}
