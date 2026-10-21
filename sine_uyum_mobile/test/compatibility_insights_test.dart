import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/profile.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/profile/profile_page.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

const _debate = CommonMovie(
  movieId: 7,
  title: 'Yüzüklerin Efendisi',
  currentUserRating: 10,
  targetUserRating: 3,
);

class FakeSocial implements SocialRepository {
  CompatibilityResult result = const CompatibilityResult(
    compatibilityScore: 71,
    commonMovieCount: 4,
    insights: CompatibilityInsights(
      summary: 'İkiniz de Dram filmlerini seviyorsunuz.',
      sharedGenres: ['Dram'],
      debateMovie: _debate,
    ),
  );

  @override
  Future<UserProfile> profile(String userId) async => const UserProfile(
    id: 'u2',
    userName: 'mehmet',
    statistics: ProfileStatistics(),
  );

  @override
  Future<FollowStatus> followStatus(String userId) async =>
      const FollowStatus();

  @override
  Future<CompatibilityResult> compatibility(String userId) async => result;

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeSession extends SessionNotifier {
  @override
  SessionState build() => const SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(id: 'u1', userName: 'ayse'),
  );
}

void main() {
  test('açıklama ayrıştırılır; eski yanıtta boş', () {
    final parsed = CompatibilityResult.fromJson({
      'compatibilityScore': 80,
      'insights': {
        'summary': 'Korku filmlerinde ayrışıyorsunuz.',
        'sharedGenres': [],
        'divergentGenres': ['Korku'],
        'sharedFavorite': null,
        'debateMovie': {
          'movieId': 3,
          'title': 'X',
          'currentUserRating': 9,
          'targetUserRating': 2,
        },
      },
    });
    expect(parsed.insights.divergentGenres, ['Korku']);
    expect(parsed.insights.debateMovie!.targetUserRating, 2);
    expect(parsed.insights.isEmpty, isFalse);

    expect(CompatibilityResult.fromJson({}).insights.isEmpty, isTrue);
  });

  testWidgets('özet ve tartışma filmi; "Ona sor" sohbeti taslakla açar', (
    tester,
  ) async {
    String? openedDraft;
    final router = GoRouter(
      initialLocation: '/profile/u2',
      routes: [
        GoRoute(
          path: '/profile/:id',
          builder: (_, state) =>
              ProfilePage(userId: state.pathParameters['id']!),
        ),
        GoRoute(
          path: '/conversation/:id',
          builder: (_, state) {
            openedDraft = state.uri.queryParameters['draft'];
            return const Scaffold(body: Text('sohbet'));
          },
        ),
      ],
    );
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          socialRepositoryProvider.overrideWithValue(FakeSocial()),
          sessionProvider.overrideWith(FakeSession.new),
        ],
        child: MaterialApp.router(routerConfig: router),
      ),
    );
    await tester.pumpAndSettle();

    expect(
      find.text('İkiniz de Dram filmlerini seviyorsunuz.'),
      findsOneWidget,
    );
    expect(
      find.textContaining('Tartışma filmi: Yüzüklerin Efendisi'),
      findsOneWidget,
    );
    expect(find.textContaining('Sen 10 verdin, mehmet 3'), findsOneWidget);

    await tester.scrollUntilVisible(find.text('Ona sor'), 100);
    await tester.tap(find.text('Ona sor'));
    await tester.pumpAndSettle();

    expect(find.text('sohbet'), findsOneWidget);
    expect(
      openedDraft,
      '"Yüzüklerin Efendisi" filmine ben 10, sen 3 vermişsin. Neden? 😄',
    );
  });
}
