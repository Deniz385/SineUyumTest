import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../data/models/message.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

class ConversationPage extends ConsumerStatefulWidget {
  const ConversationPage({
    super.key,
    required this.otherUserId,
    this.otherUserName,
  });

  final String otherUserId;
  final String? otherUserName;

  @override
  ConsumerState<ConversationPage> createState() => _ConversationPageState();
}

class _ConversationPageState extends ConsumerState<ConversationPage> {
  final _controller = TextEditingController();
  bool _isSending = false;

  @override
  void initState() {
    super.initState();
    // Sohbet açılınca gelen mesajlar okundu sayılır.
    Future.microtask(() async {
      try {
        await ref
            .read(messageRepositoryProvider)
            .markThreadAsRead(widget.otherUserId);
        ref.invalidate(conversationsProvider);
      } catch (_) {
        // Okundu işaretlemesi başarısız olsa da sohbet görüntülenebilir.
      }
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _isSending) return;

    setState(() => _isSending = true);
    try {
      await ref
          .read(messageRepositoryProvider)
          .send(recipientId: widget.otherUserId, content: text);
      _controller.clear();
      ref.invalidate(messageThreadProvider(widget.otherUserId));
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
    final thread = ref.watch(messageThreadProvider(widget.otherUserId));
    final currentUserId = ref.watch(sessionProvider).user?.id;

    return Scaffold(
      appBar: AppBar(
        title: InkWell(
          onTap: () => context.push('/profile/${widget.otherUserId}'),
          child: Text(widget.otherUserName ?? 'Sohbet'),
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
                    ref.invalidate(messageThreadProvider(widget.otherUserId)),
              ),
              data: (messages) => messages.isEmpty
                  ? const EmptyView(
                      icon: Icons.waving_hand_outlined,
                      message: 'Henüz mesaj yok. İlk mesajı sen yaz.',
                    )
                  : ListView.builder(
                      // Son mesaj altta olacak şekilde ters çiziyoruz.
                      reverse: true,
                      padding: const EdgeInsets.all(12),
                      itemCount: messages.length,
                      itemBuilder: (context, index) {
                        final message = messages[messages.length - 1 - index];
                        return _MessageBubble(
                          message: message,
                          isMine: message.senderId == currentUserId,
                        );
                      },
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
                      decoration: const InputDecoration(
                        hintText: 'Mesaj yaz...',
                        counterText: '',
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    onPressed: _isSending ? null : _send,
                    icon: const Icon(Icons.send),
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

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({required this.message, required this.isMine});

  final ChatMessage message;
  final bool isMine;

  @override
  Widget build(BuildContext context) {
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
                  DateFormat.Hm('tr').format(message.messageSent!.toLocal()),
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
                  'Film önerisi',
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
    return Row(
      children: [
        Icon(Icons.bookmark_outline, color: foreground),
        const SizedBox(width: 8),
        Flexible(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Paylaşılan liste',
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
