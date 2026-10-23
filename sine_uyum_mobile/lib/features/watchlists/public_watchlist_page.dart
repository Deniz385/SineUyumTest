import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/session_provider.dart';
import '../../providers/watchlist_provider.dart';
import '../../widgets/movie_widgets.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';
import 'list_cards.dart';

/// Read-only view of a list I am not a member of: someone else's public list
/// or one sent to me in a message. Shows the owner, the description,
/// like/save (public lists only) and the movies.
class PublicWatchlistPage extends ConsumerWidget {
  const PublicWatchlistPage({super.key, required this.listId});

  final int listId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final detail = ref.watch(publicWatchlistProvider(listId));
    final list = detail.value;

    return Scaffold(
      appBar: AppBar(title: Text(list?.name ?? l10n.watchlistFallbackTitle)),
      body: detail.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(publicWatchlistProvider(listId)),
        ),
        data: (list) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(publicWatchlistProvider(listId));
            await ref.read(publicWatchlistProvider(listId).future);
          },
          child: ListView(
            padding: const EdgeInsets.only(bottom: 16),
            children: [
              _Header(list: list),
              const Divider(height: 1),
              if (list.items.isEmpty)
                EmptyView(
                  icon: Icons.movie_outlined,
                  message: l10n.publicListEmpty,
                )
              else
                for (final item in list.items)
                  MovieListTile(
                    key: ValueKey(item.movieId),
                    movieId: item.movieId,
                    title: item.title,
                    posterPath: item.posterPath,
                  ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Header extends ConsumerWidget {
  const _Header({required this.list});

  final WatchlistDetail list;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final owner = list.owner;
    final ownerName = owner?.userName ?? list.ownerUsername ?? '';
    final myId = ref.watch(sessionProvider.select((s) => s.user?.id));
    final description = list.description;
    // Likes and saves are for others' public lists (400 on my own).
    final canReact = list.isPublic && owner != null && owner.id != myId;

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (ownerName.isNotEmpty)
            InkWell(
              borderRadius: BorderRadius.circular(999),
              onTap: owner == null || owner.id.isEmpty
                  ? null
                  : () => context.push('/profile/${owner.id}'),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    UserAvatar(
                      userName: ownerName,
                      imageUrl: owner?.imageUrl,
                      radius: 16,
                    ),
                    const SizedBox(width: 8),
                    Text('@$ownerName', style: theme.textTheme.titleSmall),
                  ],
                ),
              ),
            ),
          if (description != null && description.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(description, style: theme.textTheme.bodyMedium),
          ],
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: Text(
                  l10n.movieCount(list.items.length),
                  style: theme.textTheme.bodySmall,
                ),
              ),
              if (canReact)
                ListReactionButtons(
                  listId: list.id,
                  initial: ListReaction.ofDetail(list),
                ),
            ],
          ),
          if (list.canEdit)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: FilledButton.tonalIcon(
                onPressed: () =>
                    context.pushReplacement('/watchlist/${list.id}'),
                icon: const Icon(Icons.edit_outlined),
                label: Text(l10n.watchlistOpenEditable),
              ),
            ),
        ],
      ),
    );
  }
}
