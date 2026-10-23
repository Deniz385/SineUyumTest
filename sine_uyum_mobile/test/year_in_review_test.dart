import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/profile.dart';
import 'package:sine_uyum_mobile/data/models/social.dart';
import 'package:sine_uyum_mobile/data/models/year_in_review.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/data/repositories/year_in_review_repository.dart';
import 'package:sine_uyum_mobile/features/profile/film_identity_page.dart';
import 'package:sine_uyum_mobile/features/profile/profile_page.dart';
import 'package:sine_uyum_mobile/features/social/feed_view.dart';
import 'package:sine_uyum_mobile/features/year_in_review/year_in_review_page.dart';
import 'package:sine_uyum_mobile/features/year_in_review/year_in_review_share_card.dart';
import 'package:sine_uyum_mobile/providers/locale_provider.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';
import 'package:sine_uyum_mobile/providers/year_in_review_provider.dart';

import 'support/fake_network.dart';
import 'support/localized_app.dart';
import 'support/movie_rows.dart';

class FakeSession extends SessionNotifier {
  @override
  SessionState build() => const SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(id: 'u1', userName: 'ayse'),
  );
}

/// Recaps per requested year (null: the server's current year); an
/// exception value is thrown instead.
class FakeRecaps implements YearInReviewRepository {
  final byYear = <int?, Object>{};
  final calls = <(String, int?)>[];

  @override
  Future<YearInReview> forUser(String userId, {int? year}) async {
    calls.add((userId, year));
    final value = byYear[year];
    if (value is YearInReview) return value;
    throw value ?? StateError('no recap for $year');
  }
}

class FakeSocial implements SocialRepository {
  UserProfile Function(String userId) profileOf = (id) => UserProfile(
    id: id,
    userName: 'user_$id',
    statistics: const ProfileStatistics(),
  );

  @override
  Future<UserProfile> profile(String userId) async => profileOf(userId);

  @override
  Future<FeedPage> feed({String? cursor}) async => const FeedPage();

  @override
  Future<TasteTwin?> tasteTwin() async => null;

  @override
  Future<FollowStatus> followStatus(String userId) async =>
      const FollowStatus();

