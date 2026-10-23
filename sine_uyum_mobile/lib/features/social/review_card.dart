import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/compatibility_badge.dart';
import '../../core/ui/poster_image.dart';
import '../../data/models/social.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';

/// Author row: avatar, name and when; under it the rating and the
/// compatibility badge.
class AuthorRow extends StatelessWidget {
  const AuthorRow({
    super.key,
    required this.author,
    this.rating,
    this.compatibility,
    this.subtitle,
    this.when,
    this.avatarRadius = 18,
  });

  final Author author;
  final int? rating;
  final double? compatibility;
  final String? subtitle;
  final DateTime? when;
  final double avatarRadius;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final details = [
      ?subtitle,
      if (when != null)
        DateFormat.yMMMd(l10n.localeName).format(when!.toLocal()),
    ].join(' · ');

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        InkWell(
          borderRadius: BorderRadius.circular(AppRadii.sm),
          onTap: () => context.push('/profile/${author.id}'),
          child: Row(
            children: [
              UserAvatar(
                userName: author.userName,
                imageUrl: author.imageUrl,
                radius: avatarRadius,
              ),
              const SizedBox(width: AppSpace.sm),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      author.userName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleSmall,
                    ),
                    if (details.isNotEmpty)
                      Text(
                        details,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.bodySmall,
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
        if (rating != null || compatibility != null) ...[
          const SizedBox(height: AppSpace.xs),
          Wrap(
            spacing: AppSpace.xs,
            runSpacing: AppSpace.xxs,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              if (rating != null) RatingPill(rating: rating!),
              if (compatibility != null)
                CompatibilityBadge(score: compatibility!, label: true),
            ],
          ),
        ],
      ],
    );
  }
}

/// "★ 9/10" in the rating gold.
class RatingPill extends StatelessWidget {
  const RatingPill({super.key, required this.rating});

  final int rating;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final l10n = context.l10n;
    return Semantics(
      label: l10n.ratingSpoken(rating),
      excludeSemantics: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(
          color: colors.surfaceRaised,
          borderRadius: BorderRadius.circular(AppRadii.pill),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.star_rounded, size: 16, color: colors.primary),
            const SizedBox(width: 4),
            Text(
              l10n.ratingOutOfTen(rating),
              style: context.type.number.copyWith(
                fontSize: 13,
                height: 18 / 13,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The feed's poster-led card: the poster on the left, who did what, the
/// movie title, the scores and an optional body on the right.
class PosterLedCard extends StatelessWidget {
  const PosterLedCard({
    super.key,
    required this.movie,
    required this.author,
    this.subtitle,
    this.when,
    this.rating,
    this.compatibility,
    this.body,
    this.footer,
  });

  final ReviewMovie movie;
  final Author author;
  final String? subtitle;
  final DateTime? when;
  final int? rating;
  final double? compatibility;
  final Widget? body;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpace.sm),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                GestureDetector(
                  onTap: () => context.push('/movie/${movie.id}'),
                  child: PosterImage(
                    posterPath: movie.posterPath,
                    title: movie.title,
                    width: 84,
                    size: 'w185',
                  ),
                ),
                const SizedBox(width: AppSpace.sm),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      AuthorRow(
                        author: author,
                        subtitle: subtitle,
                        when: when,
                        avatarRadius: 14,
                      ),
                      const SizedBox(height: AppSpace.xs),
                      InkWell(
                        borderRadius: BorderRadius.circular(AppRadii.sm),
                        onTap: () => context.push('/movie/${movie.id}'),
                        child: Text(
                          movie.title,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: context.type.h3,
                        ),
                      ),
                      if (rating != null || compatibility != null) ...[
                        const SizedBox(height: AppSpace.xs),
                        Wrap(
                          spacing: AppSpace.xs,
                          runSpacing: AppSpace.xxs,
                          children: [
                            if (rating != null) RatingPill(rating: rating!),
                            if (compatibility != null)
                              CompatibilityBadge(
                                score: compatibility!,
                                label: true,
                              ),
                          ],
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            if (body != null) ...[const SizedBox(height: AppSpace.sm), body!],
            ?footer,
          ],
        ),
      ),
    );
  }
}

/// A review: spoiler hiding, likes and comments. With [showMovie] (the
/// feed) it is poster-led.
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
    // Optimistic update; the server's response corrects the count.
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
    final l10n = context.l10n;
    final colors = context.colors;
    final theme = Theme.of(context);
    final hidden = _review.containsSpoilers && !_revealed && !_review.isMine;

    final body = hidden
        ? Align(
            alignment: AlignmentDirectional.centerStart,
            child: OutlinedButton.icon(
              onPressed: () => setState(() => _revealed = true),
              icon: const Icon(Icons.visibility_rounded),
              label: Text(l10n.spoilerReveal),
            ),
          )
        : Text(_review.text, style: theme.textTheme.bodyMedium);

    final actions = Row(
      children: [
        TextButton.icon(
          style: TextButton.styleFrom(foregroundColor: colors.textSecondary),
          onPressed: _toggleLike,
          icon: Icon(
            _review.likedByMe
                ? Icons.favorite_rounded
                : Icons.favorite_border_rounded,
            color: _review.likedByMe ? colors.accent : null,
          ),
          label: Text('${_review.likeCount}'),
        ),
        TextButton.icon(
          style: TextButton.styleFrom(foregroundColor: colors.textSecondary),
          onPressed: _openComments,
          icon: const Icon(Icons.chat_bubble_outline_rounded),
          label: Text(l10n.commentCount(_review.commentCount)),
        ),
      ],
    );

    if (widget.showMovie) {
      return PosterLedCard(
        movie: _review.movie,
        author: _review.author,
        subtitle: widget.subtitle,
        when: _review.createdAt,
        rating: _review.rating,
        compatibility: _review.authorCompatibility,
        body: body,
        footer: actions,
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(
          AppSpace.md,
          AppSpace.md,
          AppSpace.md,
          AppSpace.xxs,
        ),
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
            const SizedBox(height: AppSpace.sm),
            body,
            const SizedBox(height: AppSpace.xxs),
            actions,
          ],
        ),
      ),
    );
  }
}

/// Comments panel. Returns the change in the comment count when closed.
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
    final l10n = context.l10n;
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
              ListTile(title: Text(l10n.commentsTitle)),
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
                    ? Padding(
                        padding: const EdgeInsets.all(16),
                        child: Text(l10n.commentsEmpty),
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
                                      tooltip: l10n.deleteComment,
                                      icon: const Icon(
                                        Icons.delete_outline_rounded,
                                      ),
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
                        decoration: InputDecoration(
                          hintText: l10n.commentHint,
                          counterText: '',
                        ),
                        onSubmitted: (_) => _send(),
                      ),
                    ),
                    IconButton(
                      tooltip: l10n.commonSend,
                      onPressed: _sending ? null : _send,
                      icon: const Icon(Icons.send_rounded),
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
