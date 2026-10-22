import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/core/offline/response_cache.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/social.dart';
import 'package:sine_uyum_mobile/data/models/watchlist.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/data/repositories/watchlist_repository.dart';
import 'package:sine_uyum_mobile/features/watchlists/watchlist_detail_page.dart';
import 'package:sine_uyum_mobile/features/watchlists/watchlists_page.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';
import 'package:sine_uyum_mobile/providers/watchlist_provider.dart';

import 'support/fake_network.dart';
import 'support/localized_app.dart';

class FakeSession extends SessionNotifier {
  @override
  SessionState build() => const SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(id: 'u1', userName: 'ayse'),
  );
}

class FakeWatchlists implements WatchlistRepository {
  List<WatchlistSummary> lists = const [];
  List<WatchlistSummary>? listsAfterAccept;
  List<WatchlistInvitation> pending = const [];
  WatchlistDetail? detailToReturn;
  final calls = <String>[];
  int myListsCalls = 0;

  @override
  Future<List<WatchlistSummary>> myLists() async {
    myListsCalls++;
    return lists;
  }

  @override
  Future<List<WatchlistInvitation>> invitations() async => pending;

  @override
  Future<void> accept(int listId) async {
    calls.add('accept $listId');
    pending = pending.where((i) => i.listId != listId).toList();
    lists = listsAfterAccept ?? lists;
  }

  @override
  Future<void> removeMember(int listId, String memberId) async {
    calls.add('remove $listId $memberId');
    pending = pending.where((i) => i.listId != listId).toList();
  }

  @override
  Future<WatchlistMember> invite(int listId, String userId) async {
    calls.add('invite $listId $userId');
    return WatchlistMember(
      user: Author(id: userId, userName: 'zeynep'),
    );
  }