  @override
  Future<CompatibilityResult> compatibility(String userId) async =>
      const CompatibilityResult();

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

List<Map<String, Object?>> _months(Map<int, int> counts) => [
  for (var m = 1; m <= 12; m++) {'month': m, 'count': counts[m] ?? 0},
];

/// Every part filled (posters left out: widget tests don't load images).
Map<String, Object?> fullJson({
  int year = 2026,
  bool isMine = true,
  String? posterPath,
}) => {
  'year': year,
  'userName': 'ayse',
  'isMine': isMine,
  'ready': true,
  'minMovies': 3,
  'availableYears': [2026, 2025],
  'totals': {
    'movies': 42,
    'diaryEntries': 45,
    'rewatches': 3,
    'ratings': 40,
    'reviews': 0,
    'likesReceived': 12,
    'commentsReceived': 1,
  },
  'months': _months({1: 2, 3: 4, 10: 9, 12: 1}),
  'busiestMonth': {'month': 10, 'count': 9},
  'favoriteWeekday': {'weekday': 5, 'count': 11},
  'averageRating': 7.4,
  'previousYearAverage': 6.9,
  'identity': {
    'userName': 'ayse',
    'totalRatings': 40,
    'averageRating': 7.4,
    'title': 'The Contrarian Critic',
    'titleDescription': 'Your ratings differ from the consensus.',
    'topGenres': [
      {'name': 'Drama', 'count': 18, 'average': 8.1, 'share': 45},
      {'name': 'Comedy', 'count': 8, 'average': 6.5, 'share': 20},
    ],
    'deviation': 2.3,
    'contrarianTake': {
      'movieId': 1,
      'title': 'Titanic',
      'posterPath': posterPath,
      'rating': 3,
      'tmdbAverage': 7.9,
    },
    'favorite': {
      'movieId': 2,
      'title': 'Parasite',
      'posterPath': posterPath,
      'rating': 10,
      'tmdbAverage': 8.5,
    },
  },
  'topMovies': [
    {
      'movieId': 2,
      'title': 'Parasite',
      'posterPath': posterPath,
      'rating': 10,
      'tmdbAverage': 8.5,
    },
    {
      'movieId': 3,
      'title': 'Heat',
      'posterPath': posterPath,
      'rating': 9,
      'tmdbAverage': null,
    },
  ],
  'firstMovie': {
    'movie': {'id': 3, 'title': 'Heat', 'posterPath': posterPath},
    'watchedOn': '2026-01-03',
  },
  'lastMovie': {
    'movie': {'id': 2, 'title': 'Parasite', 'posterPath': posterPath},
    'watchedOn': '2026-10-18',
  },
  'mostRewatched': {
    'movie': {'id': 4, 'title': 'Alien', 'posterPath': posterPath},
    'count': 3,
  },
  'tasteMate': {
    'user': {'id': 'u7', 'userName': 'mehmet', 'profileImageUrl': null},
    'compatibility': 87.0,
    'commonCount': 21,
  },
};

/// Ready, but with only the totals: every optional slide is skipped.
Map<String, Object?> minimalJson({int year = 2026, bool isMine = true}) => {
  'year': year,
  'userName': 'ayse',
  'isMine': isMine,
  'ready': true,
  'minMovies': 3,
  'availableYears': [year],
  'totals': {'movies': 3},
  'months': _months({}),
  'busiestMonth': null,
  'favoriteWeekday': null,
  'averageRating': null,
  'previousYearAverage': null,
  'identity': {
    'userName': 'ayse',
    'title': 'New Explorer',
    'titleDescription': 'Just getting started.',
    'topGenres': <Object>[],
    'contrarianTake': null,
    'favorite': null,
  },
  'topMovies': <Object>[],
  'firstMovie': null,
  'lastMovie': null,
  'mostRewatched': null,
  'tasteMate': null,
};

YearInReview recapOf(Map<String, Object?> json) => YearInReview.fromJson(json);

/// A phone-sized screen (360×780 logical).
void usePhoneScreen(WidgetTester tester) {
  tester.view.physicalSize = const Size(1080, 2340);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
}

void main() {
  setUpAll(initializeAllDateFormatting);

  group('model', () {
    test('parses every part of a full recap', () {
      final recap = recapOf(fullJson(posterPath: '/p.jpg'));

      expect(recap.year, 2026);
      expect(recap.userName, 'ayse');
      expect(recap.isMine, isTrue);
      expect(recap.ready, isTrue);
      expect(recap.minMovies, 3);
      expect(recap.availableYears, [2026, 2025]);
      expect(recap.totals.movies, 42);
      expect(recap.totals.diaryEntries, 45);
      expect(recap.totals.rewatches, 3);
      expect(recap.totals.ratings, 40);
      expect(recap.totals.reviews, 0);
      expect(recap.totals.likesReceived, 12);
      expect(recap.totals.commentsReceived, 1);
      expect(recap.monthCounts, [2, 0, 4, 0, 0, 0, 0, 0, 0, 9, 0, 1]);
      expect(recap.busiestMonth, (month: 10, count: 9));
      expect(recap.favoriteWeekday, (weekday: 5, count: 11));
      expect(recap.averageRating, 7.4);
      expect(recap.previousYearAverage, 6.9);
      expect(recap.identity.title, 'The Contrarian Critic');
      expect(recap.identity.topGenres.first, (
        name: 'Drama',
        count: 18,
        share: 45,
        average: 8.1,
      ));
      expect(recap.identity.contrarianTake!.tmdbAverage, 7.9);
      expect(recap.identity.favorite!.posterPath, '/p.jpg');
      expect(recap.topMovies.map((m) => m.title), ['Parasite', 'Heat']);
      expect(recap.topMovies.last.tmdbAverage, isNull);
      expect(recap.firstMovie!.movie.title, 'Heat');
      expect(recap.firstMovie!.movie.posterPath, '/p.jpg');
      expect(recap.firstMovie!.watchedOn, DateTime(2026, 1, 3));
      expect(recap.lastMovie!.watchedOn, DateTime(2026, 10, 18));
      expect(recap.mostRewatched!.movie.id, 4);
      expect(recap.mostRewatched!.count, 3);
      expect(recap.tasteMate!.user.id, 'u7');
      expect(recap.tasteMate!.user.userName, 'mehmet');
      expect(recap.tasteMate!.compatibility, 87);
      expect(recap.tasteMate!.commonCount, 21);
    });

    test('nulls and missing parts fall back without throwing', () {
      final recap = recapOf(minimalJson());
      expect(recap.busiestMonth, isNull);
      expect(recap.favoriteWeekday, isNull);
      expect(recap.averageRating, isNull);
      expect(recap.previousYearAverage, isNull);
      expect(recap.identity.contrarianTake, isNull);
      expect(recap.identity.favorite, isNull);
      expect(recap.topMovies, isEmpty);
      expect(recap.firstMovie, isNull);
      expect(recap.lastMovie, isNull);
      expect(recap.mostRewatched, isNull);
      expect(recap.tasteMate, isNull);

      final bare = YearInReview.fromJson({
        'year': 2025,
        'months': [
          {'month': 13, 'count': 5},
          'junk',
          {'month': 2, 'count': 1},
        ],
        'busiestMonth': {'month': 0, 'count': 1},
        'favoriteWeekday': {'weekday': 8},
        'topMovies': ['junk', null],
        'firstMovie': {'movie': null, 'watchedOn': '2025-01-01'},
        'lastMovie': {
          'movie': {'id': 1},
          'watchedOn': 'not a date',
        },
        'mostRewatched': {'count': 2},
        'tasteMate': {'compatibility': 50},
      });
      expect(bare.year, 2025);
      expect(bare.ready, isFalse);
      expect(bare.userName, '');
      expect(bare.availableYears, isEmpty);
      expect(bare.totals.movies, 0);
      expect(bare.monthCounts, [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
      expect(bare.busiestMonth, isNull);
      expect(bare.favoriteWeekday, isNull);
      expect(bare.identity.title, '');
      expect(bare.identity.topGenres, isEmpty);
      expect(bare.topMovies, isEmpty);
      expect(bare.firstMovie, isNull);
      expect(bare.lastMovie, isNull);
      expect(bare.mostRewatched, isNull);
      expect(bare.tasteMate, isNull);
    });

    test('slides are picked by the data that is there', () {
      expect(RecapSlide.forRecap(recapOf(fullJson())), RecapSlide.values);
      expect(RecapSlide.forRecap(recapOf(minimalJson())), [
        RecapSlide.intro,
        RecapSlide.finale,
      ]);

      // A contrarian take without a TMDb average says nothing; only
      // non-zero totals count.
      final json = fullJson();
      (json['identity']! as Map)['contrarianTake'] = {
        'movieId': 1,
        'title': 'Titanic',
        'rating': 3,
      };
      json['totals'] = {'movies': 42, 'reviews': 1};
      json['tasteMate'] = null;
      final slides = RecapSlide.forRecap(recapOf(json));
      expect(slides, contains(RecapSlide.totals));
      expect(slides, isNot(contains(RecapSlide.contrarian)));
      expect(slides, isNot(contains(RecapSlide.tasteMate)));
    });

    test('the offered recap year and the banner season', () {
      expect(recapYearFor(DateTime(2026, 10, 22)), 2026);
      expect(recapYearFor(DateTime(2026, 12, 31)), 2026);
      expect(recapYearFor(DateTime(2027, 1, 15)), 2026);
      expect(recapYearFor(DateTime(2027, 1, 16)), 2027);
      expect(isRecapSeason(DateTime(2026, 11, 30)), isFalse);
      expect(isRecapSeason(DateTime(2026, 12, 1)), isTrue);
      expect(isRecapSeason(DateTime(2027, 1, 15)), isTrue);
      expect(isRecapSeason(DateTime(2027, 1, 16)), isFalse);
      expect(isRecapSeason(DateTime(2027, 3, 10)), isFalse);
    });
  });

  group('repository (real ApiClient)', () {
    late FakeServer server;
    late ProviderContainer container;
    late YearInReviewRepository repository;

    setUp(() {
      server = FakeServer();
      container = ProviderContainer(
        overrides: [
          httpAdapterProvider.overrideWithValue(server),
          tokenStorageProvider.overrideWithValue(
            MemoryTokens(access: 'a', refresh: 'r'),
          ),
          cacheStoreProvider.overrideWithValue(MemoryCacheStore()),
          connectivitySourceProvider.overrideWithValue(FakeConnectivity()),
          sessionProvider.overrideWith(FakeSession.new),
        ],
      );
      addTearDown(container.dispose);
      repository = container.read(yearInReviewRepositoryProvider);
    });

    test('requests the user and the year (none: the current year)', () async {
      server.routes['GET /api/v1/year-in-review/user/u2'] = (200, fullJson());

      final recap = await repository.forUser('u2', year: 2025);
      await repository.forUser('u2');

      expect(server.requests, [
        'GET /api/v1/year-in-review/user/u2',
        'GET /api/v1/year-in-review/user/u2',
      ]);
      expect(server.queries[0], {'year': 2025});
      expect(server.queries[1], isEmpty);
      expect(recap.totals.movies, 42);
    });

    test('a recap can be read offline per year; a private one (403) is not '
        'masked by the cached copy', () async {
      expect(
        ResponseCache.isCacheable('/api/v1/year-in-review/user/u2'),
        isTrue,
      );
      await container.read(responseCacheProvider).useScope('u1');
      server.routes['GET /api/v1/year-in-review/user/u2'] = (200, fullJson());
      await repository.forUser('u2', year: 2026);
      await pumpEventQueue();

      server.offline = true;
      expect((await repository.forUser('u2', year: 2026)).totals.movies, 42);
      // Another year was never cached.
      await expectLater(
        repository.forUser('u2', year: 2025),
        throwsA(isA<ApiException>()),
      );

      server.offline = false;
      server.routes['GET /api/v1/year-in-review/user/u2'] = (
        403,
        {'title': 'This profile is private.'},
      );
      await expectLater(
        repository.forUser('u2', year: 2026),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'status', 403)
              .having((e) => e.message, 'message', 'This profile is private.'),
        ),
      );
    });
  });

  group('story screen', () {
    late FakeRecaps recaps;
    late List<(List<int>, String)> shared;

    Future<void> pumpStory(
      WidgetTester tester, {
      Locale locale = tr,
      int? year = 2026,
      Duration? slideDuration,
    }) async {
      usePhoneScreen(tester);
      shared = [];
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            yearInReviewRepositoryProvider.overrideWithValue(recaps),
            storySlideDurationProvider.overrideWithValue(slideDuration),
            imageSharerProvider.overrideWithValue(
              (png, text) async => shared.add((png, text)),
            ),
          ],
          child: localizedApp(
            locale: locale,
            home: YearInReviewPage(userId: 'u1', year: year),
          ),
        ),
      );
      if (slideDuration == null) {
        await tester.pumpAndSettle();
      } else {
        // Auto-advance keeps animating: step instead of settling (the
        // clock doesn't move; the progress ticker starts at t=0).
        await tester.pump();
        await tester.pump();
        await tester.pump();
      }
    }

