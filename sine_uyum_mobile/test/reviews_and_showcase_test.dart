import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/data/models/social.dart';
import 'package:sine_uyum_mobile/features/auth/auth_backdrop.dart';
import 'package:sine_uyum_mobile/features/auth/login_page.dart';
import 'package:sine_uyum_mobile/features/auth/register_page.dart';
import 'package:sine_uyum_mobile/features/profile/profile_page.dart';

import 'support/app_harness.dart';
import 'support/localized_app.dart';

Map<String, Object?> reviewJson(int id, String text, {String user = 'u7'}) => {
  'id': id,
  'movie': {'id': 100 + id, 'title': 'Movie $id'},
  'author': {'id': user, 'userName': 'zeynep'},
  'rating': 8,
  'text': text,
  'createdAt': '2026-10-${(id % 27) + 1}T12:00:00Z',
  'likeCount': 2,
  'commentCount': 0,
};

void profileOf(AppHarness app, String id) {
  app.server.routes['GET /api/v1/profile/$id'] = (
    200,
    {
      'id': id,
      'userName': id == 'u1' ? 'ayse' : 'zeynep',
      'statistics': {'totalRatings': 0},
    },
  );
  app.server.routes['GET /api/v1/follow/$id/status'] = (
    200,
    {'isFollowing': false, 'followerCount': 3, 'followingCount': 4},
  );
}

/// The profile's own scroll view (the shell keeps other tabs' lists alive).
Finder profileScrollable() => find
    .descendant(of: find.byType(ProfilePage), matching: find.byType(Scrollable))
    .first;

Future<void> openReviewsTab(WidgetTester tester, String label) async {
  final tab = find.widgetWithText(Tab, label);
  await tester.scrollUntilVisible(
    tab.hitTestable(),
    150,
    scrollable: profileScrollable(),
  );
  await tester.tap(tab);
  await AppHarness.settle(tester);
}

/// Wall tiles that carry a poster from the showcase.
Finder posterTiles() => find.byWidgetPredicate(
  (w) =>
      w.key is ValueKey<String> &&
      (w.key! as ValueKey<String>).value.startsWith('wall-'),
);

