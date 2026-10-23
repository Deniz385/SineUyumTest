import 'dart:async';

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
import 'package:sine_uyum_mobile/data/models/watchlist.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/data/repositories/watchlist_repository.dart';
import 'package:sine_uyum_mobile/features/profile/profile_page.dart';
import 'package:sine_uyum_mobile/features/watchlists/discover_lists_page.dart';
import 'package:sine_uyum_mobile/features/watchlists/public_watchlist_page.dart';
import 'package:sine_uyum_mobile/features/watchlists/watchlist_detail_page.dart';
import 'package:sine_uyum_mobile/features/watchlists/watchlists_page.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';
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

typedef LikeResult = ({bool liked, int likeCount});
typedef SaveResult = ({bool saved, int saveCount});

class FakeLists implements WatchlistRepository {
  List<WatchlistSummary> lists = const [];
  final discoverCalls = <(DiscoverSort, String, int)>[];
  WatchlistCardPage Function(DiscoverSort sort, String query, int page)
  onDiscover = (_, _, _) => const WatchlistCardPage();

  List<WatchlistCard> savedCards = const [];
  int savedCalls = 0;

  WatchlistCardPage userPage = const WatchlistCardPage();
  Object? userListsError;
  final userListsCalls = <String>[];

  Object? detailError;
  WatchlistDetail? publicToReturn;

  Future<LikeResult> Function(int listId, bool liked)? onLike;
  Future<SaveResult> Function(int listId, bool saved)? onSave;
  final reactionCalls = <String>[];

  @override
  Future<List<WatchlistSummary>> myLists() async => lists;

  @override
  Future<List<WatchlistInvitation>> invitations() async => const [];

  @override
  Future<WatchlistCardPage> discover({
    DiscoverSort sort = DiscoverSort.trending,
    String? query,
    int page = 1,
  }) async {
    discoverCalls.add((sort, query ?? '', page));
    return onDiscover(sort, query ?? '', page);
  }

  @override
  Future<WatchlistCardPage> saved({int page = 1}) async {
    savedCalls++;
    return WatchlistCardPage(items: savedCards);
  }

  @override
  Future<WatchlistCardPage> userLists(String userId, {int page = 1}) async {
    userListsCalls.add(userId);
    if (userListsError != null) throw userListsError!;
    return userPage;
  }

  @override
  Future<WatchlistDetail> detail(int listId) async {
    if (detailError != null) throw detailError!;
    throw StateError('no detail');
  }

  @override
  Future<WatchlistDetail> publicDetail(int listId) async => publicToReturn!;

  @override
  Future<LikeResult> setLiked(int listId, bool liked) {
    reactionCalls.add('${liked ? 'like' : 'unlike'} $listId');
    return onLike!(listId, liked);
  }

