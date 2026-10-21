import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../data/models/social.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/poster_image.dart';

/// Yazar satırı: avatar, ad, puan ve uyum rozeti.
class AuthorRow extends StatelessWidget {
  const AuthorRow({
    super.key,
    required this.author,
    this.rating,
    this.compatibility,
    this.subtitle,
    this.when,
  });

  final Author author;
  final int? rating;
  final double? compatibility;
  final String? subtitle;
  final DateTime? when;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final details = [
      ?subtitle,
      if (when != null) DateFormat.yMMMd('tr').format(when!.toLocal()),
    ].join(' · ');

    return Row(
      children: [
        GestureDetector(
          onTap: () => context.push('/profile/${author.id}'),
          child: UserAvatar(
            userName: author.userName,
            imageUrl: author.imageUrl,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(author.userName, style: theme.textTheme.titleSmall),
              if (details.isNotEmpty)
                Text(details, style: theme.textTheme.bodySmall),
            ],
          ),
        ),
        if (rating != null)
          Chip(
            avatar: const Icon(Icons.star, size: 16, color: Colors.amber),
            label: Text('$rating/10'),
            visualDensity: VisualDensity.compact,
          ),
        if (compatibility != null) ...[
          const SizedBox(width: 4),
          Chip(
            label: Text('%${compatibility!.round()} uyumlu'),
            visualDensity: VisualDensity.compact,
            side: BorderSide(
              color: compatibility! >= 70
                  ? Colors.green
                  : theme.colorScheme.outlineVariant,
            ),
          ),
        ],
      ],
    );
  }
}

/// Bir inceleme: spoiler gizleme, beğeni ve yorumlar.
class ReviewCard extends ConsumerStatefulWidget {
  const ReviewCard({
    super.key,
    required this.review,
    this.showMovie = false,
    this.subtitle,
  });

  final Review review;
  final bool showMovie;
  final String? subtitle;

  @override
  ConsumerState<ReviewCard> createState() => _ReviewCardState();
}

class _ReviewCardState extends ConsumerState<ReviewCard> {
  late Review _review = widget.review;
  bool _revealed = false;