    Future<void> next(WidgetTester tester) async {
      await tester.tapAt(const Offset(330, 770));
      await tester.pumpAndSettle();
    }

    Future<void> previous(WidgetTester tester) async {
      await tester.tapAt(const Offset(30, 770));
      await tester.pumpAndSettle();
    }

    setUp(() => recaps = FakeRecaps());

    testWidgets('walks through every slide in Turkish', (tester) async {
      recaps.byYear[2026] = recapOf(fullJson());
      await pumpStory(tester);

      // 1. Intro.
      expect(find.text('Senin 2026 sinema yılın'), findsOneWidget);
      expect(find.text('42'), findsOneWidget);
      expect(find.text('film izledin'), findsOneWidget);

      // 2. Totals: zero ones (reviews) are left out.
      await next(tester);
      expect(find.text('Rakamlarla bu yıl'), findsOneWidget);
      expect(find.text('günlük kaydı'), findsOneWidget);
      expect(find.text('yeniden izleme'), findsOneWidget);
      expect(find.text('film puanlandı'), findsOneWidget);
      expect(find.text('incelemelerine gelen beğeni'), findsOneWidget);
      expect(find.text('incelemelerine gelen yorum'), findsOneWidget);
      expect(find.text('inceleme yazıldı'), findsNothing);

      // Back and forth.
      await previous(tester);
      expect(find.text('Senin 2026 sinema yılın'), findsOneWidget);
      await next(tester);

      // 3. Rhythm: localized month and weekday names.
      await next(tester);
      expect(find.text('Ay ay sinema'), findsOneWidget);
      expect(find.text('En yoğun ayın Ekim oldu: 9 film.'), findsOneWidget);
      expect(find.text('En sevdiğin film günü: Cuma.'), findsOneWidget);
      expect(find.text('Ş'), findsOneWidget); // Şubat

      // 4. Genres.
      await next(tester);
      expect(find.text('En çok izlediğin türler'), findsOneWidget);
      expect(find.text('Drama'), findsOneWidget);
      expect(find.text('%45'), findsOneWidget);
      expect(find.text('18 film'), findsOneWidget);

      // 5. Top movies, the favorite highlighted.
      await next(tester);
      expect(find.text('En yüksek puan verdiklerin'), findsOneWidget);
      expect(find.text('Parasite'), findsOneWidget);
      expect(find.text('10/10'), findsOneWidget);
      expect(find.text('Favori'), findsOneWidget);

      // 6. Contrarian take.
      await next(tester);
      expect(
        find.text('Herkes Titanic filmine 7,9 verdi, sen ise 3.'),
        findsOneWidget,
      );

      // 7. Bookends.
      await next(tester);
      expect(find.text('Yılın ilk filmi'), findsOneWidget);
      expect(find.text('3 Ocak 2026'), findsOneWidget);
      expect(find.text('18 Ekim 2026'), findsOneWidget);
      expect(find.text('Alien'), findsOneWidget);
      expect(find.text('3 kez izlendi'), findsOneWidget);

      // 8. Average rating against last year.
      await next(tester);
      expect(find.text('Ortalama puanın'), findsOneWidget);
      expect(find.text('7,4 /10'), findsOneWidget);
      expect(
        find.text('2025 yılındaki 6,9 ortalamadan daha yüksek'),
        findsOneWidget,
      );

      // 9. Taste mate.
      await next(tester);
      expect(find.text('Zevk ortağın'), findsOneWidget);
      expect(find.text('mehmet'), findsOneWidget);
      expect(find.text('%87 uyumlu'), findsOneWidget);
      expect(find.text('21 ortak film'), findsOneWidget);
      expect(find.text('Profili gör'), findsOneWidget);

      // 10. Finale (the title is also on the story image behind it).
      await next(tester);
      expect(find.text('2026 film kimliğin'), findsOneWidget);
      expect(find.text('The Contrarian Critic'), findsNWidgets(2));
      expect(find.text('Paylaş'), findsOneWidget);

      // The finale stays.
      await next(tester);
      expect(find.text('Paylaş'), findsOneWidget);
    });

