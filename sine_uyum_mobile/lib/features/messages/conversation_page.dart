import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../data/models/message.dart';
import '../../l10n/l10n.dart';
import '../../providers/chat_provider.dart';
import '../../providers/notification_provider.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/state_views.dart';

class ConversationPage extends ConsumerStatefulWidget {
  const ConversationPage({
    super.key,
    required this.otherUserId,
    this.otherUserName,
    this.draft,
  });

  final String otherUserId;
  final String? otherUserName;

  /// A pre-filled draft (e.g. "Ask them" on a profile); editable before
  /// sending.
  final String? draft;

  @override
  ConsumerState<ConversationPage> createState() => _ConversationPageState();
}

class _ConversationPageState extends ConsumerState<ConversationPage> {
  late final _controller = TextEditingController(text: widget.draft ?? '');
  bool _isSending = false;
  // Captured up front because `ref` can't be used in dispose.
  late final ActiveConversation _activeConversation;

  @override
  void initState() {
    super.initState();
    _activeConversation = ref.read(activeConversationProvider.notifier);
    // When the conversation opens: its notifications are no longer shown, and
    // its messages and notifications count as read.
    Future.microtask(() {
      _activeConversation.open(widget.otherUserId);
      ref.read(chatThreadProvider(widget.otherUserId).notifier).markAsRead();
      ref
          .read(notificationsProvider.notifier)
          .markConversationRead(widget.otherUserId);
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    final otherUserId = widget.otherUserId;
    final active = _activeConversation;
    // Deferred to a microtask so no provider changes while the tree is torn
    // down.
    Future.microtask(() => active.close(otherUserId));
    super.dispose();
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _isSending) return;

    setState(() => _isSending = true);
    try {
      final sent = await ref
          .read(messageRepositoryProvider)
          .send(recipientId: widget.otherUserId, content: text);
      _controller.clear();
      // The response is the message itself: add it without refetching the
      // thread (the live echo with the same id is de-duplicated).
      ref.read(chatThreadProvider(widget.otherUserId).notifier).add(sent);
      ref.invalidate(conversationsProvider);
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    } finally {
      if (mounted) setState(() => _isSending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final thread = ref.watch(chatThreadProvider(widget.otherUserId));
    final currentUserId = ref.watch(sessionProvider).user?.id;

    return Scaffold(
      appBar: AppBar(
        title: InkWell(
          onTap: () => context.push('/profile/${widget.otherUserId}'),
          child: Text(widget.otherUserName ?? l10n.conversationFallbackTitle),
        ),
      ),
      body: Column(
        children: [
          Expanded(
            child: thread.when(
              loading: () => const LoadingView(),
              error: (error, _) => ErrorView(
                message: error.toString(),
                onRetry: () =>
                    ref.invalidate(chatThreadProvider(widget.otherUserId)),
              ),
              data: (messages) => messages.isEmpty
                  ? EmptyView(
                      icon: Icons.waving_hand_rounded,
                      message: l10n.conversationEmpty,
                    )
                  : _MessageList(
                      messages: messages,
                      currentUserId: currentUserId,
                    ),
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _controller,
                      minLines: 1,
                      maxLines: 4,
                      maxLength: 1000,
                      textInputAction: TextInputAction.send,
                      onSubmitted: (_) => _send(),
                      decoration: InputDecoration(
                        hintText: l10n.messageHint,
                        counterText: '',
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    onPressed: _isSending ? null : _send,
                    icon: const Icon(Icons.send_rounded),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MessageList extends StatelessWidget {
  const _MessageList({required this.messages, required this.currentUserId});

  final List<ChatMessage> messages;
  final String? currentUserId;

  @override
  Widget build(BuildContext context) {
    // "Seen" appears only under my last read message.
    final lastSeenId = messages
        .where((m) => m.senderId == currentUserId && m.dateRead != null)
        .fold<int>(0, (max, m) => m.id > max ? m.id : max);

    return ListView.builder(
      // Drawn reversed so the latest message is at the bottom.
      reverse: true,
      padding: const EdgeInsets.all(12),
      itemCount: messages.length,
      itemBuilder: (context, index) {
        final message = messages[messages.length - 1 - index];
        return _MessageBubble(
          message: message,
          isMine: message.senderId == currentUserId,
          showSeen: message.id == lastSeenId,
        );
      },
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({
    required this.message,
    required this.isMine,
    this.showSeen = false,
  });

  final ChatMessage message;
  final bool isMine;
  final bool showSeen;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final time = DateFormat.Hm(l10n.localeName);
    final scheme = Theme.of(context).colorScheme;
    final background = isMine ? scheme.primaryContainer : scheme.surface;
    final foreground = isMine ? scheme.onPrimaryContainer : scheme.onSurface;

    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.all(10),
        constraints: BoxConstraints(
          maxWidth: MediaQuery.sizeOf(context).width * 0.75,
        ),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(14),
          border: isMine ? null : Border.all(color: scheme.outlineVariant),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (message.movie != null)
              _SharedMovieCard(movie: message.movie!, foreground: foreground),
            if (message.watchlist != null)
              _SharedWatchlistCard(
                watchlist: message.watchlist!,
                foreground: foreground,
              ),
            if (message.content != null && message.content!.isNotEmpty)
              Padding(
                padding: EdgeInsets.only(
                  top: (message.movie != null || message.watchlist != null)
                      ? 8
                      : 0,
                ),
                child: Text(
                  message.content!,
                  style: TextStyle(color: foreground),
                ),
              ),
            if (message.messageSent != null)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  showSeen && message.dateRead != null
                      ? l10n.messageSentAndSeen(
                          time.format(message.messageSent!.toLocal()),
                          time.format(message.dateRead!.toLocal()),
                        )
                      : time.format(message.messageSent!.toLocal()),
                  style: Theme.of(context).textTheme.labelSmall
                      ?.copyWith(color: foreground.withValues(alpha: 0.7)),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _SharedMovieCard extends StatelessWidget {
  const _SharedMovieCard({required this.movie, required this.foreground});

  final SharedMovie movie;
  final Color foreground;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => context.push('/movie/${movie.id}'),
      child: Row(
        children: [
          PosterImage(posterPath: movie.posterPath, width: 40, height: 60),
          const SizedBox(width: 8),
          Flexible(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  context.l10n.sharedMovieLabel,
                  style: Theme.of(context).textTheme.labelSmall
                      ?.copyWith(color: foreground.withValues(alpha: 0.7)),
                ),
                Text(
                  movie.title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: foreground,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SharedWatchlistCard extends StatelessWidget {
  const _SharedWatchlistCard({
    required this.watchlist,
    required this.foreground,
  });

  final SharedWatchlist watchlist;
  final Color foreground;

  @override
  Widget build(BuildContext context) {
    // Opens my list, or the read-only view of someone else's.
    return InkWell(
      onTap: () => context.push('/watchlist/${watchlist.id}'),
      child: _content(context),
    );
  }

  Widget _content(BuildContext context) {
    return Row(
      children: [
        Icon(Icons.bookmark_outline_rounded, color: foreground),
        const SizedBox(width: 8),
        Flexible(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                context.l10n.sharedWatchlistLabel,
                style: Theme.of(context).textTheme.labelSmall
                    ?.copyWith(color: foreground.withValues(alpha: 0.7)),
              ),
              Text(
                watchlist.name,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: foreground,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
