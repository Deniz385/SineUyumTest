import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/features/discover/discover_page.dart';
import 'package:sine_uyum_mobile/features/notifications/notifications_page.dart';
import 'package:sine_uyum_mobile/features/profile/profile_page.dart';
import 'package:sine_uyum_mobile/features/watchlists/watchlists_page.dart';

import 'support/app_harness.dart';
import 'support/localized_app.dart';

void main() {
  setUpAll(initializeAllDateFormatting);

  Finder navItem(String label) => find.descendant(
    of: find.byType(NavigationBar),
    matching: find.text(label),
  );

  group('bottom navigation', () {
    testWidgets('five tabs: Home · Discover · Event · Messages · Profile '
        '(en)', (tester) async {
      final app = AppHarness();
      await app.pump(tester, locale: en);

      for (final label in [
        'Home',
        'Discover',
        'Event',
        'Messages',
        'Profile',
      ]) {
        expect(navItem(label), findsOneWidget, reason: label);
      }
      // The old People and Lists tabs are gone.
      expect(navItem('Lists'), findsNothing);
      expect(app.router.state.matchedLocation, '/home');
    });

    testWidgets('Discover has Movies, Lists and People sections (tr)', (
      tester,
    ) async {
      final app = AppHarness();
      await app.pump(tester);

      await tester.tap(navItem('Keşfet'));
      await AppHarness.settle(tester);
      expect(find.byType(DiscoverPage), findsOneWidget);
      expect(find.widgetWithText(Tab, 'Filmler'), findsOneWidget);
      expect(find.widgetWithText(Tab, 'Listeler'), findsOneWidget);
      expect(find.widgetWithText(Tab, 'Kişiler'), findsOneWidget);
      // Movies: rows of popular, now playing and top rated films.
      expect(find.text('Popüler filmler'), findsOneWidget);
      expect(find.text('Fight Club'), findsWidgets);

      await tester.tap(find.widgetWithText(Tab, 'Kişiler'));
      await AppHarness.settle(tester);
      expect(find.text('Senin gibi izleyenler'), findsOneWidget);
      expect(find.text('zeynep'), findsOneWidget);
      expect(find.text('%92 uyumlu · 4 ortak film'), findsOneWidget);
      expect(app.server.requests, contains('GET /api/v1/people/suggestions'));

      // Follow from the suggestion.
      app.server.routes['POST /api/v1/follow/u7'] = (200, <String, Object?>{});
      await tester.tap(find.widgetWithText(FilledButton, 'Takip et'));
      await AppHarness.settle(tester);
      expect(app.server.requests, contains('POST /api/v1/follow/u7'));
      expect(find.widgetWithText(OutlinedButton, 'Takip ediliyor'), findsOne);
    });

    testWidgets('the old /people link opens Discover → People', (tester) async {
      final app = AppHarness();
      await app.pump(tester, locale: en, initialLocation: '/people');

      expect(app.router.state.uri.toString(), '/discover?section=people');
      expect(find.text('People who watch like you'), findsOneWidget);
    });

    testWidgets('Profile holds my lists and saved lists (en)', (tester) async {
      final app = AppHarness();
      app.server.routes['GET /api/v1/profile/u1'] = (
        200,
        {
          'id': 'u1',
          'userName': 'ayse',
          'statistics': {'totalRatings': 0},
        },
      );
      app.server.routes['GET /api/v1/watchlist'] = (
        200,
        [
          {'id': 3, 'name': 'Weekend picks', 'itemCount': 2, 'isOwner': true},
        ],
      );
      app.server.routes['GET /api/v1/watchlist/saved'] = (
        200,
        {
          'items': [
            {
              'id': 9,
              'name': 'Cult classics',
              'itemCount': 5,
              'owner': {'id': 'u3', 'userName': 'mehmet'},
              'savedByMe': true,
            },
          ],
          'page': 1,
          'hasMore': false,
        },
      );
      await app.pump(tester, locale: en);

      await tester.tap(navItem('Profile'));
      await AppHarness.settle(tester);
      expect(find.byType(ProfilePage), findsOneWidget);

      await tester.scrollUntilVisible(
        find.text('Weekend picks'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('My lists'), findsWidgets);
      expect(find.text('Weekend picks'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('Cult classics'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Saved lists'), findsOneWidget);

      // "See all" opens the full My lists screen (where invitations are).
      await tester.tap(find.bySemanticsLabel('See all: My lists'));
      await AppHarness.settle(tester);
      expect(find.byType(WatchlistsPage), findsOneWidget);
    });

    testWidgets('the shared-list notification route still opens My lists', (
      tester,
    ) async {
      final app = AppHarness();
      await app.pump(
        tester,
        locale: en,
        initialLocation: notificationRouteFor('/watchlist'),
      );
      expect(find.byType(WatchlistsPage), findsOneWidget);
      expect(find.text('My lists'), findsOneWidget);
    });
  });
}