    testWidgets('renders in English with English formats; third person for '
        'someone else', (tester) async {
      recaps.byYear[2026] = recapOf(fullJson(isMine: false));
      await pumpStory(tester, locale: en);

      expect(find.text("ayse's 2026 in film"), findsOneWidget);
      expect(find.text('movies watched'), findsOneWidget);

      await next(tester);
      expect(find.text('likes on their reviews'), findsOneWidget);
      expect(find.text('diary entries'), findsOneWidget);

      await next(tester);
      expect(
        find.text('October was their busiest month: 9 movies.'),
        findsOneWidget,
      );
      expect(find.text('Their favorite movie day: Friday.'), findsOneWidget);

      await next(tester);
      expect(find.text('45%'), findsOneWidget);
      await next(tester);
      expect(find.text('Their highest rated'), findsOneWidget);
      await next(tester);
      expect(
        find.text('Everyone gave Titanic 7.9; ayse gave it 3.'),
        findsOneWidget,
      );
      await next(tester);
      expect(find.text('January 3, 2026'), findsOneWidget);
      expect(find.text('Watched 3 times'), findsOneWidget);
      await next(tester);
      expect(find.text('7.4 /10'), findsOneWidget);
      expect(find.text('Up from 6.9 in 2025'), findsOneWidget);
      await next(tester);
      expect(find.text('87% compatible'), findsOneWidget);
      expect(find.text('21 movies in common'), findsOneWidget);
      await next(tester);
      expect(find.text('2026 film identity'), findsOneWidget);
      expect(find.text('Share'), findsOneWidget);
    });

