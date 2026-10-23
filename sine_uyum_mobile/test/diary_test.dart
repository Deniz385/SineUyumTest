import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/diary.dart';
import 'package:sine_uyum_mobile/data/models/movie.dart';
import 'package:sine_uyum_mobile/data/models/profile.dart';
import 'package:sine_uyum_mobile/data/repositories/diary_repository.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/diary/diary_entry_sheet.dart';
import 'package:sine_uyum_mobile/features/diary/diary_page.dart';
import 'package:sine_uyum_mobile/features/movies/movie_detail_page.dart';
import 'package:sine_uyum_mobile/features/profile/profile_page.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';
import 'package:sine_uyum_mobile/providers/diary_provider.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/fake_network.dart';
import 'support/localized_app.dart';

class FakeSession extends SessionNotifier {
  @override
  SessionState build() => const SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(id: 'u1', userName: 'ayse'),
  );
}

/// In-memory diary: summaries are given per year, entries are paged from
/// [entriesByYear] like the server does (newest first, as listed).
class FakeDiary implements DiaryRepository {
  int currentYear = 2026;
  final summaries = <int, DiarySummary>{};
  final entriesByYear = <int, List<DiaryEntry>>{};
  Object? error;
  final calls = <String>[];

  @override
  Future<DiarySummary> summary(String userId, {int? year}) async {
    calls.add('summary $userId $year');
    if (error != null) throw error!;
    final y = year ?? currentYear;
    return summaries[y] ?? DiarySummary(year: y);
  }

  @override
  Future<DiaryEntriesPage> entries(
    String userId, {
    int? year,
    int page = 1,
    int pageSize = DiaryRepository.pageSize,
  }) async {
    calls.add('entries $userId $year $page');
    if (error != null) throw error!;
    final all = entriesByYear[year] ?? const <DiaryEntry>[];
    return DiaryEntriesPage(
      items: all.skip((page - 1) * pageSize).take(pageSize).toList(),
      totalCount: all.length,
    );
  }

  @override
  Future<DiaryEntry> update(int entryId, DiaryDraft draft) async {
    calls.add(
      'update $entryId ${formatDiaryDate(draft.watchedOn)} '
      '${draft.rewatch} ${draft.note}',
    );
    for (final list in entriesByYear.values) {
      final index = list.indexWhere((e) => e.id == entryId);
      if (index >= 0) return list[index] = list[index].applying(draft);
    }
    throw StateError('no entry $entryId');
  }

