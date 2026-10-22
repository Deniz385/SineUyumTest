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

import 'support/localized_app.dart';

const _debate = CommonMovie(
  movieId: 7,
  title: 'The Lord of the Rings',
  currentUserRating: 10,
  targetUserRating: 3,
);

class FakeSocial implements SocialRepository {
  // The summary and genre names come localized from the server.
  CompatibilityResult result = const CompatibilityResult(
    compatibilityScore: 71,
    commonMovieCount: 4,
    insights: CompatibilityInsights(
      summary: 'You both love dramas.',
      sharedGenres: ['Drama'],
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
  test('insights are parsed; empty in an older response', () {
    final parsed = CompatibilityResult.fromJson({
      'compatibilityScore': 80,
      'insights': {
        'summary': 'You differ on horror movies.',
        'sharedGenres': [],
        'divergentGenres': ['Horror'],
        'sharedFavorite': null,
        'debateMovie': {
          'movieId': 3,
          'title': 'X',
          'currentUserRating': 9,
          'targetUserRating': 2,
        },
      },
    });
    expect(parsed.insights.divergentGenres, ['Horror']);
    expect(parsed.insights.debateMovie!.targetUserRating, 2);
    expect(parsed.insights.isEmpty, isFalse);

    expect(CompatibilityResult.fromJson({}).insights.isEmpty, isTrue);
  });

  /// Pumps the profile of `u2`; returns a getter for the draft passed to the
  /// conversation screen.
  Future<String? Function()> pump(WidgetTester tester, Locale locale) async {
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
            return const Scaffold(body: Text('chat'));
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
        child: localizedRouterApp(routerConfig: router, locale: locale),
      ),
    );
    await tester.pumpAndSettle();
    return () => openedDraft;
  }

  testWidgets('summary and debate movie; "Ask them" opens the chat with a '
      'draft', (tester) async {
    final draft = await pump(tester, tr);

    expect(find.text('You both love dramas.'), findsOneWidget);
    expect(
      find.textContaining('Tartışma filmi: The Lord of the Rings'),
      findsOneWidget,
    );
    expect(find.textContaining('Sen 10 verdin, mehmet 3'), findsOneWidget);
    expect(find.text('%71'), findsOneWidget);

    await tester.scrollUntilVisible(find.text('Ona sor'), 100);
    await tester.tap(find.text('Ona sor'));
    await tester.pumpAndSettle();

    expect(find.text('chat'), findsOneWidget);
    expect(
      draft(),
      '"The Lord of the Rings" filmine ben 10, sen 3 vermişsin. Neden? 😄',
    );
  });

  testWidgets('renders the insights and the draft in English', (tester) async {
    final draft = await pump(tester, en);

    expect(find.text('Debate movie: The Lord of the Rings'), findsOneWidget);
    expect(find.text('You gave it 10, mehmet 3. Why not ask?'), findsOneWidget);
    expect(find.text('71%'), findsOneWidget);
    expect(find.text('Follow'), findsOneWidget);

    await tester.scrollUntilVisible(find.text('Ask them'), 100);
    await tester.tap(find.text('Ask them'));
    await tester.pumpAndSettle();

    expect(
      draft(),
      'I gave "The Lord of the Rings" a 10 and you gave it a 3. Why? 😄',
    );
  });
}
