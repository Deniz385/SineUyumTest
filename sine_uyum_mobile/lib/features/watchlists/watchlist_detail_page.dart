import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_exception.dart';
import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/watchlist_provider.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';
import 'list_cards.dart';
import 'public_watchlist_page.dart';
import 'watchlist_members_sheet.dart';

class WatchlistDetailPage extends ConsumerStatefulWidget {
  const WatchlistDetailPage({super.key, required this.listId});

  final int listId;

  @override
  ConsumerState<WatchlistDetailPage> createState() =>
      _WatchlistDetailPageState();
}

class _WatchlistDetailPageState extends ConsumerState<WatchlistDetailPage> {
  /// "Unwatched by all of us": only movies nobody in the list has rated.
  bool _unwatchedOnly = false;

  int get _listId => widget.listId;

  Future<void> _openMembers() async {
    final action = await WatchlistMembersSheet.show(context, listId: _listId);
    if (action == WatchlistMembersAction.leave && mounted) {
      final list = ref.read(watchlistDetailProvider(_listId)).value;
      if (list != null) await _leave(list);
    }
  }

  Future<void> _leave(WatchlistDetail list) async {
    final l10n = context.l10n;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.watchlistLeaveTitle(list.name)),
        content: Text(l10n.watchlistLeaveBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.commonCancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.watchlistLeave),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(watchlistDetailProvider(_listId).notifier).leave();
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.watchlistLeft(list.name))),
      );
      // The list is no longer accessible: back to where it was opened from.
      if (mounted) await Navigator.of(context).maybePop();
    } catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(error.toString())));
    }
  }

  Future<void> _removeMovie(WatchlistItem item) async {
    try {
      await ref
          .read(watchlistDetailProvider(_listId).notifier)
          .removeMovie(item.movieId);
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
    final detail = ref.watch(watchlistDetailProvider(_listId));
    final list = detail.value;

    // Not one of my lists (or I left it): someone else's public list, or one
    // sent to me in a message, opens read-only. Notifications and links use
    // this route for both.
    final error = detail.error;
    if (!detail.isLoading && error is ApiException && error.isNotFound) {
      return PublicWatchlistPage(listId: _listId);
    }

    return Scaffold(
      appBar: AppBar(
        title: Text(list?.name ?? l10n.watchlistFallbackTitle),
        actions: [
          if (list != null && (list.isOwner || list.isShared))
            IconButton(
              tooltip: l10n.watchlistMembers,
              icon: const Icon(Icons.group_outlined),
              onPressed: _openMembers,
            ),
          if (list != null && !list.isOwner)
            PopupMenuButton<String>(
              onSelected: (_) => _leave(list),
              itemBuilder: (context) => [
                PopupMenuItem(value: 'leave', child: Text(l10n.watchlistLeave)),
              ],
            ),
        ],
      ),
      body: detail.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(watchlistDetailProvider(_listId)),
        ),
        data: _buildList,
      ),
    );
  }

  Widget _buildList(WatchlistDetail list) {
    final l10n = context.l10n;
    final shared = list.isShared;
    final showHeader = shared || (list.isOwner && list.members.isNotEmpty);
    final visible = _unwatchedOnly
        ? list.items.where((i) => i.watchedBy.isEmpty).toList()
        : list.items;
    final images = {for (final p in list.people) p.userName: p.imageUrl};

    if (list.items.isEmpty && !showHeader && !list.isPublic) {
      return EmptyView(
        icon: Icons.movie_outlined,
        message: l10n.watchlistEmpty,
      );
    }

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(watchlistDetailProvider(_listId));
        await ref.read(watchlistDetailProvider(_listId).future);
      },
      child: ListView(
        children: [
          if (list.isPublic) _PublicHeader(list: list),
          if (showHeader) _PeopleHeader(list: list, onTap: _openMembers),
          if (list.items.isNotEmpty)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: FilterChip(
                  avatar: const Icon(Icons.visibility_off_outlined, size: 18),
                  label: Text(
                    shared
                        ? l10n.watchlistFilterUnwatchedShared
                        : l10n.watchlistFilterUnwatched,
                  ),
                  selected: _unwatchedOnly,
                  onSelected: (value) => setState(() => _unwatchedOnly = value),
                ),
              ),
            ),
          if (list.items.isEmpty)
            EmptyView(icon: Icons.movie_outlined, message: l10n.watchlistEmpty)
          else if (visible.isEmpty)
            EmptyView(
              icon: Icons.task_alt,
              message: shared
                  ? l10n.watchlistAllWatchedShared
                  : l10n.watchlistAllWatched,
            ),
          for (final item in visible)
            _ItemTile(
              key: ValueKey(item.movieId),
              item: item,
              shared: shared,
              images: images,
              onRemove: () => _removeMovie(item),
            ),
        ],
      ),
    );
  }
}

