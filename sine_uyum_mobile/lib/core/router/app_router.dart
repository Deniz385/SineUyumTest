import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/login_page.dart';
import '../../features/auth/register_page.dart';
import '../../features/events/event_page.dart';
import '../../features/home/home_page.dart';
import '../../features/messages/conversation_page.dart';
import '../../features/messages/messages_page.dart';
import '../../features/movies/movie_detail_page.dart';
import '../../features/movies/movie_search_page.dart';
import '../../features/notifications/notifications_page.dart';
import '../../features/profile/edit_profile_page.dart';
import '../../features/profile/profile_page.dart';
import '../../features/settings/privacy_settings_page.dart';
import '../../features/settings/settings_page.dart';
import '../../features/shell/app_shell.dart';
import '../../features/shell/splash_page.dart';
import '../../features/subscription/subscription_page.dart';
import '../../features/users/user_search_page.dart';
import '../../features/watchlists/watchlist_detail_page.dart';
import '../../features/watchlists/watchlists_page.dart';
import '../../providers/session_provider.dart';

final routerProvider = Provider<GoRouter>((ref) {
  // go_router'ın yönlendirmeyi yeniden değerlendirmesi için oturum durumunu
  // bir Listenable'a köprülüyoruz.
  final sessionListenable = ValueNotifier<SessionStatus>(
    ref.read(sessionProvider).status,
  );
  ref.listen<SessionState>(sessionProvider, (previous, next) {
    sessionListenable.value = next.status;
  });
  ref.onDispose(sessionListenable.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: sessionListenable,
    redirect: (context, state) {
      final status = ref.read(sessionProvider).status;
      final location = state.matchedLocation;
      final isAuthRoute = location == '/login' || location == '/register';

      // Token henüz okunmadı: açılış ekranında bekle.
      if (status == SessionStatus.unknown) {
        return location == '/splash' ? null : '/splash';
      }

      if (status == SessionStatus.unauthenticated) {
        return isAuthRoute ? null : '/login';
      }

      // Giriş yapılmış; auth veya splash ekranında kalmasın.
      if (isAuthRoute || location == '/splash') return '/home';

      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashPage()),
      GoRoute(path: '/login', builder: (_, _) => const LoginPage()),
      GoRoute(path: '/register', builder: (_, _) => const RegisterPage()),

      // Alt gezinme çubuğundaki beş sekme; her sekme kendi durumunu korur.
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) =>
            AppShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(path: '/home', builder: (_, _) => const HomePage()),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/people',
                builder: (_, _) => const UserSearchPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/watchlists',
                builder: (_, _) => const WatchlistsPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/messages',
                builder: (_, _) => const MessagesPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(path: '/event', builder: (_, _) => const EventPage()),
            ],
          ),
        ],
      ),

      // Sekmelerin üzerine açılan detay ekranları.
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
        path: '/profile/:userId',
        builder: (context, state) =>
            ProfilePage(userId: state.pathParameters['userId'] ?? ''),
      ),
      GoRoute(
        path: '/watchlist/:listId',
        builder: (context, state) => WatchlistDetailPage(
          listId: int.tryParse(state.pathParameters['listId'] ?? '') ?? 0,
        ),
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
