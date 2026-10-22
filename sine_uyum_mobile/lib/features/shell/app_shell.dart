import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../l10n/l10n.dart';
import '../../providers/notification_provider.dart';
import '../../providers/session_provider.dart';
import '../../widgets/poster_image.dart';

/// Shell holding the bottom navigation bar and the app bar.
class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static List<String> _titles(AppLocalizations l10n) => [
    l10n.appName,
    l10n.navDiscover,
    l10n.myListsTitle,
    l10n.navMessages,
    l10n.navEvent,
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final user = ref.watch(sessionProvider).user;
    final unreadCount = ref.watch(notificationsProvider).unreadCount;

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles(l10n)[navigationShell.currentIndex]),
        actions: [
          IconButton(
            tooltip: l10n.searchMoviesTooltip,
            icon: const Icon(Icons.search),
            onPressed: () => context.push('/movies/search'),
          ),
          IconButton(
            tooltip: l10n.notificationsTitle,
            icon: Badge.count(
              count: unreadCount,
              isLabelVisible: unreadCount > 0,
              child: const Icon(Icons.notifications_outlined),
            ),
            onPressed: () => context.push('/notifications'),
          ),
          PopupMenuButton<String>(
            tooltip: l10n.accountMenuTooltip,
            icon: UserAvatar(
              userName: user?.userName ?? '',
              imageUrl: user?.profileImageUrl,
              radius: 15,
            ),
            onSelected: (value) {
              switch (value) {
                case 'profile':
                  if (user != null) context.push('/profile/${user.id}');
                case 'subscription':
                  context.push('/subscription');
                case 'settings':
                  context.push('/settings');
              }
            },
            itemBuilder: (context) => [
              PopupMenuItem(value: 'profile', child: Text(l10n.menuMyProfile)),
              PopupMenuItem(
                value: 'subscription',
                child: Text(l10n.commonSubscription),
              ),
              PopupMenuItem(
                value: 'settings',
                child: Text(l10n.commonSettings),
              ),
            ],
          ),
        ],
      ),
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: navigationShell.currentIndex,
        // initialLocation: tapping the current tab again returns to its root.
        onDestinationSelected: (index) => navigationShell.goBranch(
          index,
          initialLocation: index == navigationShell.currentIndex,
        ),
        destinations: [
          NavigationDestination(
            icon: const Icon(Icons.home_outlined),
            selectedIcon: const Icon(Icons.home),
            label: l10n.navHome,
          ),
          NavigationDestination(
            icon: const Icon(Icons.people_outline),
            selectedIcon: const Icon(Icons.people),
            label: l10n.navDiscover,
          ),
          NavigationDestination(
            icon: const Icon(Icons.bookmark_outline),
            selectedIcon: const Icon(Icons.bookmark),
            label: l10n.navLists,
          ),
          NavigationDestination(
            icon: const Icon(Icons.chat_bubble_outline),
            selectedIcon: const Icon(Icons.chat_bubble),
            label: l10n.navMessages,
          ),
          NavigationDestination(
            icon: const Icon(Icons.local_activity_outlined),
            selectedIcon: const Icon(Icons.local_activity),
            label: l10n.navEvent,
          ),
        ],
      ),
    );
  }
}