    testWidgets('slides without data are skipped', (tester) async {
      recaps.byYear[2026] = recapOf(minimalJson());
      await pumpStory(tester, locale: en);

      expect(find.text('Your 2026 in film'), findsOneWidget);
      await next(tester);
      // Straight to the finale.
      expect(find.text('Your 2026 film identity'), findsOneWidget);
      expect(find.text('New Explorer'), findsWidgets);
      expect(find.text('The year in numbers'), findsNothing);
    });

    testWidgets('lower and equal averages are phrased accordingly', (
      tester,
    ) async {
      final json = fullJson()
        ..['averageRating'] = 6.5
        ..['tasteMate'] = null;
      recaps.byYear[2026] = recapOf(json);
      await pumpStory(tester, locale: en);
      for (var i = 0; i < 7; i++) {
        await next(tester);
      }
      expect(find.text('Down from 6.9 in 2025'), findsOneWidget);

      final same = fullJson()..['averageRating'] = 6.94;
      recaps.byYear[2025] = recapOf(same..['year'] = 2025);
      await tester.tap(find.byTooltip('Change year'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('2025').last);
      await tester.pumpAndSettle();
      for (var i = 0; i < 7; i++) {
        await next(tester);
      }
      expect(find.text('Same as in 2024'), findsOneWidget);
    });

    testWidgets('the year picker loads another year from the start', (
      tester,
    ) async {
      recaps.byYear[2026] = recapOf(fullJson());
      recaps.byYear[2025] = recapOf(fullJson(year: 2025));
      await pumpStory(tester);
      await next(tester);

      await tester.tap(find.byTooltip('Yılı değiştir'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('2025').last);
      await tester.pumpAndSettle();

      expect(recaps.calls, [('u1', 2026), ('u1', 2025)]);
      expect(find.text('Senin 2025 sinema yılın'), findsOneWidget);
    });

    testWidgets('auto-advances, and a long press pauses', (tester) async {
      recaps.byYear[2026] = recapOf(fullJson());
      await pumpStory(tester, slideDuration: const Duration(seconds: 6));
      expect(find.text('Senin 2026 sinema yılın'), findsOneWidget);

      await tester.pump(const Duration(seconds: 5));
      expect(find.text('Rakamlarla bu yıl'), findsNothing);
      await tester.pump(const Duration(seconds: 1));
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('Rakamlarla bu yıl'), findsOneWidget);

      final press = await tester.startGesture(const Offset(330, 770));
      await tester.pump(kLongPressTimeout + const Duration(milliseconds: 50));
      await tester.pump(const Duration(seconds: 20));
      expect(find.text('Rakamlarla bu yıl'), findsOneWidget);

      // Released: the rest of the slide's time runs, then it moves on.
      await press.up();
      await tester.pump();
      await tester.pump(const Duration(seconds: 6));
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('Ay ay sinema'), findsOneWidget);
    });

