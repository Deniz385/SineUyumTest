import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/features/notifications/notifications_page.dart';

void main() {
  group('notificationRouteFor', () {
    test('web etkinlik rotasını mobil sekmesine çevirir', () {
      expect(notificationRouteFor('/my-event'), '/event');
    });

    test('mesaj rotasını sohbet ekranına çevirir', () {
      expect(
        notificationRouteFor('/messages/user-42'),
        '/conversation/user-42',
      );
    });

    test('kullanıcı id taşımayan mesaj rotası listeye gider', () {
      expect(notificationRouteFor('/messages/'), '/messages');
    });

    test('profil ve film rotaları aynı kalır', () {
      expect(notificationRouteFor('/profile/user-7'), '/profile/user-7');
      expect(notificationRouteFor('/movie/550'), '/movie/550');
    });

    test('tanınmayan ve boş rotalar için null döner', () {
      expect(notificationRouteFor('/admin'), isNull);
      expect(notificationRouteFor(''), isNull);
      expect(notificationRouteFor(null), isNull);
    });
  });
}
