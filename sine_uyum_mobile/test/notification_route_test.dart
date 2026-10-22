import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/features/notifications/notifications_page.dart';

void main() {
  group('notificationRouteFor', () {
    test('maps the web event route to the mobile tab', () {
      expect(notificationRouteFor('/my-event'), '/event');
    });

    test('maps a message route to the conversation screen', () {
      expect(
        notificationRouteFor('/messages/user-42'),
        '/conversation/user-42',
      );
    });

    test('a message route without a user id goes to the list', () {
      expect(notificationRouteFor('/messages/'), '/messages');
    });

    test('profile and movie routes stay the same', () {
      expect(notificationRouteFor('/profile/user-7'), '/profile/user-7');
      expect(notificationRouteFor('/movie/550'), '/movie/550');
    });

    test('shared-list notifications open the list or the Lists tab', () {
      expect(notificationRouteFor('/watchlist/7'), '/watchlist/7');
      expect(notificationRouteFor('/watchlist'), '/watchlists');
    });

    test('returns null for unknown and empty routes', () {
      expect(notificationRouteFor('/admin'), isNull);
      expect(notificationRouteFor(''), isNull);
      expect(notificationRouteFor(null), isNull);
    });
  });
}