    testWidgets('not ready: explains how to unlock it (own recap)', (
      tester,
    ) async {
      recaps.byYear[2026] = recapOf(minimalJson()..['ready'] = false);
      await pumpStory(tester);

      expect(find.text('Henüz değil'), findsOneWidget);
      expect(
        find.text('Özetinin açılması için 2026 yılında en az 3 film kaydet.'),
        findsOneWidget,
      );
      expect(find.text('Günlüğümü aç'), findsOneWidget);
    });

    testWidgets('not ready: someone else\'s recap has no diary button (en)', (
      tester,
    ) async {
      recaps.byYear[2026] = recapOf(
        minimalJson(isMine: false)..['ready'] = false,
      );
      await pumpStory(tester, locale: en);

      expect(
        find.text('ayse needs at least 3 films in 2026 for a recap.'),
        findsOneWidget,
      );
      expect(find.text('Open my diary'), findsNothing);
    });

    testWidgets('a private profile shows the server message without retry', (
      tester,
    ) async {
      recaps.byYear[2026] = ApiException('Bu profil gizli.', statusCode: 403);
      await pumpStory(tester);

      expect(find.text('Bu profil gizli.'), findsOneWidget);
      expect(find.byIcon(Icons.lock_outline_rounded), findsOneWidget);
      expect(find.text('Tekrar dene'), findsNothing);
      expect(find.byTooltip('Kapat'), findsOneWidget);
    });