  @override
  Future<SaveResult> setSaved(int listId, bool saved) {
    reactionCalls.add('${saved ? 'save' : 'unsave'} $listId');
    return onSave!(listId, saved);
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeSocial implements SocialRepository {
  @override
  Future<UserProfile> profile(String userId) async => UserProfile(
    id: userId,
    userName: 'mehmet',
    statistics: const ProfileStatistics(),
  );

  @override
  Future<FollowStatus> followStatus(String userId) async =>
      const FollowStatus();

  @override
  Future<CompatibilityResult> compatibility(String userId) async =>
      const CompatibilityResult(compatibilityScore: 80);

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

const _mehmet = Author(id: 'u2', userName: 'mehmet');

WatchlistCard _card(
  int id,
  String name, {
  Author owner = _mehmet,
  int itemCount = 5,
  int likeCount = 3,
  int saveCount = 7,
  bool liked = false,
  bool saved = false,
  double? compatibility,
}) => WatchlistCard(
  id: id,
  name: name,
  owner: owner,
  itemCount: itemCount,
  likeCount: likeCount,
  saveCount: saveCount,
  likedByMe: liked,
  savedByMe: saved,
  posters: const ['/a.jpg', null],
  ownerCompatibility: compatibility,
);

Map<String, Object?> _cardJson(int id) => {
  'id': id,
  'name': 'Horror nights',
  'description': 'Scary ones',
  'owner': {'id': 'u2', 'userName': 'mehmet', 'profileImageUrl': null},
  'itemCount': 5,
  'likeCount': 3,
  'saveCount': 2,
  'likedByMe': true,
  'savedByMe': false,
  'posters': ['/a.jpg', null, '/c.jpg'],
  'publishedAt': '2026-10-20T08:00:00Z',
  'ownerCompatibility': 82.5,
};

void main() {
  tearDown(() => L10n.locale = en);

  group('models', () {
    test('a card reads the owner, counts, posters and compatibility', () {
      final card = WatchlistCard.fromJson({
        ..._cardJson(4),
        'posters': ['/a.jpg', null, '', '/d.jpg', '/e.jpg'],
      });
      expect(card.id, 4);
      expect(card.owner.userName, 'mehmet');
      expect(card.itemCount, 5);
      expect(card.likeCount, 3);
      expect(card.saveCount, 2);
      expect(card.likedByMe, isTrue);
      expect(card.savedByMe, isFalse);
      // At most four; an empty path is a missing poster.
      expect(card.posters, ['/a.jpg', null, null, '/d.jpg']);
      expect(card.publishedAt, DateTime.utc(2026, 10, 20, 8));
      expect(card.ownerCompatibility, 82.5);

      final sparse = WatchlistCard.fromJson({
        'id': 5,
        'owner': {'id': 'u3', 'userName': 'can'},
        'ownerCompatibility': null,
      });
      expect(sparse.ownerCompatibility, isNull);
      expect(sparse.posters, isEmpty);
      expect(sparse.publishedAt, isNull);
    });

    test('a page reads its items and hasMore', () {
      final page = WatchlistCardPage.fromJson({
        'items': [_cardJson(1), _cardJson(2)],
        'page': 2,
        'hasMore': true,
      });
      expect(page.items.map((c) => c.id), [1, 2]);
      expect(page.page, 2);
      expect(page.hasMore, isTrue);
    });

    test('visibility and counts on my lists; older responses are private', () {
      final summary = WatchlistSummary.fromJson({
        'id': 1,
        'name': 'Mine',
        'visibility': 'Public',
        'likeCount': 4,
        'saveCount': 2,
      });
      expect(summary.isPublic, isTrue);
      expect(summary.likeCount, 4);
      expect(summary.saveCount, 2);
      expect(WatchlistSummary.fromJson({'id': 1}).isPublic, isFalse);
      expect(
        WatchlistVisibility.fromJson('Private'),
        WatchlistVisibility.private,
      );
      expect(
        WatchlistVisibility.fromJson('nonsense'),
        WatchlistVisibility.private,
      );
    });

    test('the read-only view reads likes, saves and canEdit', () {
      final list = WatchlistDetail.fromJson({
        'id': 7,
        'name': 'Fridays',
        'ownerUsername': 'mehmet',
        'owner': {'id': 'u2', 'userName': 'mehmet'},
        'visibility': 'Public',
        'likeCount': 4,
        'saveCount': 2,
        'likedByMe': true,
        'savedByMe': true,
        'canEdit': false,
        'items': [
          {'movieId': 550, 'title': 'Fight Club'},
        ],
      });
      expect(list.isPublic, isTrue);
      expect(list.likedByMe, isTrue);
      expect(list.savedByMe, isTrue);
      expect(list.canEdit, isFalse);
      final reaction = ListReaction.ofDetail(list);
      expect(
        reaction,
        const ListReaction(
          liked: true,
          likeCount: 4,
          saved: true,
          saveCount: 2,
        ),
      );
    });
  });

  group('repository (real ApiClient)', () {
    late FakeServer server;
    late ProviderContainer container;
    late WatchlistRepository repository;

    setUp(() async {
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
      await container.read(responseCacheProvider).useScope('u1');
      repository = container.read(watchlistRepositoryProvider);
    });

    test('discover sends the sort, a trimmed search and the page', () async {
      server.routes['GET /api/v1/watchlist/discover'] = (
        200,
        {
          'items': [_cardJson(1)],
          'page': 1,
          'hasMore': true,
        },
      );
      final first = await repository.discover();
      await repository.discover(
        sort: DiscoverSort.newest,
        query: '  korku ',
        page: 2,
      );
      await repository.discover(sort: DiscoverSort.forYou, query: '   ');
      await repository.discover(sort: DiscoverSort.popular);

      expect(first.items.single.name, 'Horror nights');
      expect(first.hasMore, isTrue);
      expect(server.queries, [
        {'sort': 'trending', 'page': 1, 'pageSize': 20},
        {'sort': 'new', 'q': 'korku', 'page': 2, 'pageSize': 20},
        {'sort': 'forYou', 'page': 1, 'pageSize': 20},
        {'sort': 'popular', 'page': 1, 'pageSize': 20},
      ]);
    });

    test(
      'user lists, saved lists, like and save hit their endpoints',
      () async {
        final page = {'items': <Object>[], 'page': 1, 'hasMore': false};
        server.routes['GET /api/v1/watchlist/user/u2'] = (200, page);
        server.routes['GET /api/v1/watchlist/saved'] = (200, page);
        server.routes['POST /api/v1/watchlist/7/like'] = (
          200,
          {'liked': true, 'likeCount': 5},
        );
        server.routes['DELETE /api/v1/watchlist/7/like'] = (
          200,
          {'liked': false, 'likeCount': 4},
        );
        server.routes['POST /api/v1/watchlist/7/save'] = (
          200,
          {'saved': true, 'saveCount': 3},
        );
        server.routes['DELETE /api/v1/watchlist/7/save'] = (
          200,
          {'saved': false, 'saveCount': 2},
        );

        await repository.userLists('u2', page: 2);
        await repository.saved();
        expect(await repository.setLiked(7, true), (liked: true, likeCount: 5));
        expect(await repository.setLiked(7, false), (
          liked: false,
          likeCount: 4,
        ));
        expect(await repository.setSaved(7, true), (saved: true, saveCount: 3));
        expect(await repository.setSaved(7, false), (
          saved: false,
          saveCount: 2,
        ));

        expect(server.requests, [
          'GET /api/v1/watchlist/user/u2',
          'GET /api/v1/watchlist/saved',
          'POST /api/v1/watchlist/7/like',
          'DELETE /api/v1/watchlist/7/like',
          'POST /api/v1/watchlist/7/save',
          'DELETE /api/v1/watchlist/7/save',
        ]);
        expect(server.queries.first, {'page': 2, 'pageSize': 20});
      },
    );

    test('create sends the visibility; update only when given', () async {
      server.routes['POST /api/v1/watchlist'] = (201, {'id': 9, 'name': 'A'});
      server.routes['PUT /api/v1/watchlist/9'] = (200, {'message': 'ok'});

      await repository.create(name: 'A');
      await repository.create(
        name: 'B',
        description: 'd',
        visibility: WatchlistVisibility.public,
      );
      await repository.update(9, name: 'A');
      await repository.update(
        9,
        name: 'A',
        visibility: WatchlistVisibility.private,
      );

      expect(server.bodies, [
        {'name': 'A', 'description': null, 'visibility': 'Private'},
        {'name': 'B', 'description': 'd', 'visibility': 'Public'},
        {'name': 'A', 'description': null},
        {'name': 'A', 'description': null, 'visibility': 'Private'},
      ]);
    });

    test(
      'saved and profile lists are kept offline; discovery is not',
      () async {
        expect(ResponseCache.isCacheable('/api/v1/watchlist/saved'), isTrue);
        expect(ResponseCache.isCacheable('/api/v1/watchlist/user/u2'), isTrue);
        expect(ResponseCache.isCacheable('/api/v1/watchlist/public/7'), isTrue);
        expect(
          ResponseCache.isCacheable('/api/v1/watchlist/discover'),
          isFalse,
        );

        server.routes['GET /api/v1/watchlist/saved'] = (
          200,
          {
            'items': [_cardJson(1)],
            'page': 1,
            'hasMore': false,
          },
        );
        await repository.saved();
        await pumpEventQueue();
        server.offline = true;
        expect((await repository.saved()).items.single.id, 1);
      },
    );

    test(
      'a 403 on a profile\'s lists is not masked by an older copy',
      () async {
        server.routes['GET /api/v1/watchlist/user/u2'] = (
          200,
          {
            'items': [_cardJson(1)],
            'page': 1,
            'hasMore': false,
          },
        );
        await repository.userLists('u2');
        await pumpEventQueue();
        server.routes['GET /api/v1/watchlist/user/u2'] = (
          403,
          {'title': 'This profile is private.'},
        );
        await expectLater(
          repository.userLists('u2'),
          throwsA(
            isA<ApiException>().having((e) => e.statusCode, 'status', 403),
          ),
        );
      },
    );
  });

  group('widgets', () {
    Future<void> pump(
      WidgetTester tester,
      Widget page, {
      required FakeLists lists,
      Locale locale = tr,
    }) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            watchlistRepositoryProvider.overrideWithValue(lists),
            socialRepositoryProvider.overrideWithValue(FakeSocial()),
            sessionProvider.overrideWith(FakeSession.new),
          ],
          child: localizedApp(locale: locale, home: page),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('discover: tabs, debounced search, load more and empty '
        'states (tr)', (tester) async {
      final lists = FakeLists()
        ..onDiscover = (sort, query, page) => switch ((sort, query, page)) {
          (DiscoverSort.trending, '', 1) => WatchlistCardPage(
            items: [
              _card(1, 'Korku gecesi', compatibility: 82),
              _card(2, 'Kült klasikler'),
            ],
            hasMore: true,
          ),
          (DiscoverSort.trending, '', 2) => WatchlistCardPage(
            items: [_card(2, 'Kült klasikler'), _card(3, 'Yağmurlu pazar')],
            page: 2,
          ),
          (DiscoverSort.popular, '', _) => WatchlistCardPage(
            items: [_card(4, 'Herkesin favorisi')],
          ),
          (_, 'korku', _) => WatchlistCardPage(
            items: [_card(1, 'Korku gecesi')],
          ),
          _ => const WatchlistCardPage(),
        };
      await pump(tester, const DiscoverListsPage(), lists: lists);

      expect(find.text('Liste keşfet'), findsOneWidget);
      expect(find.text('Korku gecesi'), findsOneWidget);
      expect(find.text('@mehmet'), findsWidgets);
      expect(find.text('5 film'), findsWidgets);
      expect(find.text('%82 uyumlu'), findsOneWidget);
      // The end of the first page loads the next one (a repeated list is
      // not shown twice).
      expect(lists.discoverCalls, [
        (DiscoverSort.trending, '', 1),
        (DiscoverSort.trending, '', 2),
      ]);
      expect(find.text('Yağmurlu pazar', skipOffstage: false), findsOneWidget);
      expect(find.text('Kült klasikler', skipOffstage: false), findsOneWidget);

      await tester.tap(find.text('Popüler'));
      await tester.pumpAndSettle();
      expect(find.text('Herkesin favorisi'), findsOneWidget);
      expect(find.text('Korku gecesi'), findsNothing);

      await tester.tap(find.text('Sana özel'));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Daha çok film puanla, zevkine yakın kişilerin listelerini senin '
          'için bulalım.',
        ),
        findsOneWidget,
      );

      await tester.tap(find.text('Yeni'));
      await tester.pumpAndSettle();
      expect(
        find.text('Son zamanlarda herkese açılan bir liste yok.'),
        findsOneWidget,
      );

      await tester.tap(find.text('Bu hafta'));
      await tester.pumpAndSettle();
      lists.discoverCalls.clear();
      await tester.enterText(find.byType(TextField), 'kor');
      await tester.pump(const Duration(milliseconds: 200));
      await tester.enterText(find.byType(TextField), 'korku');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();
      // Debounced: only the last text is searched.
      expect(lists.discoverCalls, [(DiscoverSort.trending, 'korku', 1)]);
      expect(find.text('Korku gecesi'), findsOneWidget);
      expect(find.text('Kült klasikler'), findsNothing);

      await tester.enterText(find.byType(TextField), 'xyz');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();
      expect(
        find.text('"xyz" ile eşleşen bir liste bulamadık.'),
        findsOneWidget,
      );
    });

    testWidgets('discover: like and save update at once and roll back on '
        'error (en)', (tester) async {
      final lists = FakeLists()
        ..onDiscover = (_, _, _) =>
            WatchlistCardPage(items: [_card(1, 'Horror nights')]);
      await pump(tester, const DiscoverListsPage(), lists: lists, locale: en);

      expect(find.text('This week'), findsOneWidget);
      expect(find.text('3'), findsOneWidget);
      expect(find.text('7'), findsOneWidget);

      // Fails: the like is shown right away, then taken back.
      var pending = Completer<LikeResult>();
      lists.onLike = (_, _) => pending.future;
      await tester.tap(find.byTooltip('Like'));
      await tester.pump();
      expect(find.text('4'), findsOneWidget);
      expect(find.byTooltip('Remove like'), findsOneWidget);
      pending.completeError(ApiException('Slow down a little.'));
      await tester.pumpAndSettle();
      expect(find.text('3'), findsOneWidget);
      expect(find.byTooltip('Like'), findsOneWidget);
      expect(find.text('Slow down a little.'), findsOneWidget);

      // Succeeds: the server's count wins.
      pending = Completer<LikeResult>();
      lists.onLike = (_, _) => pending.future;
      await tester.tap(find.byTooltip('Like'));
      await tester.pump();
      expect(find.text('4'), findsOneWidget);
      pending.complete((liked: true, likeCount: 10));
      await tester.pumpAndSettle();
      expect(find.text('10'), findsOneWidget);
      expect(find.byTooltip('Remove like'), findsOneWidget);

      lists.onSave = (_, _) async => (saved: true, saveCount: 8);
      await tester.tap(find.byTooltip('Save list'));
      await tester.pumpAndSettle();
      expect(find.text('8'), findsOneWidget);
      expect(find.byTooltip('Remove from saved'), findsOneWidget);
      expect(lists.reactionCalls, ['like 1', 'like 1', 'save 1']);
    });

    testWidgets('a list I am not a member of opens read-only (tr)', (
      tester,
    ) async {
      final lists = FakeLists()
        ..detailError = ApiException(null, statusCode: 404)
        ..publicToReturn = const WatchlistDetail(
          id: 7,
          name: 'Cuma klasikleri',
          description: 'Her cuma bir klasik.',
          ownerUsername: 'mehmet',
          owner: _mehmet,
          visibility: WatchlistVisibility.public,
          likeCount: 4,
          saveCount: 2,
          items: [WatchlistItem(movieId: 550, title: 'Dövüş Kulübü')],
        )
        ..onSave = (_, _) async => (saved: true, saveCount: 3);
      final router = GoRouter(
        initialLocation: '/watchlist/7',
        routes: [
          GoRoute(
            path: '/watchlist/:listId',
            builder: (_, state) => WatchlistDetailPage(
              listId: int.parse(state.pathParameters['listId']!),
            ),
          ),
          GoRoute(
            path: '/movie/:movieId',
            builder: (_, state) =>
                Text('movie ${state.pathParameters['movieId']}'),
          ),
        ],
      );
      addTearDown(router.dispose);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            watchlistRepositoryProvider.overrideWithValue(lists),
            sessionProvider.overrideWith(FakeSession.new),
          ],
          child: localizedRouterApp(routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Cuma klasikleri'), findsOneWidget);
      expect(find.text('@mehmet'), findsOneWidget);
      expect(find.text('Her cuma bir klasik.'), findsOneWidget);
      expect(find.text('1 film'), findsOneWidget);
      // Nothing to edit: no remove buttons, members or filters.
      expect(find.byTooltip('Listeden kaldır'), findsNothing);
      expect(find.byTooltip('Üyeler'), findsNothing);
      expect(find.byTooltip('Beğen'), findsOneWidget);

      await tester.tap(find.byTooltip('Listeyi kaydet'));
      await tester.pumpAndSettle();
      expect(find.text('3'), findsOneWidget);
      expect(find.byTooltip('Kaydettiklerinden çıkar'), findsOneWidget);

      await tester.tap(find.text('Dövüş Kulübü'));
      await tester.pumpAndSettle();
      expect(find.text('movie 550'), findsOneWidget);
    });

    testWidgets('a private list sent in a message has no like/save (en)', (
      tester,
    ) async {
      final lists = FakeLists()
        ..publicToReturn = const WatchlistDetail(
          id: 8,
          name: 'For you',
          owner: _mehmet,
        );
      await pump(
        tester,
        const PublicWatchlistPage(listId: 8),
        lists: lists,
        locale: en,
      );

      expect(find.text('@mehmet'), findsOneWidget);
      expect(find.text('This list has no movies yet.'), findsOneWidget);
      expect(find.byTooltip('Like'), findsNothing);
      expect(find.byTooltip('Save list'), findsNothing);
    });

    testWidgets('profile: public lists are hidden on 403 (tr)', (tester) async {
      final lists = FakeLists()
        ..userListsError = ApiException(null, statusCode: 403);
      await pump(tester, const ProfilePage(userId: 'u2'), lists: lists);
      expect(find.textContaining('Puanladığı filmler'), findsOneWidget);

      // The lists live in the profile's Lists tab.
      await tester.tap(find.widgetWithText(Tab, 'Listeler'));
      await tester.pumpAndSettle();
      expect(lists.userListsCalls, ['u2']);
      expect(find.text('Herkese açık listeler'), findsNothing);
    });

    testWidgets('profile: public lists in a carousel (en)', (tester) async {
      final lists = FakeLists()
        ..userPage = WatchlistCardPage(
          items: [
            _card(1, 'Horror nights'),
            _card(2, 'Cult classics', itemCount: 1, likeCount: 1),
          ],
        );
      await pump(
        tester,
        const ProfilePage(userId: 'u2'),
        lists: lists,
        locale: en,
      );
      await tester.tap(find.widgetWithText(Tab, 'Lists'));
      await tester.pumpAndSettle();

      expect(find.text('Public lists'), findsOneWidget);
      expect(find.text('Horror nights'), findsOneWidget);
      expect(find.text('5 movies · 3 likes'), findsOneWidget);
      expect(find.text('1 movie · 1 like'), findsOneWidget);
    });

    testWidgets('lists tab: my public list is badged; unsaving removes a '
        'saved list (tr)', (tester) async {
      final lists = FakeLists()
        ..lists = const [
          WatchlistSummary(
            id: 1,
            name: 'Benim listem',
            visibility: WatchlistVisibility.public,
          ),
        ]
        ..savedCards = [
          _card(10, 'Korku gecesi', saved: true),
          _card(11, 'Kült klasikler', saved: true),
        ]
        ..onSave = (_, _) async => (saved: false, saveCount: 6);
      await pump(tester, const WatchlistsPage(), lists: lists);

      expect(find.text('Liste keşfet'), findsOneWidget);
      expect(find.text('Herkese açık'), findsOneWidget);
      expect(find.text('Kaydettiğin listeler'), findsOneWidget);
      expect(find.text('@mehmet · 5 film'), findsNWidgets(2));
      expect(lists.savedCalls, 1);

      lists.savedCards = [_card(11, 'Kült klasikler', saved: true)];
      await tester.tap(find.byTooltip('Kaydettiklerinden çıkar').first);
      await tester.pumpAndSettle();

      expect(lists.reactionCalls, ['unsave 10']);
      expect(find.text('Korku gecesi'), findsNothing);
      expect(find.text('Kült klasikler'), findsOneWidget);
      // Refreshed after the change.
      expect(lists.savedCalls, 2);
    });

    testWidgets('lists tab: a failed unsave puts the list back (en)', (
      tester,
    ) async {
      final pending = Completer<SaveResult>();
      final lists = FakeLists()
        ..savedCards = [_card(10, 'Horror nights', saved: true)]
        ..onSave = (_, _) => pending.future;
      await pump(tester, const WatchlistsPage(), lists: lists, locale: en);

      expect(find.text('Saved lists'), findsOneWidget);
      await tester.tap(find.byTooltip('Remove from saved'));
      await tester.pump();
      expect(find.text('Horror nights'), findsNothing);

      pending.completeError(ApiException(null, statusCode: 404));
      await tester.pumpAndSettle();
      expect(find.text('Horror nights'), findsOneWidget);
      expect(find.text('Not found.'), findsOneWidget);
      expect(lists.savedCalls, 1);
    });
  });

