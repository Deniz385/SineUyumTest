import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../providers/repository_providers.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

class MessagesPage extends ConsumerWidget {
  const MessagesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final conversations = ref.watch(conversationsProvider);

    return conversations.when(
      loading: () => const LoadingView(),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.invalidate(conversationsProvider),
      ),
      data: (conversations) => conversations.isEmpty
          ? const EmptyView(
              icon: Icons.chat_bubble_outline,
              message:
                  'Henüz mesajın yok. Keşfet sekmesinden birine yazabilirsin.',
            )
          : RefreshIndicator(
              onRefresh: () async {
                ref.invalidate(conversationsProvider);
                await ref.read(conversationsProvider.future);
              },
              child: ListView.separated(
                itemCount: conversations.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final conversation = conversations[index];
                  return ListTile(
                    leading: UserAvatar(
                      userName: conversation.otherUserUsername,
                      imageUrl: conversation.otherUserProfileImageUrl,
                    ),
                    title: Text(conversation.otherUserUsername),
                    subtitle: Text(
                      conversation.lastMessageContent,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: conversation.isRead
                          ? null
                          : const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    trailing: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        if (conversation.lastMessageSent != null)
                          Text(
                            _formatTimestamp(conversation.lastMessageSent!),
                            style: Theme.of(context).textTheme.labelSmall,
                          ),
                        if (!conversation.isRead)
                          Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Container(
                              width: 8,
                              height: 8,
                              decoration: BoxDecoration(
                                color: Theme.of(context).colorScheme.primary,
                                shape: BoxShape.circle,
                              ),
                            ),
                          ),
                      ],
                    ),
                    onTap: () => context.push(
                      '/conversation/${conversation.otherUserId}'
                      '?name=${Uri.encodeComponent(conversation.otherUserUsername)}',
                    ),
                  );
                },
              ),
            ),
    );
  }
}

/// Bugünse saat, bu yıl içindeyse gün/ay, daha eskiyse tam tarih.
String _formatTimestamp(DateTime timestamp) {
  final local = timestamp.toLocal();
  final now = DateTime.now();

  if (local.year == now.year &&
      local.month == now.month &&
      local.day == now.day) {
    return DateFormat.Hm('tr').format(local);
  }
  if (local.year == now.year) {
    return DateFormat('d MMM', 'tr').format(local);
  }
  return DateFormat('d MMM y', 'tr').format(local);
}
