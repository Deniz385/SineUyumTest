import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../providers/notification_provider.dart';
import '../../providers/session_provider.dart';
import '../../widgets/poster_image.dart';

/// Alt gezinme çubuğu ve üst çubuğu taşıyan kabuk.
class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const _titles = [
    'SineUyum',
    'Keşfet',
    'Listelerim',
    'Mesajlar',
    'Etkinlik',
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(sessionProvider).user;
    final unreadCount = ref.watch(notificationsProvider).unreadCount;

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[navigationShell.currentIndex]),
        actions: [
          IconButton(
            tooltip: 'Film ara',
            icon: const Icon(Icons.search),
            onPressed: () => context.push('/movies/search'),
          ),
          IconButton(
            tooltip: 'Bildirimler',
            icon: Badge.count(
              count: unreadCount,
              isLabelVisible: unreadCount > 0,
              child: const Icon(Icons.notifications_outlined),
            ),
            onPressed: () => context.push('/notifications'),
          ),
          PopupMenuButton<String>(
            tooltip: 'Hesap',
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
            itemBuilder: (context) => const [
              PopupMenuItem(value: 'profile', child: Text('Profilim')),
              PopupMenuItem(value: 'subscription', child: Text('Abonelik')),
              PopupMenuItem(value: 'settings', child: Text('Ayarlar')),
            ],
          ),
        ],
      ),
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: navigationShell.currentIndex,
        // initialLocation: aynı sekmeye tekrar dokunulduğunda o sekmenin
        // köküne dönülür.
        onDestinationSelected: (index) => navigationShell.goBranch(
          index,
          initialLocation: index == navigationShell.currentIndex,
        ),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Ana sayfa',
          ),
          NavigationDestination(
            icon: Icon(Icons.people_outline),
            selectedIcon: Icon(Icons.people),
            label: 'Keşfet',
          ),
          NavigationDestination(
            icon: Icon(Icons.bookmark_outline),
            selectedIcon: Icon(Icons.bookmark),
            label: 'Listeler',
          ),
          NavigationDestination(
            icon: Icon(Icons.chat_bubble_outline),
            selectedIcon: Icon(Icons.chat_bubble),
            label: 'Mesajlar',
          ),
          NavigationDestination(
            icon: Icon(Icons.local_activity_outlined),
            selectedIcon: Icon(Icons.local_activity),
            label: 'Etkinlik',
          ),
        ],
      ),
    );
  }
}