    testWidgets('sharing hands a 1080×1920 PNG and a text to the sharer', (
      tester,
    ) async {
      recaps.byYear[2026] = recapOf(minimalJson());
      await pumpStory(tester);
      await next(tester);

      // Capturing the image is real asynchronous work; run it in runAsync.
      await tester.runAsync(() async {
        await tester.tap(find.text('Paylaş'));
        for (var i = 0; i < 100 && shared.isEmpty; i++) {
          await Future<void>.delayed(const Duration(milliseconds: 20));
        }
      });
      await tester.pump();

      expect(shared, hasLength(1));
      final png = shared.single.$1;
      expect(png.take(4), [0x89, 0x50, 0x4E, 0x47]);
      int readInt(int offset) =>
          png[offset] << 24 |
          png[offset + 1] << 16 |
          png[offset + 2] << 8 |
          png[offset + 3];
      expect(readInt(16), 1080); // IHDR width
      expect(readInt(20), 1920); // IHDR height
      expect(shared.single.$2, "SineUyum'da 2026 sinema yılım: New Explorer");
    });
  });

  group('share card', () {
    Future<void> pumpCard(
      WidgetTester tester,
      YearInReview recap,
      Locale locale,
    ) async {
      usePhoneScreen(tester);
      await tester.pumpWidget(
        localizedApp(
          locale: locale,
          home: Center(child: YearInReviewShareCard(recap: recap)),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('summarizes the year in the owner\'s voice (tr)', (
      tester,
    ) async {
      await pumpCard(tester, recapOf(fullJson()), tr);

      expect(
        tester.getSize(find.byType(YearInReviewShareCard)),
        const Size(360, 640),
      );
      expect(find.text('SİNEUYUM · YILIN ÖZETİ'), findsOneWidget);
      expect(find.text('Senin 2026 sinema yılın'), findsOneWidget);
      expect(find.text('42'), findsOneWidget);
      expect(find.text('Drama'), findsOneWidget);
      expect(find.text('FAVORİN'), findsOneWidget);
      expect(find.text('Parasite'), findsOneWidget);
      expect(find.text('Ekim'), findsOneWidget);
      expect(find.text('The Contrarian Critic'), findsOneWidget);
    });

    testWidgets('uses the third person for someone else (en)', (tester) async {
      await pumpCard(tester, recapOf(fullJson(isMine: false)), en);

      expect(find.text("ayse's 2026 in film"), findsOneWidget);
      expect(find.text('movies watched'), findsOneWidget);
      expect(find.text('THEIR FAVORITE'), findsOneWidget);
      expect(find.text('BUSIEST MONTH'), findsOneWidget);
      expect(find.text('October'), findsOneWidget);
    });
  });

  group('entry points', () {
    late FakeRecaps recaps;
    late FakeSocial social;
    late MemoryPreferences preferences;

    setUp(() {
      recaps = FakeRecaps();
      social = FakeSocial();
      preferences = MemoryPreferences();
    });

    Future<GoRouter> pumpRouter(
      WidgetTester tester, {
      required String initialLocation,
      required DateTime now,
      Locale locale = tr,
    }) async {
      usePhoneScreen(tester);
      final router = GoRouter(
        initialLocation: initialLocation,
        routes: [
          GoRoute(
            path: '/feed',
            builder: (_, _) => const Scaffold(body: FeedView()),
          ),
          GoRoute(
            path: '/profile/:userId/year-in-review',
            builder: (_, state) => YearInReviewPage(
              userId: state.pathParameters['userId']!,
              year: int.tryParse(state.uri.queryParameters['year'] ?? ''),
            ),
          ),
          GoRoute(
            path: '/profile/:userId',
            builder: (_, state) =>
                ProfilePage(userId: state.pathParameters['userId']!),
          ),
        ],
      );
      addTearDown(router.dispose);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            socialRepositoryProvider.overrideWithValue(social),
            yearInReviewRepositoryProvider.overrideWithValue(recaps),
            sessionProvider.overrideWith(FakeSession.new),
            preferencesStorageProvider.overrideWithValue(preferences),
            clockProvider.overrideWithValue(() => now),
            storySlideDurationProvider.overrideWithValue(null),
            // The feed is thin here: its movie rows stay empty.
            ...noMovieRows,
            // My own profile also lists my lists.
            myWatchlistsProvider.overrideWith((ref) async => const []),
          ],
          child: localizedRouterApp(routerConfig: router, locale: locale),
        ),
      );
      await tester.pumpAndSettle();
      return router;
    }

    testWidgets('own profile: "2026 özetin" opens that year\'s story', (
      tester,
    ) async {
      recaps.byYear[2026] = recapOf(fullJson());
      await pumpRouter(
        tester,
        initialLocation: '/profile/u1',
        now: DateTime(2026, 10, 22),
      );

      await tester.tap(find.text('2026 özetin'));
      await tester.pumpAndSettle();

      expect(find.byType(YearInReviewPage), findsOneWidget);
      expect(recaps.calls, [('u1', 2026)]);
      expect(find.text('Senin 2026 sinema yılın'), findsOneWidget);
    });

    testWidgets('someone else\'s profile in early January offers last '
        'year (en)', (tester) async {
      await pumpRouter(
        tester,
        initialLocation: '/profile/u2',
        now: DateTime(2027, 1, 10),
        locale: en,
      );
      expect(find.text('2026 recap'), findsOneWidget);
    });

    testWidgets('hidden where the diary is hidden (private profile)', (
      tester,
    ) async {
      social.profileOf = (id) => UserProfile(
        id: id,
        userName: 'gizli',
        statistics: const ProfileStatistics(),
        isRestricted: true,
      );
      await pumpRouter(
        tester,
        initialLocation: '/profile/u2',
        now: DateTime(2026, 10, 22),
      );
      expect(find.text('2026 özeti'), findsNothing);
      expect(find.text('Film günlüğü'), findsNothing);
    });

    testWidgets('feed banner in December: opens the recap; hiding it is '
        'remembered for the year', (tester) async {
      recaps.byYear[2026] = recapOf(fullJson());
      await pumpRouter(
        tester,
        initialLocation: '/feed',
        now: DateTime(2026, 12, 10),
      );

      expect(find.text('2026 sinema yılın hazır'), findsOneWidget);
      await tester.tap(find.text('Özetime bak'));
      await tester.pumpAndSettle();
      expect(find.text('Senin 2026 sinema yılın'), findsOneWidget);

      await tester.tap(find.byTooltip('Kapat'));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('Gizle'));
      await tester.pumpAndSettle();
      expect(find.text('2026 sinema yılın hazır'), findsNothing);
      expect(preferences.dismissedRecapYears, {'u1': 2026});

      // Still hidden after a restart.
      await tester.pumpWidget(const SizedBox());
      await pumpRouter(
        tester,
        initialLocation: '/feed',
        now: DateTime(2026, 12, 11),
      );
      expect(find.text('2026 sinema yılın hazır'), findsNothing);
    });

    testWidgets('feed banner in January shows the previous year (en)', (
      tester,
    ) async {
      recaps.byYear[2026] = recapOf(fullJson());
      // Dismissing an older year doesn't hide this one.
      preferences.dismissedRecapYears['u1'] = 2025;
      await pumpRouter(
        tester,
        initialLocation: '/feed',
        now: DateTime(2027, 1, 5),
        locale: en,
      );
      expect(find.text('Your 2026 in film is ready'), findsOneWidget);
      expect(recaps.calls, [('u1', 2026)]);
    });

    testWidgets('no feed banner in March, nor for a recap that isn\'t ready', (
      tester,
    ) async {
      recaps.byYear[2026] = recapOf(fullJson());
      await pumpRouter(
        tester,
        initialLocation: '/feed',
        now: DateTime(2026, 3, 10),
      );
      expect(find.text('2026 sinema yılın hazır'), findsNothing);
      expect(recaps.calls, isEmpty);

      await tester.pumpWidget(const SizedBox());
      recaps.byYear[2026] = recapOf(minimalJson()..['ready'] = false);
      await pumpRouter(
        tester,
        initialLocation: '/feed',
        now: DateTime(2026, 12, 10),
      );
      expect(find.text('2026 sinema yılın hazır'), findsNothing);
    });
  });
}