void main() {
  setUpAll(initializeAllDateFormatting);

  group('a user\'s reviews', () {
    test('a page is parsed; missing fields have defaults', () {
      final page = ReviewPage.fromJson({
        'items': [reviewJson(1, 'Great.')],
        'page': 2,
        'hasMore': true,
      });
      expect(page.items.single.text, 'Great.');
      expect(page.items.single.movie.title, 'Movie 1');
      expect(page.page, 2);
      expect(page.hasMore, isTrue);
      expect(ReviewPage.fromJson({}).items, isEmpty);
      expect(ReviewPage.fromJson({}).hasMore, isFalse);
    });

    testWidgets('the Reviews tab pages newest first, without repeats (tr)', (
      tester,
    ) async {
      final app = AppHarness();
      profileOf(app, 'u7');
      app.server.handlers['GET /api/v1/reviews/user/u7'] = (query) =>
          query['page'] == 1
          ? (
              200,
              {
                'items': [reviewJson(2, 'Yeni olan'), reviewJson(1, 'Eski')],
                'page': 1,
                'hasMore': true,
              },
            )
          : (
              200,
              {
                // A repeat (a review was written meanwhile) and an older one.
                'items': [reviewJson(1, 'Eski'), reviewJson(0, 'En eski')],
                'page': 2,
                'hasMore': false,
              },
            );
      await app.pump(tester, initialLocation: '/profile/u7');
      await openReviewsTab(tester, 'İncelemeler');

      final calls = [
        for (final (i, r) in app.server.requests.indexed)
          if (r == 'GET /api/v1/reviews/user/u7') app.server.queries[i],
      ];
      expect(calls, [
        {'page': 1, 'pageSize': 20},
        {'page': 2, 'pageSize': 20},
      ]);
      expect(find.text('Yeni olan'), findsOneWidget);
      expect(find.text('Eski'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('En eski'),
        200,
        scrollable: profileScrollable(),
      );
      expect(find.text('En eski'), findsOneWidget);
      // Newest first.
      expect(
        tester.getTopLeft(find.text('Yeni olan')).dy,
        lessThan(tester.getTopLeft(find.text('Eski')).dy),
      );
    });

    testWidgets('a failed next page offers a retry (en)', (tester) async {
      final app = AppHarness();
      profileOf(app, 'u7');
      var failNext = true;
      app.server.handlers['GET /api/v1/reviews/user/u7'] = (query) {
        if (query['page'] == 1) {
          return (
            200,
            {
              'items': [reviewJson(1, 'First page')],
              'page': 1,
              'hasMore': true,
            },
          );
        }
        if (failNext) return (500, {'title': 'Server trouble'});
        return (
          200,
          {
            'items': [reviewJson(0, 'Second page')],
            'page': 2,
            'hasMore': false,
          },
        );
      };
      await app.pump(tester, locale: en, initialLocation: '/profile/u7');
      await openReviewsTab(tester, 'Reviews');
      expect(find.text('First page'), findsOneWidget);
      expect(find.text('Second page'), findsNothing);

      failNext = false;
      await tester.scrollUntilVisible(
        find.text('Try again'),
        200,
        scrollable: profileScrollable(),
      );
      await tester.tap(find.text('Try again'));
      await AppHarness.settle(tester);
      expect(find.text('Second page'), findsOneWidget);
      expect(find.text('Try again'), findsNothing);
    });

    testWidgets('a private profile shows the server message (en)', (
      tester,
    ) async {
      final app = AppHarness();
      profileOf(app, 'u7');
      app.server.routes['GET /api/v1/reviews/user/u7'] = (
        403,
        {'title': 'zeynep keeps their reviews private.'},
      );
      await app.pump(tester, locale: en, initialLocation: '/profile/u7');
      await openReviewsTab(tester, 'Reviews');
      expect(find.text('zeynep keeps their reviews private.'), findsOneWidget);
      expect(find.text('Try again'), findsNothing);
    });

    for (final (locale, tab, empty, hint) in [
      (
        tr,
        'İncelemeler',
        'Henüz inceleme yok',
        'İlk incelemeni bir filmin sayfasından yazabilirsin.',
      ),
      (
        en,
        'Reviews',
        'No reviews yet',
        "Write your first one from a movie's page.",
      ),
    ]) {
      testWidgets('my own empty tab points to movie pages '
          '(${locale.languageCode})', (tester) async {
        final app = AppHarness();
        profileOf(app, 'u1');
        app.server.routes['GET /api/v1/reviews/user/u1'] = (
          200,
          {'items': <Object>[], 'page': 1, 'hasMore': false},
        );
        await app.pump(tester, locale: locale, initialLocation: '/profile');
        await openReviewsTab(tester, tab);
        expect(find.text(empty), findsOneWidget);
        expect(find.text(hint), findsOneWidget);
      });
    }
  });

  group('sign-in poster wall', () {
    AppHarness signedOut() {
      final app = AppHarness();
      app.tokens.access = app.tokens.refresh = null;
      return app;
    }

    for (final locale in [tr, en]) {
      testWidgets('shows the showcase posters, fetched without a token '
          '(${locale.languageCode})', (tester) async {
        final app = signedOut();
        app.server.routes['GET /api/v1/movies/showcase'] = (
          200,
          {
            'posters': ['/one.jpg', '/two.jpg', '/three.jpg'],
          },
        );
        await app.pump(tester, locale: locale);

        expect(find.byType(LoginPage), findsOneWidget);
        expect(find.byType(PosterWall), findsOneWidget);
        expect(posterTiles(), findsWidgets);
        expect(find.byKey(const ValueKey('wall-0-/one.jpg')), findsOneWidget);
        final i = app.server.requests.indexOf('GET /api/v1/movies/showcase');
        expect(app.server.headers[i].containsKey('Authorization'), isFalse);
        expect(app.server.headers[i]['Accept-Language'], locale.languageCode);

        // Kept in memory: sign-up and back don't fetch it again.
        await tester.tap(find.byType(TextButton).last);
        await AppHarness.settle(tester);
        expect(find.byType(RegisterPage), findsOneWidget);
        expect(posterTiles(), findsWidgets);
        expect(
          app.server.requests.where((r) => r == 'GET /api/v1/movies/showcase'),
          hasLength(1),
        );
      });
    }

    testWidgets('no posters (or no answer): tinted tiles, the form works', (
      tester,
    ) async {
      final app = signedOut();
      app.server.routes['GET /api/v1/movies/showcase'] = (
        500,
        {'title': 'TMDb is down'},
      );
      await app.pump(tester, locale: en);
      expect(find.byType(PosterWall), findsOneWidget);
      expect(posterTiles(), findsNothing);
      expect(find.text('Sign in'), findsOneWidget);
      expect(find.text('TMDb is down'), findsNothing);
      expect(tester.takeException(), isNull);
    });
  });
}