  @override
  Future<WatchlistDetail> detail(int listId) async => detailToReturn!;

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeSocial implements SocialRepository {
  final queries = <String>[];

  @override
  Future<List<UserSearchResult>> searchUsers(String query) async {
    queries.add(query);
    return const [
      UserSearchResult(id: 'u9', userName: 'zeynep'),
      UserSearchResult(id: 'u2', userName: 'mehmet'),
    ];
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

const _owner = Author(id: 'u1', userName: 'ayse');
const _mehmet = Author(id: 'u2', userName: 'mehmet');

WatchlistDetail _detail({
  bool isOwner = true,
  List<WatchlistMember> members = const [
    WatchlistMember(user: _mehmet, accepted: true),
  ],
  List<WatchlistItem> items = const [],
  Author owner = _owner,
}) => WatchlistDetail(
  id: 7,
  name: 'Friday nights',
  isOwner: isOwner,
  owner: owner,
  members: members,
  items: items,
);

void main() {
  tearDown(() => L10n.locale = en);

  group('models', () {
    test('summary reads the sharing fields; an older response is my own '
        'unshared list', () {
      final shared = WatchlistSummary.fromJson({
        'id': 3,
        'name': 'Horror',
        'itemCount': 4,
        'isShared': true,
        'isOwner': false,
        'ownerUsername': 'mehmet',
        'memberCount': 3,
      });
      expect(shared.isShared, isTrue);
      expect(shared.isOwner, isFalse);
      expect(shared.ownerUsername, 'mehmet');
      expect(shared.memberCount, 3);

      final old = WatchlistSummary.fromJson({'id': 1, 'name': 'Mine'});
      expect(old.isShared, isFalse);
      expect(old.isOwner, isTrue);
      expect(old.memberCount, 1);
    });

    test('detail reads owner, members and per-item sharing info', () {
      final detail = WatchlistDetail.fromJson({
        'id': 7,
        'name': 'Friday nights',
        'isOwner': true,
        'owner': {'id': 'u1', 'userName': 'ayse', 'profileImageUrl': '/a.jpg'},
        'members': [
          {
            'user': {'id': 'u2', 'userName': 'mehmet'},
            'accepted': true,
          },
          {
            'user': {'id': 'u3', 'userName': 'zeynep'},
            'accepted': false,
          },
        ],
        'items': [
          {
            'movieId': 550,
            'title': 'Fight Club',
            'addedAt': '2026-10-01T12:00:00Z',
            'addedByUsername': 'mehmet',
            'watchedBy': ['ayse', 'mehmet'],
          },
          {'movieId': 13, 'title': 'Forrest Gump'},
        ],
      });

      expect(detail.owner!.imageUrl, '/a.jpg');
      expect(detail.members, hasLength(2));
      expect(detail.members.last.accepted, isFalse);
      expect(detail.isShared, isTrue);
      expect(detail.people.map((p) => p.userName), ['ayse', 'mehmet']);
      expect(detail.items.first.addedByUsername, 'mehmet');
      expect(detail.items.first.watchedBy, ['ayse', 'mehmet']);
      expect(detail.items.last.addedByUsername, isNull);
      expect(detail.items.last.watchedBy, isEmpty);
    });

    test('a list with only pending invitations is not shared yet; a member '
        'always sees it as shared', () {
      expect(
        _detail(members: const [WatchlistMember(user: _mehmet)]).isShared,
        isFalse,
      );
      expect(_detail(isOwner: false, members: const []).isShared, isTrue);
    });

    test('invitation parses the owner and counts', () {
      final invitation = WatchlistInvitation.fromJson({
        'listId': 7,
        'name': 'Friday nights',
        'owner': {'id': 'u2', 'userName': 'mehmet'},
        'itemCount': 5,
        'invitedAt': '2026-10-20T08:00:00Z',
      });
      expect(invitation.listId, 7);
      expect(invitation.owner.userName, 'mehmet');
      expect(invitation.itemCount, 5);
      expect(invitation.invitedAt, DateTime.utc(2026, 10, 20, 8));
    });
  });

  group('repository (real ApiClient)', () {
    late FakeServer server;
    late ProviderContainer container;

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
    });

    test('invite posts the user id to the members endpoint', () async {
      server.routes['POST /api/v1/watchlist/7/members'] = (
        200,
        {
          'user': {'id': 'u9', 'userName': 'zeynep'},
          'accepted': false,
        },
      );
      final member = await container
          .read(watchlistRepositoryProvider)
          .invite(7, 'u9');
      expect(server.requests, ['POST /api/v1/watchlist/7/members']);
      expect(server.bodies.single, {'userId': 'u9'});
      expect(member.user.userName, 'zeynep');
      expect(member.accepted, isFalse);
    });

    test('accept and decline hit the accept / own member endpoints', () async {
      server.routes['GET /api/v1/watchlist/invitations'] = (
        200,
        [
          {
            'listId': 7,
            'name': 'A',
            'owner': {'id': 'u2', 'userName': 'mehmet'},
          },
          {
            'listId': 8,
            'name': 'B',
            'owner': {'id': 'u2', 'userName': 'mehmet'},
          },
        ],
      );
      server.routes['POST /api/v1/watchlist/7/accept'] = (
        200,
        {'message': 'ok'},
      );
      server.routes['DELETE /api/v1/watchlist/8/members/u1'] = (
        200,
        {'message': 'ok'},
      );

      final notifier = container.read(watchlistInvitationsProvider.notifier);
      expect(
        await container.read(watchlistInvitationsProvider.future),
        hasLength(2),
      );
      await notifier.accept(7);
      await notifier.decline(8);

      expect(server.requests, [
        'GET /api/v1/watchlist/invitations',
        'POST /api/v1/watchlist/7/accept',
        'DELETE /api/v1/watchlist/8/members/u1',
      ]);
      expect(container.read(watchlistInvitationsProvider).value, isEmpty);
    });

    test('leaving deletes my own membership', () async {
      server.routes['GET /api/v1/watchlist/7'] = (
        200,
        {'id': 7, 'name': 'A', 'isOwner': false},
      );
      server.routes['DELETE /api/v1/watchlist/7/members/u1'] = (
        200,
        {'message': 'ok'},
      );
      final sub = container.listen(watchlistDetailProvider(7), (_, _) {});
      addTearDown(sub.close);
      await container.read(watchlistDetailProvider(7).future);
      await container.read(watchlistDetailProvider(7).notifier).leave();
      expect(server.requests.last, 'DELETE /api/v1/watchlist/7/members/u1');
    });

    test('a server error message is passed on as-is', () async {
      server.routes['POST /api/v1/watchlist/7/members'] = (
        409,
        {'title': 'This user is already a member or invited.'},
      );
      await expectLater(
        container.read(watchlistRepositoryProvider).invite(7, 'u2'),
        throwsA(
          predicate(
            (e) => e.toString() == 'This user is already a member or invited.',
          ),
        ),
      );
    });

    test('invitations are not kept in the offline cache; lists are', () {
      expect(
        ResponseCache.isCacheable('/api/v1/watchlist/invitations'),
        isFalse,
      );
      expect(ResponseCache.isCacheable('/api/v1/watchlist'), isTrue);
      expect(ResponseCache.isCacheable('/api/v1/watchlist/7'), isTrue);
    });
  });

  group('widgets', () {
    Future<(FakeWatchlists, FakeSocial)> pump(
      WidgetTester tester,
      Widget page, {
      required FakeWatchlists watchlists,
      Locale locale = tr,
      bool pushed = false,
    }) async {
      final social = FakeSocial();
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            watchlistRepositoryProvider.overrideWithValue(watchlists),
            socialRepositoryProvider.overrideWithValue(social),
            sessionProvider.overrideWith(FakeSession.new),
          ],
          child: localizedApp(
            locale: locale,
            home: pushed
                ? Builder(
                    builder: (context) => Scaffold(
                      body: Center(
                        child: TextButton(
                          onPressed: () => Navigator.of(
                            context,
                          ).push(MaterialPageRoute<void>(builder: (_) => page)),
                          child: const Text('open'),
                        ),
                      ),
                    ),
                  )
                : page,
          ),
        ),
      );
      await tester.pumpAndSettle();
      if (pushed) {
        await tester.tap(find.text('open'));
        await tester.pumpAndSettle();
      }
      return (watchlists, social);
    }

