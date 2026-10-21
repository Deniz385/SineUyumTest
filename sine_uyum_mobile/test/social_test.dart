import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/data/models/social.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/social/feed_view.dart';
import 'package:sine_uyum_mobile/features/social/movie_reviews_section.dart';
import 'package:sine_uyum_mobile/features/social/review_card.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/social_provider.dart';

Review review({
  int id = 1,
  bool spoilers = false,
  bool liked = false,
  int likes = 0,
  bool mine = false,
}) => Review(
  id: id,
  movie: const ReviewMovie(id: 10, title: 'Başlangıç'),
  author: const Author(id: 'u2', userName: 'mehmet'),
  rating: 9,
  text: 'Topaç dönüyor.',
  containsSpoilers: spoilers,
  likeCount: likes,
  likedByMe: liked,
  isMine: mine,
  authorCompatibility: 82,
);

FeedItem item(int movieId) => FeedItem(
  activityAt: DateTime.utc(2026, 10, 21),
  author: const Author(id: 'u2', userName: 'mehmet'),
  movie: ReviewMovie(id: movieId, title: 'Film $movieId'),
  rating: 7,
);

class FakeSocial implements SocialRepository {
  final pages = <String?, FeedPage>{};
  Object? feedError;
  Object? likeError;
  final likes = <(int, bool)>[];
  MovieReviews reviews = const MovieReviews();
  final upserts = <(int, String, bool)>[];
  TasteTwin? twin;
  final follows = <String>[];

  @override
  Future<TasteTwin?> tasteTwin() async => twin;

  @override
  Future<void> follow(String userId) async => follows.add(userId);

  @override
  Future<FeedPage> feed({String? cursor}) async {
    if (cursor != null && feedError != null) throw feedError!;
    return pages[cursor]!;
  }

  @override
  Future<({bool liked, int likeCount})> setLiked(
    int reviewId,
    bool liked,
  ) async {
    likes.add((reviewId, liked));
    if (likeError != null) throw likeError!;
    return (liked: liked, likeCount: liked ? 5 : 4);
  }

  @override
  Future<MovieReviews> movieReviews(int movieId, {int page = 1}) async =>
      reviews;