  @override
  Future<void> delete(int entryId) async {
    calls.add('delete $entryId');
    for (final list in entriesByYear.values) {
      list.removeWhere((e) => e.id == entryId);
    }
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeProfiles implements SocialRepository {
  @override
  Future<UserProfile> profile(String userId) async => UserProfile(
    id: userId,
    userName: 'ayse',
    statistics: const ProfileStatistics(),
  );

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

DiaryEntry _entry(
  int id,
  String title,
  DateTime watchedOn, {
  int? rating,
  bool rewatch = false,
  String? note,
}) => DiaryEntry(
  id: id,
  movie: Movie(id: 1000 + id, title: title),
  watchedOn: watchedOn,
  rating: rating,
  rewatch: rewatch,
  note: note,
);

List<int> _months(Map<int, int> counts) => [
  for (var m = 1; m <= 12; m++) counts[m] ?? 0,
];

/// 2026: three entries (two in October, one in January); 2025: one.
FakeDiary _sampleDiary() => FakeDiary()
  ..summaries[2026] = DiarySummary(
    year: 2026,
    totalCount: 3,
    rewatchCount: 1,
    monthCounts: _months({1: 1, 10: 2}),
    availableYears: const [2026, 2025],
  )
  ..summaries[2025] = DiarySummary(
    year: 2025,
    totalCount: 1,
    monthCounts: _months({5: 1}),
    availableYears: const [2026, 2025],
  )
  ..entriesByYear[2026] = [
    _entry(
      3,
      'Fight Club',
      DateTime(2026, 10, 12),
      rating: 9,
      rewatch: true,
      note: 'Still great.',
    ),
    _entry(2, 'Arrival', DateTime(2026, 10, 3)),
    _entry(1, 'Dune', DateTime(2026, 1, 15), rating: 7),
  ]
  ..entriesByYear[2025] = [_entry(4, 'Parasite', DateTime(2025, 5, 1))];

Map<String, Object?> _entryJson({
  int id = 9,
  String watchedOn = '2026-10-12',
  bool rewatch = false,
  String? note,
  int? rating,
}) => {
  'id': id,
  'movie': {'id': 550, 'title': 'Fight Club', 'posterPath': '/fc.jpg'},
  'watchedOn': watchedOn,
  'rewatch': rewatch,
  'note': note,
  'rating': rating,
  'createdAt': '2026-10-12T19:30:00Z',
};

void main() {
  setUpAll(initializeAllDateFormatting);
  tearDown(() => L10n.locale = en);

  group('models', () {
    test('entry parses the movie, the date-only watch date and the rating', () {
      final entry = DiaryEntry.fromJson(
        _entryJson(rewatch: true, note: 'With Ali', rating: 9),
      );
      expect(entry.id, 9);
      expect(entry.movie.id, 550);
      expect(entry.movie.title, 'Fight Club');
      expect(entry.movie.posterPath, '/fc.jpg');
      // A calendar date: local midnight, never shifted by the time zone.
      expect(entry.watchedOn, DateTime(2026, 10, 12));
      expect(entry.rewatch, isTrue);
      expect(entry.note, 'With Ali');
      expect(entry.rating, 9);
      expect(entry.createdAt, DateTime.utc(2026, 10, 12, 19, 30));

      final bare = DiaryEntry.fromJson(_entryJson(note: '  '));
      expect(bare.note, isNull);
      expect(bare.rating, isNull);
      expect(bare.rewatch, isFalse);
    });

    test('dates are sent as yyyy-MM-dd', () {
      expect(formatDiaryDate(DateTime(2026, 3, 7)), '2026-03-07');
      expect(formatDiaryDate(DateTime(1999, 12, 31, 23, 59)), '1999-12-31');
      expect(parseDiaryDate('2026-03-07'), DateTime(2026, 3, 7));
      expect(parseDiaryDate('nope'), isNull);
    });

    test('page parses items and the total count', () {
      final page = DiaryEntriesPage.fromJson({
        'items': [_entryJson(id: 2), _entryJson(id: 1)],
        'totalCount': 41,
      });
      expect(page.items.map((e) => e.id), [2, 1]);
      expect(page.totalCount, 41);
    });

    test('summary always has 12 months and keeps the years order', () {
      final summary = DiarySummary.fromJson({
        'year': 2026,
        'totalCount': 5,
        'rewatchCount': 2,
        'months': [
          {'month': 10, 'count': 3},
          {'month': 1, 'count': 2},
          {'month': 13, 'count': 9},
        ],
        'availableYears': [2026, 2024],
      });
      expect(summary.year, 2026);
      expect(summary.totalCount, 5);
      expect(summary.rewatchCount, 2);
      expect(summary.monthCounts, hasLength(12));
      expect(summary.countFor(1), 2);
      expect(summary.countFor(10), 3);
      expect(summary.countFor(12), 0);
      expect(summary.availableYears, [2026, 2024]);
    });
  });

  group('repository (real ApiClient)', () {
    late FakeServer server;
    late ProviderContainer container;
    late DiaryRepository diary;

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
      diary = container.read(diaryRepositoryProvider);
    });

    test('create posts the movie, date, rewatch, note and rating', () async {
      server.routes['POST /api/v1/diary'] = (
        201,
        _entryJson(rewatch: true, note: 'With Ali', rating: 8),
      );
      final created = await diary.create(
        550,
        DiaryDraft(
          watchedOn: DateTime(2026, 10, 12),
          rewatch: true,
          note: '  With Ali ',
          rating: 8,
        ),
      );
      expect(server.requests, ['POST /api/v1/diary']);
      expect(server.bodies.single, {
        'movieId': 550,
        'watchedOn': '2026-10-12',
        'rewatch': true,
        'note': 'With Ali',
        'rating': 8,
      });
      expect(created.rating, 8);
    });

    test(
      'create without a rating leaves it out; a blank note is null',
      () async {
        server.routes['POST /api/v1/diary'] = (201, _entryJson());
        await diary.create(
          550,
          DiaryDraft(watchedOn: DateTime(2026, 1, 2), note: '   '),
        );
        expect(server.bodies.single, {
          'movieId': 550,
          'watchedOn': '2026-01-02',
          'rewatch': false,
          'note': null,
        });
      },
    );

    test('update puts date, rewatch and note; delete hits the entry', () async {
      server.routes['PUT /api/v1/diary/9'] = (
        200,
        _entryJson(watchedOn: '2026-10-11', note: 'Edited'),
      );
      server.routes['DELETE /api/v1/diary/9'] = (200, {'message': 'Deleted.'});

      final updated = await diary.update(
        9,
        DiaryDraft(watchedOn: DateTime(2026, 10, 11), note: 'Edited'),
      );
      await diary.delete(9);

      expect(server.requests, [
        'PUT /api/v1/diary/9',
        'DELETE /api/v1/diary/9',
      ]);
      expect(server.bodies.first, {
        'watchedOn': '2026-10-11',
        'rewatch': false,
        'note': 'Edited',
      });
      expect(updated.watchedOn, DateTime(2026, 10, 11));
    });

    test('list and summary send the year and paging', () async {
      server.routes['GET /api/v1/diary/user/u2'] = (
        200,
        {
          'items': [_entryJson()],
          'totalCount': 21,
        },
      );
      server.routes['GET /api/v1/diary/user/u2/summary'] = (
        200,
        {
          'year': 2025,
          'totalCount': 21,
          'rewatchCount': 0,
          'months': [
            for (var m = 1; m <= 12; m++) {'month': m, 'count': 0},
          ],
          'availableYears': [2025],
        },
      );

      final page = await diary.entries('u2', year: 2025, page: 2);
      final summary = await diary.summary('u2', year: 2025);
      await diary.summary('u2');

      expect(server.requests, [
        'GET /api/v1/diary/user/u2',
        'GET /api/v1/diary/user/u2/summary',
        'GET /api/v1/diary/user/u2/summary',
      ]);
      expect(server.queries[0], {'year': 2025, 'page': 2, 'pageSize': 20});
      expect(server.queries[1], {'year': 2025});
      // No year: the server picks the current one.
      expect(server.queries[2], isEmpty);
      expect(page.totalCount, 21);
      expect(summary.year, 2025);
    });

    test('movie entries: my entries for one movie, newest first; not '
        'cached for offline use', () async {
      server.routes['GET /api/v1/diary/movie/550'] = (
        200,
        [
          _entryJson(id: 12, watchedOn: '2026-10-12', rewatch: true),
          _entryJson(id: 7, watchedOn: '2025-02-01'),
        ],
      );

      final entries = await diary.movieEntries(550);

      expect(server.requests, ['GET /api/v1/diary/movie/550']);
      expect(entries.map((e) => e.id), [12, 7]);
      expect(entries.first.watchedOn, DateTime(2026, 10, 12));
      expect(entries.first.movie.id, 550);
      expect(ResponseCache.isCacheable('/api/v1/diary/movie/550'), isFalse);

      server.routes['GET /api/v1/diary/movie/551'] = (200, <Object>[]);
      expect(await diary.movieEntries(551), isEmpty);
    });

    test('the duplicate (409) message is passed on as-is', () async {
      server.routes['POST /api/v1/diary'] = (
        409,
        {'title': 'You already logged this movie on that day.'},
      );
      await expectLater(
        diary.create(550, DiaryDraft(watchedOn: DateTime(2026, 10, 12))),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'status', 409)
              .having(
                (e) => e.message,
                'message',
                'You already logged this movie on that day.',
              ),
        ),
      );
    });

    test('a diary can be read offline; a private one (403) is not masked '
        'by the cached copy', () async {
      expect(ResponseCache.isCacheable('/api/v1/diary/user/u2'), isTrue);
      expect(
        ResponseCache.isCacheable('/api/v1/diary/user/u2/summary'),
        isTrue,
      );
      expect(ResponseCache.isCacheable('/api/v1/diary'), isFalse);
      expect(ResponseCache.isCacheable('/api/v1/diary/9'), isFalse);

      await container.read(responseCacheProvider).useScope('u1');
      server.routes['GET /api/v1/diary/user/u2'] = (
        200,
        {
          'items': [_entryJson(note: 'cached')],
          'totalCount': 1,
        },
      );
      await diary.entries('u2', year: 2026);
      await pumpEventQueue();

      server.offline = true;
      final offline = await diary.entries('u2', year: 2026);
      expect(offline.items.single.note, 'cached');

      server.offline = false;
      server.routes['GET /api/v1/diary/user/u2'] = (
        403,
        {'title': 'This profile is private.'},
      );
      await expectLater(
        diary.entries('u2', year: 2026),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'This profile is private.',
          ),
        ),
      );
    });
  });

  group('provider', () {
    late FakeDiary fake;
    late ProviderContainer container;

    setUp(() {
      fake = FakeDiary();
      container = ProviderContainer(
        overrides: [diaryRepositoryProvider.overrideWithValue(fake)],
      );
      addTearDown(container.dispose);
    });

    Future<DiaryState> load(DiaryKey key) async {
      final sub = container.listen(diaryProvider(key), (_, _) {});
      addTearDown(sub.close);
      return container.read(diaryProvider(key).future);
    }

    test('without a year it opens the newest year that has entries', () async {
      fake
        ..summaries[2025] = DiarySummary(
          year: 2025,
          totalCount: 1,
          monthCounts: _months({5: 1}),
          availableYears: const [2025, 2023],
        )
        ..summaries[2026] = const DiarySummary(
          year: 2026,
          availableYears: [2025, 2023],
        )
        ..entriesByYear[2025] = [_entry(4, 'Parasite', DateTime(2025, 5, 1))];

      final state = await load((userId: 'u1', year: null));

      expect(fake.calls, [
        'summary u1 null',
        'summary u1 2025',
        'entries u1 2025 1',
      ]);
      expect(state.year, 2025);
      expect(state.entries.single.movie.title, 'Parasite');
    });

    test(
      'loads more pages; after a delete no entry is skipped or repeated',
      () async {
        fake
          ..summaries[2026] = const DiarySummary(
            year: 2026,
            totalCount: 25,
            availableYears: [2026],
          )
          ..entriesByYear[2026] = [
            for (var i = 25; i >= 1; i--)
              _entry(
                i,
                'Movie $i',
                DateTime(2026, 1, 1).add(Duration(days: i)),
              ),
          ];
        const key = (userId: 'u1', year: 2026);
        var state = await load(key);
        expect(state.entries, hasLength(20));
        expect(state.hasMore, isTrue);

        final notifier = container.read(diaryProvider(key).notifier);
        await notifier.deleteEntry(25);
        await pumpEventQueue();
        await notifier.loadMore();

        state = container.read(diaryProvider(key)).value!;
        expect(state.entries.map((e) => e.id), [
          for (var i = 24; i >= 1; i--) i,
        ]);
        expect(state.hasMore, isFalse);
        expect(fake.calls, contains('delete 25'));
      },
    );
  });

  group('add to diary from the movie page', () {
    late FakeServer server;

    Future<void> pumpMovie(WidgetTester tester, {Locale locale = tr}) async {
      server = FakeServer()
        ..routes['GET /api/v1/movies/550'] = (
          200,
          {'id': 550, 'title': 'Fight Club', 'release_date': '1999-10-15'},
        )
        ..routes['GET /api/v1/reviews/movie/550'] = (
          200,
          {'myRating': 8, 'reviews': <Object>[], 'totalCount': 0},
        );
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            httpAdapterProvider.overrideWithValue(server),
            tokenStorageProvider.overrideWithValue(
              MemoryTokens(access: 'a', refresh: 'r'),
            ),
            cacheStoreProvider.overrideWithValue(MemoryCacheStore()),
            connectivitySourceProvider.overrideWithValue(FakeConnectivity()),
            sessionProvider.overrideWith(FakeSession.new),
          ],
          child: localizedApp(
            locale: locale,
            home: const MovieDetailPage(movieId: 550),
          ),
        ),
      );
      await tester.pumpAndSettle();
    }

    /// Body of the last diary POST (refreshes follow it after a save).
    Object? postedBody() =>
        server.bodies[server.requests.lastIndexOf('POST /api/v1/diary')];

    Finder inSheet(Finder finder) =>
        find.descendant(of: find.byType(DiaryEntrySheet), matching: finder);

    Future<void> tapInSheet(WidgetTester tester, Finder finder) async {
      await tester.ensureVisible(finder);
      await tester.pumpAndSettle();
      await tester.tap(finder);
      await tester.pumpAndSettle();
    }

    testWidgets('submits the right body, shows the 409 message, then saves '
        '(tr)', (tester) async {
      await pumpMovie(tester);
      final today = DateTime.now();

      await tester.ensureVisible(find.text('Günlüğe ekle'));
      await tester.tap(find.text('Günlüğe ekle'));
      await tester.pumpAndSettle();

      expect(inSheet(find.text('Ne zaman izledin?')), findsOneWidget);
      expect(
        inSheet(find.text(DateFormat.yMMMMEEEEd('tr').format(today))),
        findsOneWidget,
      );
      // My current rating is prefilled.
      expect(inSheet(find.text('8/10')), findsOneWidget);

      await tapInSheet(tester, inSheet(find.text('Yeniden izleme')));
      await tester.enterText(
        inSheet(find.byType(TextField)),
        'Sinemada izledim',
      );
      server.routes['POST /api/v1/diary'] = (
        409,
        {'title': 'Bu filmi o gün zaten günlüğüne eklemişsin.'},
      );
      await tapInSheet(tester, inSheet(find.text('Günlüğe kaydet')));

      expect(server.requests.last, 'POST /api/v1/diary');
      expect(server.bodies.last, {
        'movieId': 550,
        'watchedOn': formatDiaryDate(today),
        'rewatch': true,
        'note': 'Sinemada izledim',
        'rating': 8,
      });
      // The sheet stays open with the server's message.
      expect(
        inSheet(find.text('Bu filmi o gün zaten günlüğüne eklemişsin.')),
        findsOneWidget,
      );

      server.routes['POST /api/v1/diary'] = (
        201,
        _entryJson(rewatch: true, rating: 8),
      );
      final reviewLoads = server.requests
          .where((r) => r == 'GET /api/v1/reviews/movie/550')
          .length;
      await tapInSheet(tester, inSheet(find.text('Günlüğe kaydet')));

      expect(find.byType(DiaryEntrySheet), findsNothing);
      expect(find.text('Günlüğüne eklendi.'), findsOneWidget);
      // The rating was saved too: the movie's rating info is reloaded.
      expect(
        server.requests.where((r) => r == 'GET /api/v1/reviews/movie/550'),
        hasLength(reviewLoads + 1),
      );
    });

    bool rewatchInSheet(WidgetTester tester, String label) => tester
        .widget<SwitchListTile>(
          inSheet(find.widgetWithText(SwitchListTile, label)),
        )
        .value;

    testWidgets('a movie logged before pre-selects "Rewatch" and shows the '
        'last date (tr)', (tester) async {
      await pumpMovie(tester);
      server.routes['GET /api/v1/diary/movie/550'] = (
        200,
        [
          _entryJson(id: 12, watchedOn: '2026-10-12'),
          _entryJson(id: 7, watchedOn: '2025-02-01'),
        ],
      );
      server.routes['POST /api/v1/diary'] = (201, _entryJson(rewatch: true));

      await tester.ensureVisible(find.text('Günlüğe ekle'));
      await tester.tap(find.text('Günlüğe ekle'));
      await tester.pumpAndSettle();

      expect(server.requests, contains('GET /api/v1/diary/movie/550'));
      expect(
        inSheet(find.text('En son 12 Ekim 2026 tarihinde kaydettin')),
        findsOneWidget,
      );
      expect(rewatchInSheet(tester, 'Yeniden izleme'), isTrue);
      await tapInSheet(tester, inSheet(find.text('Günlüğe kaydet')));

      expect(postedBody(), containsPair('rewatch', true));
      expect(find.text('Günlüğüne eklendi.'), findsOneWidget);
    });

    testWidgets('the last date is localized; "Rewatch" can still be turned '
        'off (en)', (tester) async {
      await pumpMovie(tester, locale: en);
      server.routes['GET /api/v1/diary/movie/550'] = (
        200,
        [_entryJson(id: 12, watchedOn: '2026-10-12')],
      );
      server.routes['POST /api/v1/diary'] = (201, _entryJson());

      await tester.ensureVisible(find.text('Add to diary'));
      await tester.tap(find.text('Add to diary'));
      await tester.pumpAndSettle();

      expect(
        inSheet(find.text('Last logged on October 12, 2026')),
        findsOneWidget,
      );
      expect(rewatchInSheet(tester, 'Rewatch'), isTrue);
      await tapInSheet(tester, inSheet(find.text('Rewatch')));
      await tapInSheet(tester, inSheet(find.text('Save to diary')));

      expect(postedBody(), containsPair('rewatch', false));
    });

    testWidgets('if the lookup fails the sheet works as before (tr)', (
      tester,
    ) async {
      await pumpMovie(tester);
      server.routes['GET /api/v1/diary/movie/550'] = (
        500,
        {'title': 'Sunucu hatası'},
      );
      server.routes['POST /api/v1/diary'] = (201, _entryJson());

      await tester.ensureVisible(find.text('Günlüğe ekle'));
      await tester.tap(find.text('Günlüğe ekle'));
      await tester.pumpAndSettle();

      expect(server.requests, contains('GET /api/v1/diary/movie/550'));
      expect(rewatchInSheet(tester, 'Yeniden izleme'), isFalse);
      expect(inSheet(find.text('Daha önce de izlemiştin')), findsOneWidget);
      expect(inSheet(find.textContaining('En son')), findsNothing);
      expect(find.text('Sunucu hatası'), findsNothing);
      await tapInSheet(tester, inSheet(find.text('Günlüğe kaydet')));

      expect(postedBody(), containsPair('rewatch', false));
      expect(find.byType(DiaryEntrySheet), findsNothing);
      expect(find.text('Günlüğüne eklendi.'), findsOneWidget);
    });

    testWidgets('the rating can be left out (en)', (tester) async {
      await pumpMovie(tester, locale: en);
      server.routes['POST /api/v1/diary'] = (201, _entryJson());

      await tester.ensureVisible(find.text('Add to diary'));
      await tester.tap(find.text('Add to diary'));
      await tester.pumpAndSettle();
      await tapInSheet(tester, inSheet(find.text('Rate it too')));
      expect(inSheet(find.text('8/10')), findsNothing);
      await tapInSheet(tester, inSheet(find.text('Save to diary')));

      expect(server.bodies.last, {
        'movieId': 550,
        'watchedOn': formatDiaryDate(DateTime.now()),
        'rewatch': false,
        'note': null,
      });
      expect(find.text('Added to your diary.'), findsOneWidget);
    });
  });

  group('entry sheet', () {
    Future<void> pumpSheet(
      WidgetTester tester, {
      DiaryEntry? entry,
      Future<List<DiaryEntry>> Function()? loadMovieEntries,
    }) async {
      await tester.pumpWidget(
        localizedApp(
          locale: en,
          home: Scaffold(
            body: DiaryEntrySheet(
              key: UniqueKey(),
              movieTitle: 'Fight Club',
              entry: entry,
              loadMovieEntries: loadMovieEntries,
              onSubmit: (_) async {},
            ),
          ),
        ),
      );
    }

    bool rewatchValue(WidgetTester tester) => tester
        .widget<SwitchListTile>(find.widgetWithText(SwitchListTile, 'Rewatch'))
        .value;

    testWidgets("a choice made before the lookup returns isn't overridden", (
      tester,
    ) async {
      final lookup = Completer<List<DiaryEntry>>();
      await pumpSheet(tester, loadMovieEntries: () => lookup.future);
      expect(rewatchValue(tester), isFalse);

      // On, then off again: the user's own choice.
      await tester.tap(find.text('Rewatch'));
      await tester.pump();
      await tester.tap(find.text('Rewatch'));
      await tester.pump();

      lookup.complete([_entry(1, 'Fight Club', DateTime(2026, 10, 12))]);
      await tester.pumpAndSettle();

      expect(rewatchValue(tester), isFalse);
      expect(find.text('Last logged on October 12, 2026'), findsOneWidget);
    });

    testWidgets('no earlier entries: nothing changes; editing never looks '
        'up', (tester) async {
      await pumpSheet(tester, loadMovieEntries: () async => []);
      await tester.pumpAndSettle();
      expect(rewatchValue(tester), isFalse);
      expect(find.text("You've seen it before"), findsOneWidget);

      var lookups = 0;
      await pumpSheet(
        tester,
        entry: _entry(3, 'Fight Club', DateTime(2026, 10, 1)),
        loadMovieEntries: () async {
          lookups++;
          return [_entry(1, 'Fight Club', DateTime(2026, 10, 12))];
        },
      );
      await tester.pumpAndSettle();
      expect(lookups, 0);
      expect(find.textContaining('Last logged'), findsNothing);
    });
  });

  group('diary page', () {
    Future<FakeDiary> pumpDiary(
      WidgetTester tester, {
      required FakeDiary diary,
      String userId = 'u1',
      Locale locale = tr,
    }) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            diaryRepositoryProvider.overrideWithValue(diary),
            sessionProvider.overrideWith(FakeSession.new),
          ],
          child: localizedApp(
            locale: locale,
            home: DiaryPage(userId: userId),
          ),
        ),
      );
      await tester.pumpAndSettle();
      return diary;
    }

    testWidgets('my profile links to my diary (tr)', (tester) async {
      final router = GoRouter(
        initialLocation: '/profile/u1',
        routes: [
          GoRoute(
            path: '/profile/:userId/diary',
            builder: (_, state) =>
                DiaryPage(userId: state.pathParameters['userId']!),
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
            diaryRepositoryProvider.overrideWithValue(_sampleDiary()),
            socialRepositoryProvider.overrideWithValue(FakeProfiles()),
            sessionProvider.overrideWith(FakeSession.new),
            // My own profile also lists my lists.
            myWatchlistsProvider.overrideWith((ref) async => const []),
          ],
          child: localizedRouterApp(routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Günlüğüm'));
      await tester.pumpAndSettle();

      expect(find.byType(DiaryPage), findsOneWidget);
      expect(find.text('2026 yılında 3 film izlendi'), findsOneWidget);
    });

    testWidgets('renders the chart, totals and month groups (tr)', (
      tester,
    ) async {
      await pumpDiary(tester, diary: _sampleDiary());

      expect(find.text('Günlüğüm'), findsOneWidget);
      expect(find.widgetWithText(ChoiceChip, '2026'), findsOneWidget);
      expect(find.widgetWithText(ChoiceChip, '2025'), findsOneWidget);
      expect(find.text('2026 yılında 3 film izlendi'), findsOneWidget);
      expect(find.text('1 yeniden izleme'), findsOneWidget);
      // Twelve localized month labels under the bars.
      final chart = find.byType(DiaryMonthChart);
      for (final month in ['Oca', 'Şub', 'Haz', 'Ağu', 'Eki', 'Ara']) {
        expect(
          find.descendant(of: chart, matching: find.text(month)),
          findsOneWidget,
        );
      }
      expect(
        find.descendant(of: chart, matching: find.text('2')),
        findsOneWidget,
      );
      // Grouped by month, newest first, with the month's total.
      expect(find.text('Ekim 2026'), findsOneWidget);
      expect(find.text('Ocak 2026'), findsOneWidget);
      expect(find.text('2 film'), findsOneWidget);
      expect(find.text('1 film'), findsOneWidget);
      expect(
        tester.getTopLeft(find.text('Ekim 2026')).dy,
        lessThan(tester.getTopLeft(find.text('Ocak 2026')).dy),
      );
      expect(find.text('Fight Club'), findsOneWidget);
      expect(find.text('Still great.'), findsOneWidget);
      expect(find.text('9/10'), findsOneWidget);
      expect(find.byTooltip('Yeniden izleme'), findsOneWidget);
      expect(find.text('12'), findsOneWidget);
      // Own diary: every entry has a menu.
      expect(find.byType(PopupMenuButton<String>), findsNWidgets(3));
    });

    testWidgets('renders the chart, totals and month groups (en)', (
      tester,
    ) async {
      await pumpDiary(tester, diary: _sampleDiary(), locale: en);

      expect(find.text('My diary'), findsOneWidget);
      expect(find.text('3 movies logged in 2026'), findsOneWidget);
      expect(find.text('1 rewatch'), findsOneWidget);
      final chart = find.byType(DiaryMonthChart);
      for (final month in ['Jan', 'Feb', 'Jun', 'Aug', 'Oct', 'Dec']) {
        expect(
          find.descendant(of: chart, matching: find.text(month)),
          findsOneWidget,
        );
      }
      expect(find.text('October 2026'), findsOneWidget);
      expect(find.text('January 2026'), findsOneWidget);
      expect(find.text('2 movies'), findsOneWidget);
      expect(find.text('1 movie'), findsOneWidget);
      expect(find.byTooltip('Rewatch'), findsOneWidget);
    });

    testWidgets('picking another year loads it (tr)', (tester) async {
      final diary = await pumpDiary(tester, diary: _sampleDiary());

      await tester.tap(find.widgetWithText(ChoiceChip, '2025'));
      await tester.pumpAndSettle();

      expect(
        diary.calls,
        containsAll(['summary u1 2025', 'entries u1 2025 1']),
      );
      expect(find.text('2025 yılında 1 film izlendi'), findsOneWidget);
      expect(find.text('Mayıs 2025'), findsOneWidget);
      expect(find.text('Parasite'), findsOneWidget);
      expect(find.text('Fight Club'), findsNothing);
    });

    testWidgets("someone else's diary has no edit or delete (en)", (
      tester,
    ) async {
      final diary = await pumpDiary(
        tester,
        diary: _sampleDiary(),
        userId: 'u2',
        locale: en,
      );

      expect(diary.calls.first, 'summary u2 null');
      expect(find.text('Film diary'), findsOneWidget);
      expect(find.text('Fight Club'), findsOneWidget);
      expect(find.byType(PopupMenuButton<String>), findsNothing);
    });

    testWidgets('editing an entry uses the sheet without a rating (en)', (
      tester,
    ) async {
      final diary = await pumpDiary(tester, diary: _sampleDiary(), locale: en);

      await tester.tap(find.byType(PopupMenuButton<String>).first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Edit'));
      await tester.pumpAndSettle();

      expect(find.text('Edit diary entry'), findsOneWidget);
      expect(find.text('Rate it too'), findsNothing);
      expect(find.text('Still great.'), findsNWidgets(2));
      await tester.enterText(
        find.descendant(
          of: find.byType(DiaryEntrySheet),
          matching: find.byType(TextField),
        ),
        'Even better the second time.',
      );
      await tester.tap(find.text('Rewatch').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(
        diary.calls,
        contains('update 3 2026-10-12 false Even better the second time.'),
      );
      expect(find.byType(DiaryEntrySheet), findsNothing);
      expect(find.text('Diary entry updated.'), findsOneWidget);
      expect(find.text('Even better the second time.'), findsOneWidget);
      expect(find.byTooltip('Rewatch'), findsNothing);
    });

    testWidgets('deleting asks first, then removes the entry (tr)', (
      tester,
    ) async {
      final diary = await pumpDiary(tester, diary: _sampleDiary());

      await tester.tap(find.byType(PopupMenuButton<String>).first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Sil'));
      await tester.pumpAndSettle();

      expect(find.text('Bu günlük kaydı silinsin mi?'), findsOneWidget);
      expect(
        find.text(
          '"Fight Club" filminin 12 Ekim 2026 tarihli kaydı günlüğünden '
          'silinecek. Puanın olduğu gibi kalır.',
        ),
        findsOneWidget,
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Sil'));
      await tester.pumpAndSettle();

      expect(diary.calls, contains('delete 3'));
      expect(find.text('Fight Club'), findsNothing);
      expect(find.text('Arrival'), findsOneWidget);
      expect(find.text('Günlük kaydı silindi.'), findsOneWidget);
    });

    testWidgets("a private diary shows the server's message (tr)", (
      tester,
    ) async {
      final diary = FakeDiary()
        ..error = ApiException(
          'Bu kullanıcının profili gizli.',
          statusCode: 403,
        );
      await pumpDiary(tester, diary: diary, userId: 'u2');

      expect(find.text('Bu kullanıcının profili gizli.'), findsOneWidget);
      expect(find.byIcon(Icons.lock_outline_rounded), findsOneWidget);
      // Retrying would not help.
      expect(find.byIcon(Icons.refresh_rounded), findsNothing);
    });

    testWidgets('my empty diary explains how to log (tr)', (tester) async {
      await pumpDiary(tester, diary: FakeDiary());
      expect(
        find.text(
          'Günlüğün boş. İzlediğin filmleri ve ne zaman izlediğini film '
          'sayfasındaki "Günlüğe ekle" düğmesiyle kaydedebilirsin.',
        ),
        findsOneWidget,
      );
      expect(find.byType(DiaryMonthChart), findsNothing);
      expect(find.byType(ChoiceChip), findsNothing);
    });

    testWidgets("someone else's empty diary (en)", (tester) async {
      await pumpDiary(tester, diary: FakeDiary(), userId: 'u2', locale: en);
      expect(find.text('Nothing logged in this diary yet.'), findsOneWidget);
    });

    testWidgets('more entries load when scrolling down (en)', (tester) async {
      final diary = FakeDiary()
        ..summaries[2026] = DiarySummary(
          year: 2026,
          totalCount: 25,
          monthCounts: _months({3: 25}),
          availableYears: const [2026],
        )
        ..entriesByYear[2026] = [
          for (var i = 25; i >= 1; i--)
            _entry(i, 'Movie $i', DateTime(2026, 3, i)),
        ];
      await pumpDiary(tester, diary: diary, locale: en);
      expect(diary.calls, isNot(contains('entries u1 2026 2')));

      await tester.scrollUntilVisible(
        find.text('Movie 1'),
        300,
        scrollable: find.byWidgetPredicate(
          (w) => w is Scrollable && w.axisDirection == AxisDirection.down,
        ),
      );
      await tester.pumpAndSettle();

      expect(diary.calls, contains('entries u1 2026 2'));
      expect(find.text('Movie 1'), findsOneWidget);
      expect(find.text('Load more'), findsNothing);
    });
  });
}