    testWidgets('accepting an invitation removes it and refreshes the lists '
        '(tr)', (tester) async {
      final watchlists = FakeWatchlists()
        ..lists = const [WatchlistSummary(id: 1, name: 'Benim listem')]
        ..listsAfterAccept = const [
          WatchlistSummary(id: 1, name: 'Benim listem'),
          WatchlistSummary(
            id: 7,
            name: 'Cuma akşamları',
            itemCount: 5,
            isShared: true,
            isOwner: false,
            ownerUsername: 'mehmet',
            memberCount: 3,
          ),
        ]
        ..pending = const [
          WatchlistInvitation(
            listId: 7,
            name: 'Cuma akşamları',
            owner: _mehmet,
            itemCount: 5,
          ),
        ];
      await pump(tester, const WatchlistsPage(), watchlists: watchlists);

      expect(find.text('Bekleyen davet'), findsOneWidget);
      expect(find.text('@mehmet seni davet etti · 5 film'), findsOneWidget);
      expect(watchlists.myListsCalls, 1);

      await tester.tap(find.text('Kabul et'));
      await tester.pumpAndSettle();

      expect(watchlists.calls, ['accept 7']);
      expect(find.text('Bekleyen davet'), findsNothing);
      expect(find.text('Kabul et'), findsNothing);
      expect(watchlists.myListsCalls, 2);
      expect(find.text('"Cuma akşamları" listesine katıldın.'), findsOneWidget);
      // The joined list: shared badge, owner, and no rename/delete menu.
      expect(find.text('3 kişi'), findsOneWidget);
      expect(find.text('5 film · Sahibi: @mehmet'), findsOneWidget);
      expect(find.byType(PopupMenuButton<String>), findsOneWidget);
    });

    testWidgets('declining an invitation removes my membership (en)', (
      tester,
    ) async {
      final watchlists = FakeWatchlists()
        ..pending = const [
          WatchlistInvitation(listId: 7, name: 'Friday nights', owner: _mehmet),
        ];
      await pump(
        tester,
        const WatchlistsPage(),
        watchlists: watchlists,
        locale: en,
      );

      expect(find.text('@mehmet invited you · 0 movies'), findsOneWidget);
      await tester.tap(find.text('Decline'));
      await tester.pumpAndSettle();

      expect(watchlists.calls, ['remove 7 u1']);
      expect(find.text('Invitation declined.'), findsOneWidget);
      expect(find.text("You don't have any watchlists yet."), findsOneWidget);
    });