/// A public list: the badge, and how many people liked and saved it.
class _PublicHeader extends StatelessWidget {
  const _PublicHeader({required this.list});

  final WatchlistDetail list;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final small = theme.textTheme.bodySmall?.copyWith(
      color: theme.colorScheme.onSurfaceVariant,
    );
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      child: Wrap(
        spacing: 12,
        runSpacing: 6,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          const PublicListBadge(),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.favorite_border, size: 16, color: small?.color),
              const SizedBox(width: 4),
              Text(l10n.watchlistLikeCount(list.likeCount), style: small),
            ],
          ),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.bookmark_border, size: 16, color: small?.color),
              const SizedBox(width: 4),
              Text(l10n.watchlistSaveCount(list.saveCount), style: small),
            ],
          ),
        ],
      ),
    );
  }
}

/// The list's people (owner first); tapping opens the members sheet.
class _PeopleHeader extends StatelessWidget {
  const _PeopleHeader({required this.list, required this.onTap});

  final WatchlistDetail list;
  final VoidCallback onTap;

  static const _maxAvatars = 5;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final people = list.people;
    final shown = people.take(_maxAvatars).toList();
    final pending = list.members.where((m) => !m.accepted).length;
    final owner = list.owner;
    final details = [
      l10n.watchlistPeopleCount(people.length),
      if (!list.isOwner && owner != null) l10n.watchlistByOwner(owner.userName),
      if (list.isOwner && pending > 0) l10n.watchlistPendingCount(pending),
    ];

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 8, 4),
        child: Row(
          children: [
            // Overlapping avatars.
            for (final (index, person) in shown.indexed)
              Align(
                widthFactor: index == shown.length - 1 ? 1 : 0.75,
                child: CircleAvatar(
                  radius: 18,
                  backgroundColor: Theme.of(context).colorScheme.surface,
                  child: UserAvatar(
                    userName: person.userName,
                    imageUrl: person.imageUrl,
                    radius: 16,
                  ),
                ),
              ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                details.join(' · '),
                style: Theme.of(context).textTheme.bodyMedium,
              ),
            ),
            const Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }
}

class _ItemTile extends StatelessWidget {
  const _ItemTile({
    super.key,
    required this.item,
    required this.shared,
    required this.images,
    required this.onRemove,
  });

  final WatchlistItem item;
  final bool shared;

  /// Profile photos of the list's people, by username.
  final Map<String, String?> images;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final small = theme.textTheme.bodySmall?.copyWith(
      color: theme.colorScheme.onSurfaceVariant,
    );
    final addedBy = item.addedByUsername;
    final watched = item.watchedBy;

    final lines = <Widget>[
      if (shared && addedBy != null && addedBy.isNotEmpty)
        Text(l10n.watchlistAddedBy(addedBy), style: small),
      if (watched.isNotEmpty)
        Padding(
          padding: const EdgeInsets.only(top: 2),
          child: Row(
            children: [
              if (shared)
                for (final name in watched.take(4))
                  Padding(
                    padding: const EdgeInsetsDirectional.only(end: 2),
                    child: UserAvatar(
                      userName: name,
                      imageUrl: images[name],
                      radius: 9,
                    ),
                  )
              else
                Icon(
                  Icons.check_circle,
                  size: 14,
                  color: theme.colorScheme.primary,
                ),
              const SizedBox(width: 4),
              Flexible(
                child: Text(
                  shared
                      ? l10n.watchlistWatchedBy(watched.join(', '))
                      : l10n.watchlistWatched,
                  style: small,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ),
    ];

    return ListTile(
      leading: PosterImage(posterPath: item.posterPath, width: 44, height: 66),
      title: Text(item.title, maxLines: 2, overflow: TextOverflow.ellipsis),
      subtitle: lines.isEmpty
          ? null
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: lines,
            ),
      trailing: IconButton(
        tooltip: l10n.removeFromList,
        icon: const Icon(Icons.delete_outline),
        onPressed: onRemove,
      ),
      onTap: () => context.push('/movie/${item.movieId}'),
    );
  }
}
