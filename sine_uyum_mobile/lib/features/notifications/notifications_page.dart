import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../l10n/l10n.dart';
import '../../providers/notification_provider.dart';
import '../../core/ui/state_views.dart';

class NotificationsPage extends ConsumerWidget {
  const NotificationsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final state = ref.watch(notificationsProvider);
    final notifier = ref.read(notificationsProvider.notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.notificationsTitle),
        actions: [
          if (state.unreadCount > 0)
            TextButton(
              onPressed: notifier.markAllAsRead,
              child: Text(l10n.notificationsMarkAllRead),
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
            return EmptyView(
              icon: Icons.notifications_none_rounded,
              message: l10n.notificationsEmpty,
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
                        ? Icons.notifications_none_rounded
                        : Icons.notifications_active_rounded,
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
                          DateFormat.yMMMd(l10n.localeName)
                              .add_Hm()
                              .format(notification.createdAt!.toLocal()),
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

/// The server builds notifications with web routes (`/my-event`,
/// `/messages/{id}`, `/profile/{id}`); map them to the mobile routes.
String? _toMobileRoute(String? webUrl) {
  if (webUrl == null || webUrl.isEmpty) return null;

  if (webUrl == '/my-event') return '/event';
  // Shared-list invitation: the invitations are on the My lists screen.
  if (webUrl == '/watchlist') return '/watchlists';
  if (webUrl.startsWith('/messages/')) {
    final userId = webUrl.substring('/messages/'.length);
    return userId.isEmpty ? '/messages' : '/conversation/$userId';
  }
  if (webUrl.startsWith('/profile/')) return webUrl;
  if (webUrl.startsWith('/movie/')) return webUrl;
  // Someone joined my shared list.
  if (RegExp(r'^/watchlist/\d+$').hasMatch(webUrl)) return webUrl;

  return null;
}

/// Public alias of the route mapping, so unit tests (and push) can call it.
String? notificationRouteFor(String? webUrl) => _toMobileRoute(webUrl);
