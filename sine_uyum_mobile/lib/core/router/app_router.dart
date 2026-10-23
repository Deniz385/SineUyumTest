import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/login_page.dart';
import '../../features/auth/register_page.dart';
import '../../features/diary/diary_page.dart';
import '../../features/discover/discover_page.dart';
import '../../features/events/event_page.dart';
import '../../features/home/home_page.dart';
import '../../features/messages/conversation_page.dart';
import '../../features/messages/messages_page.dart';
import '../../features/movies/movie_detail_page.dart';
import '../../features/movies/movie_search_page.dart';
import '../../features/notifications/notifications_page.dart';
import '../../features/onboarding/welcome_page.dart';
import '../../features/profile/edit_profile_page.dart';
import '../../features/profile/film_identity_page.dart';
import '../../features/profile/profile_page.dart';
import '../../features/settings/privacy_settings_page.dart';
import '../../features/settings/settings_page.dart';
import '../../features/shell/app_shell.dart';
import '../../features/shell/splash_page.dart';
import '../../features/shell/tab_scaffold.dart';
import '../../features/subscription/subscription_page.dart';
import '../../features/watchlists/discover_lists_page.dart';
import '../../features/watchlists/public_watchlist_page.dart';
import '../../features/watchlists/watchlist_detail_page.dart';
import '../../features/watchlists/watchlists_page.dart';
import '../../features/year_in_review/year_in_review_page.dart';
import '../../l10n/l10n.dart';
import '../../providers/session_provider.dart';

final routerProvider = Provider<GoRouter>((ref) {
  // Bridge the session state (signed in or not, first-run welcome done or
  // not) to a Listenable so go_router re-evaluates its redirects.
  (SessionStatus, bool) gate(SessionState s) =>
      (s.status, s.user?.onboardingCompleted ?? true);
  final sessionListenable = ValueNotifier<(SessionStatus, bool)>(
    gate(ref.read(sessionProvider)),
  );
  ref.listen<SessionState>(sessionProvider, (previous, next) {
    sessionListenable.value = gate(next);
  });
  ref.onDispose(sessionListenable.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: sessionListenable,
    redirect: (context, state) {
      final session = ref.read(sessionProvider);
      final status = session.status;
      final location = state.matchedLocation;
      final isAuthRoute = location == '/login' || location == '/register';

      // Token not read yet: wait on the splash screen.
      if (status == SessionStatus.unknown) {
        return location == '/splash' ? null : '/splash';
      }

      if (status == SessionStatus.unauthenticated) {
        return isAuthRoute ? null : '/login';
      }

      // Signed in for the first time: the welcome comes first, once.
      final welcomed = session.user?.onboardingCompleted ?? true;
      if (!welcomed) return location == '/welcome' ? null : '/welcome';
      if (location == '/welcome') return '/home';

      // Signed in: don't stay on the auth or splash screens.
      if (isAuthRoute || location == '/splash') return '/home';

      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashPage()),
      GoRoute(path: '/login', builder: (_, _) => const LoginPage()),
      GoRoute(path: '/register', builder: (_, _) => const RegisterPage()),
      GoRoute(path: '/welcome', builder: (_, _) => const WelcomePage()),

      // The five bottom-navigation tabs (docs/DESIGN-SYSTEM.md §7); each
      // keeps its own state. Keep AppShell.tabPaths in the same order.
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) =>
            AppShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/home',
                builder: (context, _) => TabScaffold(
                  title: context.l10n.appName,
                  body: const HomePage(),
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/discover',
                builder: (context, state) => DiscoverPage(
                  section: DiscoverSection.parse(
                    state.uri.queryParameters['section'],
                  ),
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/event',
                builder: (context, _) => TabScaffold(
                  title: context.l10n.navEvent,
                  body: const EventPage(),
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/messages',
                builder: (context, _) => TabScaffold(
                  title: context.l10n.navMessages,
                  body: const MessagesPage(),
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/profile',
                builder: (_, _) => const MyProfileTab(),
              ),
            ],
          ),
        ],
      ),

      // The old People tab lives in Discover now; old links still work.
      GoRoute(path: '/people', redirect: (_, _) => '/discover?section=people'),
      // My lists, invitations and saved lists (the old Lists tab; the
      // shared-list notification opens it).
      GoRoute(path: '/watchlists', builder: (_, _) => const WatchlistsPage()),

      // Detail screens opened on top of the tabs.
      GoRoute(
        path: '/movie/:movieId',
        builder: (context, state) => MovieDetailPage(
          movieId: int.tryParse(state.pathParameters['movieId'] ?? '') ?? 0,
        ),
      ),
      GoRoute(
        path: '/movies/search',
        builder: (_, _) => const MovieSearchPage(),
      ),
      GoRoute(
        path: '/profile/edit',
        builder: (_, _) => const EditProfilePage(),
      ),
      GoRoute(
        path: '/profile/:userId/identity',
        builder: (context, state) => FilmIdentityPage(
          userId: state.pathParameters['userId'] ?? '',
          isMine: state.uri.queryParameters['mine'] == '1',
        ),
      ),
      GoRoute(
        path: '/profile/:userId/diary',
        builder: (context, state) =>
            DiaryPage(userId: state.pathParameters['userId'] ?? ''),
      ),
      GoRoute(
        path: '/profile/:userId/year-in-review',
        builder: (context, state) => YearInReviewPage(
          userId: state.pathParameters['userId'] ?? '',
          year: int.tryParse(state.uri.queryParameters['year'] ?? ''),
        ),
      ),
      GoRoute(
        path: '/profile/:userId',
        builder: (context, state) =>
            ProfilePage(userId: state.pathParameters['userId'] ?? ''),
      ),
      // My list (owner or member); any other list falls back to the
      // read-only view.
      GoRoute(
        path: '/watchlist/:listId',
        builder: (context, state) => WatchlistDetailPage(
          listId: int.tryParse(state.pathParameters['listId'] ?? '') ?? 0,
        ),
      ),
      // Someone else's list, read-only (from discovery, profiles, saved).
      GoRoute(
        path: '/watchlist/:listId/view',
        builder: (context, state) => PublicWatchlistPage(
          listId: int.tryParse(state.pathParameters['listId'] ?? '') ?? 0,
        ),
      ),
      GoRoute(
        path: '/lists/discover',
        builder: (_, _) => const DiscoverListsPage(),
      ),
      GoRoute(
        path: '/conversation/:userId',
        builder: (context, state) => ConversationPage(
          otherUserId: state.pathParameters['userId'] ?? '',
          otherUserName: state.uri.queryParameters['name'],
          draft: state.uri.queryParameters['draft'],
        ),
      ),
      GoRoute(
        path: '/notifications',
        builder: (_, _) => const NotificationsPage(),
      ),
      GoRoute(path: '/settings', builder: (_, _) => const SettingsPage()),
      GoRoute(
        path: '/settings/privacy',
        builder: (_, _) => const PrivacySettingsPage(),
      ),
      GoRoute(
        path: '/subscription',
        builder: (_, _) => const SubscriptionPage(),
      ),
    ],
  );
});

/// The Profile tab: the signed-in user's own profile.
class MyProfileTab extends ConsumerWidget {
  const MyProfileTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final userId = ref.watch(sessionProvider.select((s) => s.user?.id));
    if (userId == null) return const SizedBox.shrink();
    return ProfilePage(userId: userId);
  }
}
