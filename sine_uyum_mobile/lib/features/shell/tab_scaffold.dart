import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../l10n/l10n.dart';
import '../../providers/notification_provider.dart';

/// The notifications bell with the unread count.
class NotificationsButton extends ConsumerWidget {
  const NotificationsButton({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final unread = ref.watch(
      notificationsProvider.select((state) => state.unreadCount),
    );
    return IconButton(
      tooltip: l10n.notificationsTitle,
      icon: Badge.count(
        count: unread,
        isLabelVisible: unread > 0,
        child: const Icon(Icons.notifications_none_rounded),
      ),
      onPressed: () => context.push('/notifications'),
    );
  }
}

/// A top-level tab: its own app bar (title, optional actions, the
/// notifications bell) over [body].
class TabScaffold extends StatelessWidget {
  const TabScaffold({
    super.key,
    required this.title,
    required this.body,
    this.actions = const [],
    this.floatingActionButton,
  });

  final String title;
  final Widget body;
  final List<Widget> actions;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: Semantics(header: true, child: Text(title)),
      actions: [
        ...actions,
        const NotificationsButton(),
        const SizedBox(width: 4),
      ],
    ),
    body: body,
    floatingActionButton: floatingActionButton,
  );
}