  @override
  void didUpdateWidget(covariant ReviewCard oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.review != widget.review) _review = widget.review;
  }

  Future<void> _toggleLike() async {
    final before = _review;
    final liked = !before.likedByMe;
    // İyimser güncelleme; sunucu yanıtı sayıyı düzeltir.
    setState(
      () => _review = before.copyWith(
        likedByMe: liked,
        likeCount: before.likeCount + (liked ? 1 : -1),
      ),
    );
    try {
      final result = await ref
          .read(socialRepositoryProvider)
          .setLiked(before.id, liked);
      if (mounted) {
        setState(
          () => _review = _review.copyWith(
            likedByMe: result.liked,
            likeCount: result.likeCount,
          ),
        );
      }
    } catch (error) {
      if (!mounted) return;
      setState(() => _review = before);
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error.toString())));
    }
  }

  Future<void> _openComments() async {
    final delta = await showModalBottomSheet<int>(
      context: context,
      isScrollControlled: true,
      builder: (_) => CommentsSheet(reviewId: _review.id),
    );
    if (delta != null && delta != 0 && mounted) {
      setState(
        () => _review = _review.copyWith(
          commentCount: _review.commentCount + delta,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final hidden = _review.containsSpoilers && !_revealed && !_review.isMine;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AuthorRow(
              author: _review.author,
              rating: _review.rating,
              compatibility: _review.authorCompatibility,
              subtitle: widget.subtitle,
              when: _review.createdAt,
            ),
            if (widget.showMovie) ...[
              const SizedBox(height: 10),
              InkWell(
                onTap: () => context.push('/movie/${_review.movie.id}'),
                child: Row(
                  children: [
                    PosterImage(
                      posterPath: _review.movie.posterPath,
                      width: 40,
                      height: 60,
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        _review.movie.title,
                        style: theme.textTheme.titleMedium,
                      ),
                    ),
                  ],
                ),
              ),
            ],
            const SizedBox(height: 10),
            if (hidden)
              OutlinedButton.icon(
                onPressed: () => setState(() => _revealed = true),
                icon: const Icon(Icons.visibility_outlined),
                label: const Text('Spoiler içeriyor — göster'),
              )
            else
              Text(_review.text, style: theme.textTheme.bodyMedium),
            const SizedBox(height: 4),
            Row(
              children: [
                TextButton.icon(
                  onPressed: _toggleLike,
                  icon: Icon(
                    _review.likedByMe ? Icons.favorite : Icons.favorite_border,
                    color: _review.likedByMe ? Colors.redAccent : null,
                  ),
                  label: Text('${_review.likeCount}'),
                ),
                TextButton.icon(
                  onPressed: _openComments,
                  icon: const Icon(Icons.chat_bubble_outline),
                  label: Text('${_review.commentCount} yorum'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Yorumlar paneli. Kapanırken yorum sayısındaki değişimi döner.
class CommentsSheet extends ConsumerStatefulWidget {
  const CommentsSheet({super.key, required this.reviewId});

  final int reviewId;

  @override
  ConsumerState<CommentsSheet> createState() => _CommentsSheetState();
}

class _CommentsSheetState extends ConsumerState<CommentsSheet> {
  final _controller = TextEditingController();
  List<ReviewComment>? _comments;
  Object? _error;
  bool _sending = false;
  int _delta = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final comments = await ref
          .read(socialRepositoryProvider)
          .comments(widget.reviewId);
      if (mounted) setState(() => _comments = comments);
    } catch (error) {
      if (mounted) setState(() => _error = error);
    }
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _sending) return;
    setState(() => _sending = true);
    try {
      final comment = await ref
          .read(socialRepositoryProvider)
          .addComment(widget.reviewId, text);
      if (!mounted) return;
      setState(() {
        _comments = [...?_comments, comment];
        _delta++;
        _controller.clear();
      });
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  Future<void> _delete(ReviewComment comment) async {
    try {
      await ref.read(socialRepositoryProvider).deleteComment(comment.id);
      if (!mounted) return;
      setState(() {
        _comments = _comments?.where((c) => c.id != comment.id).toList();
        _delta--;
      });
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final comments = _comments;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.of(context).pop(_delta);
      },
      child: Padding(
        padding: EdgeInsets.only(
          bottom: MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const ListTile(title: Text('Yorumlar')),
              Flexible(
                child: _error != null
                    ? Padding(
                        padding: const EdgeInsets.all(16),
                        child: Text(_error.toString()),
                      )
                    : comments == null
                    ? const Padding(
                        padding: EdgeInsets.all(24),
                        child: CircularProgressIndicator(),
                      )
                    : comments.isEmpty
                    ? const Padding(
                        padding: EdgeInsets.all(16),
                        child: Text('Henüz yorum yok. İlk yorumu sen yaz.'),
                      )
                    : ListView(
                        shrinkWrap: true,
                        children: [
                          for (final c in comments)
                            ListTile(
                              leading: UserAvatar(
                                userName: c.author.userName,
                                imageUrl: c.author.imageUrl,
                                radius: 16,
                              ),
                              title: Text(c.author.userName),
                              subtitle: Text(c.text),
                              trailing: c.canDelete
                                  ? IconButton(
                                      tooltip: 'Yorumu sil',
                                      icon: const Icon(Icons.delete_outline),
                                      onPressed: () => _delete(c),
                                    )
                                  : null,
                            ),
                        ],
                      ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
                child: Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _controller,
                        maxLength: 500,
                        decoration: const InputDecoration(
                          hintText: 'Yorum yaz...',
                          counterText: '',
                        ),
                        onSubmitted: (_) => _send(),
                      ),
                    ),
                    IconButton(
                      tooltip: 'Gönder',
                      onPressed: _sending ? null : _send,
                      icon: const Icon(Icons.send),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
