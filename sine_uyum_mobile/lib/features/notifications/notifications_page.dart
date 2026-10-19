import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../providers/notification_provider.dart';
import '../../widgets/state_views.dart';

class NotificationsPage extends ConsumerWidget {
  const NotificationsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(notificationsProvider);
    final notifier = ref.read(notificationsProvider.notifier);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Bildirimler'),
        actions: [
          if (state.unreadCount > 0)
            TextButton(
              onPressed: notifier.markAllAsRead,
              child: const Text('Tümünü okundu yap'),
            ),
        ],
      ),
      body: Builder(
        builder: (context) {
          if (state.isLoading && state.items.isEmpty) {
            return const LoadingView();
          }
          if (state.error != null && state.items.isEmpty) {
            return ErrorView(message: state.error!, onRetry: notifier.load);
          }
          if (state.items.isEmpty) {
            return const EmptyView(
              icon: Icons.notifications_none,
              message: 'Henüz bildirimin yok.',
            );
          }

          return RefreshIndicator(
            onRefresh: notifier.load,
            child: ListView.separated(
              itemCount: state.items.length,
              separatorBuilder: (_, _) => const Divider(height: 1),
              itemBuilder: (context, index) {
                final notification = state.items[index];
                return ListTile(
                  leading: Icon(
                    notification.isRead
                        ? Icons.notifications_none
                        : Icons.notifications_active,
                    color: notification.isRead
                        ? null
                        : Theme.of(context).colorScheme.primary,
                  ),
                  title: Text(
                    notification.message,
                    style: notification.isRead
                        ? null
                        : const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  subtitle: notification.createdAt == null
                      ? null
                      : Text(
                          DateFormat(
                            'd MMM y, HH:mm',
                            'tr',
                          ).format(notification.createdAt!.toLocal()),
                        ),
                  onTap: () {
                    if (!notification.isRead) {
                      notifier.markAsRead(notification.id);
                    }
                    final route = _toMobileRoute(notification.relatedUrl);
                    if (route != null) context.push(route);
                  },
                );
              },
            ),
          );
        },
      ),
    );
  }
}

/// Bildirimler sunucuda web rotalarıyla üretiliyor (`/my-event`,
/// `/messages/{id}`, `/profile/{id}`). Mobil rota adlarına çeviriyoruz.
String? _toMobileRoute(String? webUrl) {
  if (webUrl == null || webUrl.isEmpty) return null;

  if (webUrl == '/my-event') return '/event';
  if (webUrl.startsWith('/messages/')) {
    final userId = webUrl.substring('/messages/'.length);
    return userId.isEmpty ? '/messages' : '/conversation/$userId';
  }
  if (webUrl.startsWith('/profile/')) return webUrl;
  if (webUrl.startsWith('/movie/')) return webUrl;

  return null;
}

/// Rota çevirisini birim testten çağırabilmek için açık bırakılmış kopya.
String? notificationRouteFor(String? webUrl) => _toMobileRoute(webUrl);