  @override
  Future<Review> upsertReview(
    int movieId, {
    required String text,
    required bool containsSpoilers,
  }) async {
    upserts.add((movieId, text, containsSpoilers));
    return review(mine: true);
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  setUpAll(() => initializeDateFormatting('tr'));

  test('akış ve inceleme modelleri ayrıştırılır', () {
    final page = FeedPage.fromJson({
      'items': [
        {
          'type': 'Reviewed',
          'activityAt': '2026-10-21T10:00:00Z',
          'author': {'id': 'a', 'userName': 'ayse', 'profileImageUrl': null},
          'movie': {'id': 5, 'title': 'X', 'posterPath': '/x.jpg'},
          'rating': 8,
          'review': {
            'id': 3,
            'movie': {'id': 5, 'title': 'X'},
            'author': {'id': 'a', 'userName': 'ayse'},
            'rating': 8,
            'text': 'güzel',
            'containsSpoilers': true,
            'likeCount': 2,
            'commentCount': 1,
            'likedByMe': true,
            'isMine': false,
            'authorCompatibility': 76.0,
          },
          'authorCompatibility': 76.0,
        },
      ],
      'nextCursor': 'c1',
    });
    final r = page.items.single.review!;
    expect(r.containsSpoilers, isTrue);
    expect(r.likedByMe, isTrue);
    expect(r.authorCompatibility, 76);
    expect(page.nextCursor, 'c1');
    expect(FeedPage.fromJson({}).items, isEmpty);
  });

  test('sana göre puan ayrıştırılır; yoksa null', () {
    final parsed = MovieReviews.fromJson({
      'forYou': {
        'score': 8.7,
        'basedOn': 4,
        'topMatches': [
          {
            'author': {'id': 'a', 'userName': 'ayse'},
            'rating': 9,
            'compatibility': 93.0,
          },
        ],
      },
    });
    expect(parsed.forYou!.score, 8.7);
    expect(parsed.forYou!.topMatches.single.author.userName, 'ayse');
    expect(MovieReviews.fromJson({'forYou': null}).forYou, isNull);
  });

  test('akış sonraki sayfayı ekler; hata eldekini korur', () async {
    final social = FakeSocial()
      ..pages[null] = FeedPage(items: [item(1), item(2)], nextCursor: 'c1')
      ..pages['c1'] = FeedPage(items: [item(3)]);
    final container = ProviderContainer(
      overrides: [socialRepositoryProvider.overrideWithValue(social)],
    );
    addTearDown(container.dispose);
    container.listen(feedProvider, (_, _) {});

    await container.read(feedProvider.future);
    await container.read(feedProvider.notifier).loadMore();
    var state = container.read(feedProvider).value!;
    expect(state.items.map((i) => i.movie.id), [1, 2, 3]);
    expect(state.hasMore, isFalse);

    // Hata senaryosu: yeni kapsayıcı, ikinci sayfa düşer.
    final failing = FakeSocial()
      ..pages[null] = FeedPage(items: [item(1)], nextCursor: 'c1')
      ..feedError = ApiException('ağ yok');
    final c2 = ProviderContainer(
      overrides: [socialRepositoryProvider.overrideWithValue(failing)],
    );
    addTearDown(c2.dispose);
    c2.listen(feedProvider, (_, _) {});
    await c2.read(feedProvider.future);
    await c2.read(feedProvider.notifier).loadMore();
    state = c2.read(feedProvider).value!;
    expect(state.items.map((i) => i.movie.id), [1]);
    expect(state.hasMore, isTrue);
    expect(state.isLoadingMore, isFalse);
  });

  Future<FakeSocial> pump(
    WidgetTester tester,
    Widget child, {
    FakeSocial? fake,
  }) async {
    final social = fake ?? FakeSocial();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [socialRepositoryProvider.overrideWithValue(social)],
        child: MaterialApp(
          home: Scaffold(body: SingleChildScrollView(child: child)),
        ),
      ),
    );
    await tester.pumpAndSettle();
    return social;
  }

  testWidgets('spoiler gizli; dokununca açılır, uyum rozeti görünür', (
    tester,
  ) async {
    await pump(tester, ReviewCard(review: review(spoilers: true)));

    expect(find.text('Topaç dönüyor.'), findsNothing);
    expect(find.text('%82 uyumlu'), findsOneWidget);
    await tester.tap(find.text('Spoiler içeriyor — göster'));
    await tester.pump();
    expect(find.text('Topaç dönüyor.'), findsOneWidget);
  });

  testWidgets(
    'beğeni iyimser, sunucu sayısıyla düzeltilir; hatada geri alınır',
    (tester) async {
      final social = await pump(tester, ReviewCard(review: review(likes: 3)));

      await tester.tap(find.byIcon(Icons.favorite_border));
      await tester.pumpAndSettle();
      expect(social.likes, [(1, true)]);
      expect(find.byIcon(Icons.favorite), findsOneWidget);
      expect(find.text('5'), findsOneWidget); // sunucunun sayısı

      social.likeError = ApiException('Çok fazla istek.');
      await tester.tap(find.byIcon(Icons.favorite));
      await tester.pumpAndSettle();
      expect(find.byIcon(Icons.favorite), findsOneWidget); // geri alındı
      expect(find.text('5'), findsOneWidget);
      expect(find.text('Çok fazla istek.'), findsOneWidget);
    },
  );

  testWidgets('puan yoksa inceleme yazılamaz; puan varsa paylaşılır', (
    tester,
  ) async {
    final social = FakeSocial();
    await pump(tester, const MovieReviewsSection(movieId: 10), fake: social);
    expect(
      find.text('İnceleme yazmak için önce filmi puanla.'),
      findsOneWidget,
    );

    final rated = FakeSocial()..reviews = const MovieReviews(myRating: 8);
    await pump(tester, const MovieReviewsSection(movieId: 10), fake: rated);
    await tester.enterText(find.byType(TextField), 'Başyapıt.');
    await tester.tap(find.text('Spoiler içeriyor'));
    await tester.tap(find.text('Paylaş'));
    await tester.pumpAndSettle();
    expect(rated.upserts, [(10, 'Başyapıt.', true)]);
  });

  testWidgets('sana göre rozeti yalnızca veri varsa', (tester) async {
    final none = FakeSocial();
    await pump(tester, const ForYouBadge(movieId: 10), fake: none);
    expect(find.textContaining('sana göre'), findsNothing);

    final some = FakeSocial()
      ..reviews = const MovieReviews(
        forYou: ForYouScore(score: 8.66, basedOn: 3),
      );
    await pump(tester, const ForYouBadge(movieId: 10), fake: some);
    expect(find.text('8.7'), findsOneWidget);
    expect(find.text(' sana göre (3 uyumlu kişi)'), findsOneWidget);
  });

  testWidgets('akışın üstünde zevk ikizi; takip edilebilir', (tester) async {
    final social = FakeSocial()
      ..pages[null] = FeedPage(items: [item(1)])
      ..twin = TasteTwin.fromJson({
        'user': {'id': 'u9', 'userName': 'zeynep'},
        'compatibility': 92.0,
        'commonMovieCount': 14,
        'isFollowing': false,
      });
    await tester.pumpWidget(
      ProviderScope(
        overrides: [socialRepositoryProvider.overrideWithValue(social)],
        child: const MaterialApp(home: Scaffold(body: FeedView())),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('zeynep'), findsOneWidget);
    expect(find.text('%92 uyumlu · 14 ortak film'), findsOneWidget);
    await tester.tap(find.text('Takip et'));
    await tester.pumpAndSettle();
    expect(social.follows, ['u9']);
    expect(find.text('Takip ediliyor'), findsOneWidget);
  });

  testWidgets('zevk ikizi yoksa kart görünmez', (tester) async {
    final social = FakeSocial()..pages[null] = FeedPage(items: [item(1)]);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [socialRepositoryProvider.overrideWithValue(social)],
        child: const MaterialApp(home: Scaffold(body: FeedView())),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('ZEVK İKİZİ'), findsNothing);
    expect(find.text('Film 1'), findsOneWidget);
  });
}
