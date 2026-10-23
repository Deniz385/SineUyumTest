import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/data/models/social.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/social/feed_view.dart';
import 'package:sine_uyum_mobile/features/social/movie_reviews_section.dart';
import 'package:sine_uyum_mobile/features/social/review_card.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/social_provider.dart';
import 'package:sine_uyum_mobile/providers/year_in_review_provider.dart';

import 'support/localized_app.dart';
import 'support/movie_rows.dart';

Review review({
  int id = 1,
  bool spoilers = false,
  bool liked = false,
  int likes = 0,
  bool mine = false,
}) => Review(
  id: id,
  movie: const ReviewMovie(id: 10, title: 'Inception'),
  author: const Author(id: 'u2', userName: 'mehmet'),
  rating: 9,
  text: 'The top keeps spinning.',
  containsSpoilers: spoilers,
  likeCount: likes,
  likedByMe: liked,
  isMine: mine,
  authorCompatibility: 82,
);

FeedItem item(int movieId) => FeedItem(
  activityAt: DateTime.utc(2026, 10, 21),
  author: const Author(id: 'u2', userName: 'mehmet'),
  movie: ReviewMovie(id: movieId, title: 'Movie $movieId'),
  rating: 7,
);

/// The like button's icon (the compatibility badge has a heart too).
Finder likeIcon(IconData icon) =>
    find.descendant(of: find.byType(TextButton), matching: find.byIcon(icon));

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

TasteTwin twin() => TasteTwin.fromJson({
  'user': {'id': 'u9', 'userName': 'zeynep'},
  'compatibility': 92.0,
  'commonMovieCount': 14,
  'isFollowing': false,
});

