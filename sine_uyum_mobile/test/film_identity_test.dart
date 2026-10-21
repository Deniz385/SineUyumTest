import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/data/models/profile.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/profile/film_identity_page.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';

class FakeSocial implements SocialRepository {
  @override
  Future<FilmIdentity> identity(String userId) async => FilmIdentity.fromJson({
    'userName': 'ayse',
    'totalRatings': 42,
    'averageRating': 7.4,
    'title': 'Aykırı Eleştirmen',
    'titleDescription': 'Puanların genel kanıdan ortalama 2,3 puan sapıyor.',
    'topGenres': [
      {'name': 'Dram', 'share': 45, 'average': 8.1},
    ],
    'contrarianTake': {
      'movieId': 1,
      'title': 'Titanik',
      'rating': 3,
      'tmdbAverage': 7.9,
    },
    'favorite': {'movieId': 2, 'title': 'Esaretin Bedeli', 'rating': 10},
  });

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  testWidgets('kart gösterilir; paylaş PNG üretip paylaşıcıya verir', (
    tester,
  ) async {
    final shared = <(List<int>, String)>[];
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          socialRepositoryProvider.overrideWithValue(FakeSocial()),
          imageSharerProvider.overrideWithValue(
            (png, text) async => shared.add((png, text)),
          ),
        ],
        child: const MaterialApp(
          home: FilmIdentityPage(userId: 'u1', isMine: true),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Aykırı Eleştirmen'), findsOneWidget);
    expect(find.text('42 film · ortalama 7,4'), findsOneWidget);
    expect(find.text('Titanik: herkes 7,9, sen 3'), findsOneWidget);
    expect(find.text('Esaretin Bedeli · 10/10'), findsOneWidget);

    // Görüntü yakalama gerçek zamanlı iş; runAsync içinde.
    await tester.runAsync(() async {
      await tester.tap(find.text('Görsel olarak paylaş'));
      for (var i = 0; i < 50 && shared.isEmpty; i++) {
        await Future<void>.delayed(const Duration(milliseconds: 20));
      }
    });
    await tester.pump();

    expect(shared, hasLength(1));
    // PNG imzası.
    expect(shared.single.$1.take(4), [0x89, 0x50, 0x4E, 0x47]);
    expect(shared.single.$2, 'Aykırı Eleştirmen — SineUyum film kimliğim');
  });

  test('kimlik ayrıştırma: aykırı puan ve favori olmayabilir', () {
    final identity = FilmIdentity.fromJson({
      'userName': 'x',
      'title': 'Yeni Keşifçi',
      'titleDescription': '...',
    });
    expect(identity.contrarianTake, isNull);
    expect(identity.favorite, isNull);
    expect(identity.topGenres, isEmpty);
  });
}