  group('visibility in the list editor (real ApiClient)', () {
    Future<FakeServer> pumpLists(WidgetTester tester, Locale locale) async {
      final server = FakeServer()
        ..routes['GET /api/v1/watchlist'] = (
          200,
          [
            {
              'id': 5,
              'name': 'Horror',
              'itemCount': 2,
              'isOwner': true,
              'visibility': 'Public',
              'likeCount': 2,
              'saveCount': 1,
            },
          ],
        )
        ..routes['GET /api/v1/watchlist/invitations'] = (200, <Object>[])
        ..routes['GET /api/v1/watchlist/saved'] = (
          200,
          {'items': <Object>[], 'page': 1, 'hasMore': false},
        )
        ..routes['POST /api/v1/watchlist'] = (201, {'id': 6, 'name': 'x'})
        ..routes['PUT /api/v1/watchlist/5'] = (200, {'message': 'ok'});
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
          child: localizedApp(locale: locale, home: const WatchlistsPage()),
        ),
      );
      await tester.pumpAndSettle();
      return server;
    }

    Object? bodyOf(FakeServer server, String request) =>
        server.bodies[server.requests.lastIndexOf(request)];

    testWidgets('creating a public list sends "Public" (tr)', (tester) async {
      final server = await pumpLists(tester, tr);
      expect(find.text('Herkese açık'), findsOneWidget);

      await tester.tap(find.byType(FloatingActionButton));
      await tester.pumpAndSettle();
      expect(
        find.text('Sadece sen ve davet ettiğin kişiler görebilir.'),
        findsOneWidget,
      );
      await tester.enterText(find.byType(TextFormField).first, 'Cuma');
      await tester.tap(find.byType(Switch));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Profilini görebilen herkes bu listeyi bulabilir, beğenip '
          'kaydedebilir.',
        ),
        findsOneWidget,
      );
      await tester.tap(find.text('Kaydet'));
      await tester.pumpAndSettle();

      expect(bodyOf(server, 'POST /api/v1/watchlist'), {
        'name': 'Cuma',
        'description': '',
        'visibility': 'Public',
      });
      // My lists are reloaded.
      expect(
        server.requests.where((r) => r == 'GET /api/v1/watchlist'),
        hasLength(2),
      );
    });

    testWidgets('making a public list private sends "Private" (en)', (
      tester,
    ) async {
      final server = await pumpLists(tester, en);

      await tester.tap(find.byType(PopupMenuButton<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Edit'));
      await tester.pumpAndSettle();
      expect(tester.widget<Switch>(find.byType(Switch)).value, isTrue);
      expect(
        find.text(
          'Anyone who can see your profile can find, like and save it.',
        ),
        findsOneWidget,
      );
      await tester.tap(find.byType(Switch));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(bodyOf(server, 'PUT /api/v1/watchlist/5'), {
        'name': 'Horror',
        'description': '',
        'visibility': 'Private',
      });
    });
  });
}