    testWidgets('the owner sees pending members and can invite (en)', (
      tester,
    ) async {
      final watchlists = FakeWatchlists()
        ..detailToReturn = _detail(
          members: const [
            WatchlistMember(user: _mehmet, accepted: true),
            WatchlistMember(
              user: Author(id: 'u3', userName: 'can'),
            ),
          ],
        );
      final (_, social) = await pump(
        tester,
        const WatchlistDetailPage(listId: 7),
        watchlists: watchlists,
        locale: en,
      );

      expect(find.text('2 people · 1 pending invitation'), findsOneWidget);
      await tester.tap(find.byTooltip('Members'));
      await tester.pumpAndSettle();

      expect(find.text('Owner'), findsOneWidget);
      expect(find.text('Invitation pending'), findsOneWidget);
      expect(find.byTooltip('Cancel invitation'), findsOneWidget);
      expect(find.byTooltip('Remove from list'), findsOneWidget);
      expect(find.text('Leave list'), findsNothing);

      await tester.enterText(find.byType(TextField), 'ze');
      await tester.enterText(find.byType(TextField), 'zey');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();
      // Debounced: only the last query is searched.
      expect(social.queries, ['zey']);
      // mehmet is already a member.
      expect(find.text('Already in the list'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Invite'));
      await tester.pumpAndSettle();

      expect(watchlists.calls, ['invite 7 u9']);
      expect(find.text('@zeynep was invited.'), findsOneWidget);
      expect(find.text('Invitation pending'), findsNWidgets(2));
    });

    testWidgets('a member cannot invite and can leave the list (tr)', (
      tester,
    ) async {
      final watchlists = FakeWatchlists()
        ..detailToReturn = _detail(
          isOwner: false,
          owner: _mehmet,
          members: const [WatchlistMember(user: _owner, accepted: true)],
        );
      await pump(
        tester,
        const WatchlistDetailPage(listId: 7),
        watchlists: watchlists,
        pushed: true,
      );

      expect(find.text('2 kişi · Sahibi: @mehmet'), findsOneWidget);
      await tester.tap(find.byTooltip('Üyeler'));
      await tester.pumpAndSettle();

      expect(find.byType(TextField), findsNothing);
      expect(find.byTooltip('Listeden çıkar'), findsNothing);

      await tester.tap(find.text('Listeden ayrıl'));
      await tester.pumpAndSettle();
      expect(
        find.text('"Friday nights" listesinden ayrılmak istiyor musun?'),
        findsOneWidget,
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Listeden ayrıl'));
      await tester.pumpAndSettle();

      expect(watchlists.calls, ['remove 7 u1']);
      expect(
        find.text('"Friday nights" listesinden ayrıldın.'),
        findsOneWidget,
      );
      // Back where the list was opened from.
      expect(find.text('open'), findsOneWidget);
    });

    testWidgets('"Unwatched by all of us" hides movies anyone watched (en)', (
      tester,
    ) async {
      final watchlists = FakeWatchlists()
        ..detailToReturn = _detail(
          items: const [
            WatchlistItem(
              movieId: 1,
              title: 'Seen one',
              addedByUsername: 'mehmet',
              watchedBy: ['ayse', 'mehmet'],
            ),
            WatchlistItem(
              movieId: 2,
              title: 'Fresh one',
              addedByUsername: 'ayse',
            ),
          ],
        );
      await pump(
        tester,
        const WatchlistDetailPage(listId: 7),
        watchlists: watchlists,
        locale: en,
      );

      expect(find.text('added by @mehmet'), findsOneWidget);
      expect(find.text('Watched by ayse, mehmet'), findsOneWidget);

      await tester.tap(find.text('Unwatched by all of us'));
      await tester.pumpAndSettle();
      expect(find.text('Seen one'), findsNothing);
      expect(find.text('Fresh one'), findsOneWidget);

      await tester.tap(find.text('Unwatched by all of us'));
      await tester.pumpAndSettle();
      expect(find.text('Seen one'), findsOneWidget);
    });

    testWidgets('a private list filters "not watched yet" (tr)', (
      tester,
    ) async {
      final watchlists = FakeWatchlists()
        ..detailToReturn = _detail(
          members: const [],
          items: const [
            WatchlistItem(
              movieId: 1,
              title: 'İzlenen',
              addedByUsername: 'ayse',
              watchedBy: ['ayse'],
            ),
          ],
        );
      await pump(
        tester,
        const WatchlistDetailPage(listId: 7),
        watchlists: watchlists,
      );

      // Not shared: no "added by", no members header.
      expect(find.text('ekleyen: @ayse'), findsNothing);
      expect(find.text('İzlendi'), findsOneWidget);
      expect(find.textContaining('kişi'), findsNothing);

      await tester.tap(find.text('Henüz izlenmeyenler'));
      await tester.pumpAndSettle();
      expect(find.text('İzlenen'), findsNothing);
      expect(find.text('Bu listedeki bütün filmleri izledin.'), findsOneWidget);
    });
  });
}
