import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/session_provider.dart';
import '../../providers/watchlist_provider.dart';
import '../../core/ui/poster_image.dart';

/// Opens a public list: my own in the editable view, someone else's in the
/// read-only one (`/watchlist/{id}` would fall back to it too, after a 404).
void openListCard(BuildContext context, WidgetRef ref, WatchlistCard card) {
  final myId = ref.read(sessionProvider).user?.id;
  context.push(
    card.owner.id == myId
        ? '/watchlist/${card.id}'
        : '/watchlist/${card.id}/view',
  );
}

/// The latest four posters in a 2×2 grid; empty slots are left blank.
class PosterCollage extends StatelessWidget {
  const PosterCollage({super.key, required this.posters, this.size = 96});

  final List<String?> posters;
  final double size;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    const gap = 2.0;
    final cell = (size - gap) / 2;

    Widget slot(int index) => index < posters.length
        ? PosterImage(
            posterPath: posters[index],
            width: cell,
            height: cell,
            size: 'w185',
            borderRadius: 0,
          )
        : Container(
            width: cell,
            height: cell,
            color: scheme.surfaceContainerHighest,
          );

    return ClipRRect(
      borderRadius: BorderRadius.circular(10),
      child: SizedBox(
        width: size,
        height: size,
        child: posters.isEmpty
            ? Container(
                color: scheme.surfaceContainerHighest,
                alignment: Alignment.center,
                child: Icon(
                  Icons.movie_filter_rounded,
                  color: scheme.onSurfaceVariant,
                ),
              )
            : Column(
                children: [
                  Row(
                    children: [
                      slot(0),
                      const SizedBox(width: gap),
                      slot(1),
                    ],
                  ),
                  const SizedBox(height: gap),
                  Row(
                    children: [
                      slot(2),
                      const SizedBox(width: gap),
                      slot(3),
                    ],
                  ),
                ],
              ),
      ),
    );
  }
}

/// "Public" pill on my own public lists.
class PublicListBadge extends StatelessWidget {
  const PublicListBadge({super.key});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: scheme.tertiaryContainer,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.public_rounded,
            size: 14,
            color: scheme.onTertiaryContainer,
          ),
          const SizedBox(width: 4),
          Text(
            context.l10n.watchlistPublicBadge,
            style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: scheme.onTertiaryContainer),
          ),
        ],
      ),
    );
  }
}

/// "82% compatible" with the list's owner.
class OwnerCompatibilityChip extends StatelessWidget {
  const OwnerCompatibilityChip({super.key, required this.score});

  final double score;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final color = score >= 70
        ? Colors.green
        : Theme.of(context).colorScheme.onSurfaceVariant;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: color.withValues(alpha: 0.6)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.favorite_rounded, size: 12, color: color),
          const SizedBox(width: 4),
          Text(
            l10n.listOwnerCompatibility(l10n.percent(score)),
            style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: color),
          ),
        ],
      ),
    );
  }
}

/// Like and save buttons with their counts for someone else's public list.
/// [initial] is the screen's copy; my latest change (see
/// [listReactionsProvider]) is shown over it.
class ListReactionButtons extends ConsumerWidget {
  const ListReactionButtons({
    super.key,
    required this.listId,
    required this.initial,
  });

  final int listId;
  final ListReaction initial;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final reaction =
        ref.watch(listReactionsProvider.select((all) => all[listId])) ??
        initial;
    final notifier = ref.read(listReactionsProvider.notifier);
    final scheme = Theme.of(context).colorScheme;

    Future<void> run(Future<void> Function(int, ListReaction) toggle) async {
      final messenger = ScaffoldMessenger.of(context);
      try {
        await toggle(listId, reaction);
      } catch (error) {
        // Already rolled back; server messages are shown as-is.
        messenger.showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Tooltip(
          message: reaction.liked ? l10n.watchlistUnlike : l10n.watchlistLike,
          child: TextButton.icon(
            onPressed: () => run(notifier.toggleLike),
            icon: Icon(
              reaction.liked
                  ? Icons.favorite_rounded
                  : Icons.favorite_border_rounded,
              color: reaction.liked ? Colors.redAccent : null,
            ),
            label: Text(
              '${reaction.likeCount}',
              semanticsLabel: l10n.watchlistLikeCount(reaction.likeCount),
            ),
          ),
        ),
        Tooltip(
          message: reaction.saved ? l10n.watchlistUnsave : l10n.watchlistSave,
          child: TextButton.icon(
            onPressed: () => run(notifier.toggleSave),
            icon: Icon(
              reaction.saved
                  ? Icons.bookmark_rounded
                  : Icons.bookmark_border_rounded,
              color: reaction.saved ? scheme.primary : null,
            ),
            label: Text(
              '${reaction.saveCount}',
              semanticsLabel: l10n.watchlistSaveCount(reaction.saveCount),
            ),
          ),
        ),
      ],
    );
  }
}

/// The owner's avatar and @name; opens their profile.
class ListOwnerLink extends StatelessWidget {
  const ListOwnerLink({super.key, required this.card});

  final WatchlistCard card;

  @override
  Widget build(BuildContext context) {
    final owner = card.owner;
    return InkWell(
      borderRadius: BorderRadius.circular(999),
      onTap: () => context.push('/profile/${owner.id}'),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 2),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            UserAvatar(
              userName: owner.userName,
              imageUrl: owner.imageUrl,
              radius: 10,
            ),
            const SizedBox(width: 6),
            Flexible(
              child: Text(
                '@${owner.userName}',
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.labelLarge,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A public list in discovery: posters, name, owner, film count,
/// compatibility, and like/save.
class WatchlistCardTile extends ConsumerWidget {
  const WatchlistCardTile({super.key, required this.card});

  final WatchlistCard card;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final compatibility = card.ownerCompatibility;
    final description = card.description;

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => openListCard(context, ref, card),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 12, 8, 4),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              PosterCollage(posters: card.posters),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      card.name,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleMedium,
                    ),
                    if (description != null && description.isNotEmpty)
                      Text(
                        description,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    const SizedBox(height: 6),
                    ListOwnerLink(card: card),
                    const SizedBox(height: 4),
                    Wrap(
                      spacing: 8,
                      runSpacing: 4,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        Text(
                          l10n.movieCount(card.itemCount),
                          style: theme.textTheme.bodySmall,
                        ),
                        if (compatibility != null)
                          OwnerCompatibilityChip(score: compatibility),
                      ],
                    ),
                    Align(
                      alignment: AlignmentDirectional.centerEnd,
                      child: ListReactionButtons(
                        listId: card.id,
                        initial: ListReaction.ofCard(card),
                      ),
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

/// A small card for a horizontal carousel (a profile's public lists).
class WatchlistCardCompact extends ConsumerWidget {
  const WatchlistCardCompact({super.key, required this.card});

  final WatchlistCard card;

  static const width = 132.0;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final likes =
        ref
            .watch(listReactionsProvider.select((all) => all[card.id]))
            ?.likeCount ??
        card.likeCount;
    return SizedBox(
      width: width,
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: () => openListCard(context, ref, card),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            PosterCollage(posters: card.posters, size: width),
            const SizedBox(height: 6),
            Text(
              card.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.titleSmall,
            ),
            Text(
              '${context.l10n.movieCount(card.itemCount)} · '
              '${context.l10n.watchlistLikeCount(likes)}',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}
