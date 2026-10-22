import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/data/models/profile.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/profile/film_identity_page.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';

import 'support/localized_app.dart';

class FakeSocial implements SocialRepository {
  // Title, description and genre names come localized from the server.
  @override
  Future<FilmIdentity> identity(String userId) async => FilmIdentity.fromJson({
    'userName': 'ayse',
    'totalRatings': 42,
    'averageRating': 7.4,
    'title': 'The Contrarian Critic',
    'titleDescription': 'Your ratings differ from the consensus by 2.3 points.',
    'topGenres': [
      {'name': 'Drama', 'share': 45, 'average': 8.1},
    ],
    'contrarianTake': {
      'movieId': 1,
      'title': 'Titanic',
      'rating': 3,
      'tmdbAverage': 7.9,
    },
    'favorite': {
      'movieId': 2,
      'title': 'The Shawshank Redemption',
      'rating': 10,
    },
  });

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  late List<(List<int>, String)> shared;

  Future<void> pump(WidgetTester tester, Locale locale) async {
    shared = [];
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          socialRepositoryProvider.overrideWithValue(FakeSocial()),
          imageSharerProvider.overrideWithValue(
            (png, text) async => shared.add((png, text)),
          ),
        ],
        child: localizedApp(
          locale: locale,
          home: const FilmIdentityPage(userId: 'u1', isMine: true),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> share(WidgetTester tester, String buttonText) async {
    // Capturing the image is real asynchronous work; run it in runAsync.
    await tester.runAsync(() async {
      await tester.tap(find.text(buttonText));
      for (var i = 0; i < 50 && shared.isEmpty; i++) {
        await Future<void>.delayed(const Duration(milliseconds: 20));
      }
    });
    await tester.pump();
  }

  testWidgets('shows the card; sharing hands a PNG to the sharer', (
    tester,
  ) async {
    await pump(tester, tr);

    expect(find.text('The Contrarian Critic'), findsOneWidget);
    expect(find.text('42 film · ortalama 7,4'), findsOneWidget);
    expect(find.text('%45'), findsOneWidget);
    expect(find.text('Titanic: herkes 7,9, sen 3'), findsOneWidget);
    expect(find.text('The Shawshank Redemption · 10/10'), findsOneWidget);

    await share(tester, 'Görsel olarak paylaş');

    expect(shared, hasLength(1));
    // PNG signature.
    expect(shared.single.$1.take(4), [0x89, 0x50, 0x4E, 0x47]);
    expect(shared.single.$2, 'The Contrarian Critic — SineUyum film kimliğim');
  });

  testWidgets('renders the card in English with English number formats', (
    tester,
  ) async {
    await pump(tester, en);

    expect(find.text('SINEUYUM · FILM IDENTITY'), findsOneWidget);
    expect(find.text('42 movies · average 7.4'), findsOneWidget);
    expect(find.text('YOUR TOP GENRES'), findsOneWidget);
    expect(find.text('45%'), findsOneWidget);
    expect(find.text('Titanic: everyone 7.9, you 3'), findsOneWidget);
    expect(find.text('YOUR FAVORITE'), findsOneWidget);

    await share(tester, 'Share as image');
    expect(
      shared.single.$2,
      'The Contrarian Critic — my SineUyum film identity',
    );
  });

  test('identity parsing: contrarian take and favorite may be missing', () {
    final identity = FilmIdentity.fromJson({
      'userName': 'x',
      'title': 'New Explorer',
      'titleDescription': '...',
    });
    expect(identity.contrarianTake, isNull);
    expect(identity.favorite, isNull);
    expect(identity.topGenres, isEmpty);
  });
}