void main() {
  setUpAll(initializeAllDateFormatting);

  test('feed and review models are parsed', () {
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
            'text': 'nice',
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

  test('the "for you" score is parsed; null when absent', () {
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

  test('the feed appends the next page; an error keeps what it has', () async {
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

    // Failure case: a new container where the second page fails.
    final failing = FakeSocial()
      ..pages[null] = FeedPage(items: [item(1)], nextCursor: 'c1')
      ..feedError = ApiException('no network');
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
    Locale locale = tr,
  }) async {
    final social = fake ?? FakeSocial();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [socialRepositoryProvider.overrideWithValue(social)],
        child: localizedApp(
          locale: locale,
          home: Scaffold(body: SingleChildScrollView(child: child)),
        ),
      ),
    );
    await tester.pumpAndSettle();
    return social;
  }

  Future<FakeSocial> pumpFeed(
    WidgetTester tester,
    FakeSocial social, {
    Locale locale = tr,
  }) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          socialRepositoryProvider.overrideWithValue(social),
          // Outside the yearly recap season: no recap banner (it would
          // need a session); covered in year_in_review_test.dart.
          clockProvider.overrideWithValue(() => DateTime(2026, 3, 10)),
          // A thin feed adds movie rows; empty here, so they stay hidden.
          ...noMovieRows,
        ],
        child: localizedApp(
          locale: locale,
          home: const Scaffold(body: FeedView()),
        ),
      ),
    );
    await tester.pumpAndSettle();
    return social;
  }

  testWidgets('spoilers hidden until tapped; compatibility badge shown', (
    tester,
  ) async {
    await pump(tester, ReviewCard(review: review(spoilers: true)));

    expect(find.text('The top keeps spinning.'), findsNothing);
    expect(find.text('%82 uyumlu'), findsOneWidget);
    await tester.tap(find.text('Spoiler içeriyor — göster'));
    await tester.pump();
    expect(find.text('The top keeps spinning.'), findsOneWidget);
  });

  testWidgets('the review card renders in English', (tester) async {
    await pump(tester, ReviewCard(review: review(spoilers: true)), locale: en);

    expect(find.text('82% match'), findsOneWidget);
    expect(find.text('9/10'), findsOneWidget);
    expect(find.text('0 comments'), findsOneWidget);
    await tester.tap(find.text('Contains spoilers — show'));
    await tester.pump();
    expect(find.text('The top keeps spinning.'), findsOneWidget);
  });

  testWidgets('likes are optimistic, corrected by the server count, and '
      'rolled back on error', (tester) async {
    final social = await pump(tester, ReviewCard(review: review(likes: 3)));

    await tester.tap(likeIcon(Icons.favorite_border_rounded));
    await tester.pumpAndSettle();
    expect(social.likes, [(1, true)]);
    expect(likeIcon(Icons.favorite_rounded), findsOneWidget);
    expect(find.text('5'), findsOneWidget); // the server's count

    social.likeError = ApiException('Too many requests.');
    await tester.tap(likeIcon(Icons.favorite_rounded));
    await tester.pumpAndSettle();
    expect(likeIcon(Icons.favorite_rounded), findsOneWidget); // rolled back
    expect(find.text('5'), findsOneWidget);
    expect(find.text('Too many requests.'), findsOneWidget);
  });

  testWidgets('no review without a rating; with a rating it can be posted', (
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
    await tester.enterText(find.byType(TextField), 'Masterpiece.');
    await tester.tap(find.text('Spoiler içeriyor'));
    await tester.tap(find.text('Paylaş'));
    await tester.pumpAndSettle();
    expect(rated.upserts, [(10, 'Masterpiece.', true)]);
  });

  testWidgets('the reviews section renders in English', (tester) async {
    await pump(
      tester,
      const MovieReviewsSection(movieId: 10),
      fake: FakeSocial(),
      locale: en,
    );
    expect(find.text('Reviews'), findsOneWidget);
    expect(
      find.text('Rate the movie first to write a review.'),
      findsOneWidget,
    );

    final rated = FakeSocial()..reviews = const MovieReviews(myRating: 8);
    await pump(
      tester,
      const MovieReviewsSection(movieId: 10),
      fake: rated,
      locale: en,
    );
    expect(find.text('Your review (your rating: 8/10)'), findsOneWidget);
    await tester.enterText(find.byType(TextField), 'Masterpiece.');
    await tester.tap(find.text('Post'));
    await tester.pumpAndSettle();
    expect(rated.upserts, [(10, 'Masterpiece.', false)]);
  });

  testWidgets('the "for you" badge only appears with data', (tester) async {
    final none = FakeSocial();
    await pump(tester, const ForYouBadge(movieId: 10), fake: none);
    expect(find.text('SANA GÖRE'), findsNothing);

    final some = FakeSocial()
      ..reviews = const MovieReviews(
        forYou: ForYouScore(score: 8.66, basedOn: 3),
      );
    await pump(tester, const ForYouBadge(movieId: 10), fake: some);
    // Turkish uses a decimal comma.
    expect(find.text('8,7'), findsOneWidget);
    expect(find.text('SANA GÖRE'), findsOneWidget);
    expect(find.text('3 uyumlu kişiye göre'), findsOneWidget);

    await pump(tester, const ForYouBadge(movieId: 10), fake: some, locale: en);
    expect(find.text('8.7'), findsOneWidget);
    expect(find.text('FOR YOU'), findsOneWidget);
    expect(find.text('From 3 matching people'), findsOneWidget);
  });

  testWidgets('taste twin above the feed; can be followed', (tester) async {
    final social = FakeSocial()
      ..pages[null] = FeedPage(items: [item(1)])
      ..twin = twin();
    await pumpFeed(tester, social);

    expect(find.text('zeynep'), findsOneWidget);
    expect(find.text('%92 uyumlu · 14 ortak film'), findsOneWidget);
    await tester.tap(find.text('Takip et'));
    await tester.pumpAndSettle();
    expect(social.follows, ['u9']);
    expect(find.text('Takip ediliyor'), findsOneWidget);
  });

  testWidgets('the feed and taste twin render in English', (tester) async {
    final social = FakeSocial()
      ..pages[null] = FeedPage(items: [item(1)])
      ..twin = twin();
    await pumpFeed(tester, social, locale: en);

    expect(find.text("✨ THIS WEEK'S TASTE TWIN"), findsOneWidget);
    expect(find.text('92% match · 14 movies in common'), findsOneWidget);
    expect(find.text('rated a movie · Oct 21, 2026'), findsOneWidget);
    await tester.tap(find.text('Follow'));
    await tester.pumpAndSettle();
    expect(find.text('Following'), findsOneWidget);
  });

  testWidgets('no taste twin card without a twin', (tester) async {
    final social = FakeSocial()..pages[null] = FeedPage(items: [item(1)]);
    await pumpFeed(tester, social);
    expect(find.textContaining('ZEVK İKİZİ'), findsNothing);
    expect(find.text('Movie 1'), findsOneWidget);
  });
}
