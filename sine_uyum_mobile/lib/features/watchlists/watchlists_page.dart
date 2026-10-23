import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/watchlist_provider.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/state_views.dart';
import 'list_cards.dart';

class WatchlistsPage extends ConsumerWidget {
  const WatchlistsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final lists = ref.watch(myWatchlistsProvider);
    // Not cached offline: when it can't be loaded the section is just hidden.
    final invitations =
        ref.watch(watchlistInvitationsProvider).value ?? const [];

    Future<void> refresh() async {
      ref.invalidate(myWatchlistsProvider);
      ref.invalidate(watchlistInvitationsProvider);
      ref.invalidate(savedListsProvider);
      await ref.read(myWatchlistsProvider.future);
    }

    return Scaffold(
      appBar: AppBar(title: Text(l10n.myListsTitle)),
      body: lists.when(
        loading: () => const LoadingView(),
        error: (error, _) =>
            ErrorView(message: error.toString(), onRetry: refresh),
        data: (lists) => RefreshIndicator(
          onRefresh: refresh,
          child: ListView(
            // Room for the FAB under the last list.
            padding: const EdgeInsets.only(bottom: 88),
            children: [
              const _DiscoverEntry(),
              if (invitations.isNotEmpty)
                _InvitationsSection(invitations: invitations),
              if (lists.isEmpty)
                EmptyView(
                  icon: Icons.bookmark_outline_rounded,
                  message: l10n.watchlistsEmpty,
                  action: invitations.isEmpty
                      ? FilledButton.icon(
                          onPressed: () => _showEditor(context, ref),
                          icon: const Icon(Icons.add_rounded),
                          label: Text(l10n.createList),
                        )
                      : null,
                ),
              for (final list in lists)
                _WatchlistTile(
                  list: list,
                  onEdit: () => _showEditor(context, ref, existing: list),
                  onDelete: () => _confirmDelete(context, ref, list),
                ),
              const _SavedListsSection(),
            ],
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _showEditor(context, ref),
        tooltip: l10n.newList,
        child: const Icon(Icons.add_rounded),
      ),
    );
  }

  Future<void> _showEditor(
    BuildContext context,
    WidgetRef ref, {
    WatchlistSummary? existing,
  }) async {
    final result = await showDialog<_ListDraft>(
      context: context,
      builder: (_) => _ListEditorDialog(existing: existing),
    );
    if (result == null) return;

    final repository = ref.read(watchlistRepositoryProvider);
    try {
      if (existing == null) {
        await repository.create(
          name: result.name,
          description: result.description,
          visibility: result.visibility,
        );
      } else {
        await repository.update(
          existing.id,
          name: result.name,
          description: result.description,
          visibility: result.visibility,
        );
      }
      refreshMyWatchlists(ref, listId: existing?.id);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }

  Future<void> _confirmDelete(
    BuildContext context,
    WidgetRef ref,
    WatchlistSummary list,
  ) async {
    final l10n = context.l10n;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.deleteListTitle),
        content: Text(l10n.deleteListBody(list.name)),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.commonCancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.commonDelete),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    try {
      await ref.read(watchlistRepositoryProvider).remove(list.id);
      refreshMyWatchlists(ref);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }
}

typedef _ListDraft = ({
  String name,
  String description,
  WatchlistVisibility visibility,
});

/// Name, description and visibility of a new or edited list. It owns its
/// text controllers, so they live as long as the dialog (including its
/// closing animation).
class _ListEditorDialog extends StatefulWidget {
  const _ListEditorDialog({this.existing});

  final WatchlistSummary? existing;

  @override
  State<_ListEditorDialog> createState() => _ListEditorDialogState();
}

class _ListEditorDialogState extends State<_ListEditorDialog> {
  final _formKey = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.existing?.name ?? '');
  late final _description = TextEditingController(
    text: widget.existing?.description ?? '',
  );
  late bool _isPublic = widget.existing?.isPublic ?? false;

  @override
  void dispose() {
    _name.dispose();
    _description.dispose();
    super.dispose();
  }

  void _save() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop<_ListDraft>((
      name: _name.text.trim(),
      description: _description.text.trim(),
      visibility: _isPublic
          ? WatchlistVisibility.public
          : WatchlistVisibility.private,
    ));
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return AlertDialog(
      title: Text(widget.existing == null ? l10n.newList : l10n.editList),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: _name,
                autofocus: true,
                maxLength: 100,
                decoration: InputDecoration(labelText: l10n.listNameLabel),
                validator: (value) => (value == null || value.trim().isEmpty)
                    ? l10n.listNameRequired
                    : null,
              ),
              TextFormField(
                controller: _description,
                maxLength: 500,
                decoration: InputDecoration(labelText: l10n.descriptionLabel),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                secondary: Icon(
                  _isPublic ? Icons.public_rounded : Icons.lock_outline_rounded,
                ),
                title: Text(l10n.watchlistPublicSwitch),
                // One line on what the choice means.
                subtitle: Text(
                  _isPublic
                      ? l10n.watchlistPublicHint
                      : l10n.watchlistPrivateHint,
                ),
                value: _isPublic,
                onChanged: (value) => setState(() => _isPublic = value),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: Text(l10n.commonCancel),
        ),
        FilledButton(onPressed: _save, child: Text(l10n.commonSave)),
      ],
    );
  }
}

/// A list in "my lists": a shared list carries a badge with the number of
/// people, and someone else's list names its owner. Only the owner can
/// rename or delete it.
class _WatchlistTile extends StatelessWidget {
  const _WatchlistTile({
    required this.list,
    required this.onEdit,
    required this.onDelete,
  });

  final WatchlistSummary list;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final owner = list.ownerUsername;
    final details = [
      l10n.movieCount(list.itemCount),
      if (!list.isOwner && owner != null && owner.isNotEmpty)
        l10n.watchlistByOwner(owner),
      if (list.description?.isNotEmpty == true) list.description!,
    ];

    return ListTile(
      leading: Icon(
        list.isShared ? Icons.group_rounded : Icons.bookmark_outline_rounded,
      ),
      title: Row(
        children: [
          Flexible(child: Text(list.name, overflow: TextOverflow.ellipsis)),
          if (list.isShared) ...[
            const SizedBox(width: 8),
            WatchlistPeopleBadge(count: list.memberCount),
          ],
          if (list.isPublic) ...[
            const SizedBox(width: 8),
            const PublicListBadge(),
          ],
        ],
      ),
      subtitle: Text(
        details.join(' · '),
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: list.isOwner
          ? PopupMenuButton<String>(
              onSelected: (value) {
                if (value == 'edit') {
                  onEdit();
                } else if (value == 'delete') {
                  onDelete();
                }
              },
              itemBuilder: (context) => [
                PopupMenuItem(value: 'edit', child: Text(l10n.commonEdit)),
                PopupMenuItem(value: 'delete', child: Text(l10n.commonDelete)),
              ],
            )
          : null,
      onTap: () => context.push('/watchlist/${list.id}'),
    );
  }
}

/// "Shared" pill showing how many people a list has.
class WatchlistPeopleBadge extends StatelessWidget {
  const WatchlistPeopleBadge({super.key, required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    final label = l10n.watchlistPeopleCount(count);
    return Semantics(
      label: '${l10n.sharedWatchlistLabel}, $label',
      excludeSemantics: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        decoration: BoxDecoration(
          color: scheme.secondaryContainer,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.group_rounded,
              size: 14,
              color: scheme.onSecondaryContainer,
            ),
            const SizedBox(width: 4),
            Text(
              label,
              style: Theme.of(context).textTheme.labelSmall
                  ?.copyWith(color: scheme.onSecondaryContainer),
            ),
          ],
        ),
      ),
    );
  }
}

/// Invitations to other people's lists, above my lists.
class _InvitationsSection extends StatelessWidget {
  const _InvitationsSection({required this.invitations});

  final List<WatchlistInvitation> invitations;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(4, 0, 4, 8),
            child: Text(
              context.l10n.watchlistInvitationsHeader(invitations.length),
              style: Theme.of(context).textTheme.titleSmall,
            ),
          ),
          for (final invitation in invitations)
            _InvitationCard(
              key: ValueKey(invitation.listId),
              invitation: invitation,
            ),
        ],
      ),
    );
  }
}

class _InvitationCard extends ConsumerStatefulWidget {
  const _InvitationCard({super.key, required this.invitation});

  final WatchlistInvitation invitation;

  @override
  ConsumerState<_InvitationCard> createState() => _InvitationCardState();
}

class _InvitationCardState extends ConsumerState<_InvitationCard> {
  bool _busy = false;

  Future<void> _answer({required bool accept}) async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    final invitation = widget.invitation;
    final notifier = ref.read(watchlistInvitationsProvider.notifier);
    setState(() => _busy = true);
    try {
      if (accept) {
        await notifier.accept(invitation.listId);
      } else {
        await notifier.decline(invitation.listId);
      }
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            accept
                ? l10n.watchlistJoined(invitation.name)
                : l10n.watchlistInvitationDeclined,
          ),
        ),
      );
    } catch (error) {
      // Server messages (e.g. the invitation was cancelled) are shown as-is.
      messenger.showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final invitation = widget.invitation;
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Column(
          children: [
            ListTile(
              leading: UserAvatar(
                userName: invitation.owner.userName,
                imageUrl: invitation.owner.imageUrl,
              ),
              title: Text(invitation.name),
              subtitle: Text(
                '${l10n.watchlistInvitedBy(invitation.owner.userName)} · '
                '${l10n.movieCount(invitation.itemCount)}',
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton(
                    onPressed: _busy ? null : () => _answer(accept: false),
                    child: Text(l10n.watchlistDecline),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    onPressed: _busy ? null : () => _answer(accept: true),
                    child: Text(l10n.watchlistAccept),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The way into list discovery, above my lists.
class _DiscoverEntry extends StatelessWidget {
  const _DiscoverEntry();

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final scheme = Theme.of(context).colorScheme;
    return Card(
      margin: const EdgeInsets.fromLTRB(12, 12, 12, 4),
      color: scheme.secondaryContainer,
      child: ListTile(
        leading: Icon(
          Icons.explore_rounded,
          color: scheme.onSecondaryContainer,
        ),
        title: Text(l10n.discoverListsTitle),
        subtitle: Text(l10n.discoverListsSubtitle),
        trailing: const Icon(Icons.chevron_right_rounded),
        onTap: () => context.push('/lists/discover'),
      ),
    );
  }
}

/// Others' public lists I saved, below my lists. Hidden while loading, when
/// it fails (e.g. offline without a stored copy) and when empty. Unsaving
/// takes a list out right away (it comes back if that fails).
class _SavedListsSection extends ConsumerWidget {
  const _SavedListsSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final saved = ref.watch(savedListsProvider).value;
    final reactions = ref.watch(listReactionsProvider);
    if (saved == null) return const SizedBox.shrink();
    final cards = saved.items
        .where((card) => reactions[card.id]?.saved ?? card.savedByMe)
        .toList();
    if (cards.isEmpty) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 4),
            child: Text(
              l10n.savedListsTitle,
              style: Theme.of(context).textTheme.titleSmall,
            ),
          ),
          for (final card in cards)
            _SavedListTile(key: ValueKey(card.id), card: card),
          if (saved.hasMore)
            Center(
              child: TextButton(
                onPressed: saved.isLoadingMore
                    ? null
                    : () => ref.read(savedListsProvider.notifier).loadMore(),
                child: Text(l10n.savedListsShowMore),
              ),
            ),
        ],
      ),
    );
  }
}

class _SavedListTile extends ConsumerWidget {
  const _SavedListTile({super.key, required this.card});

  final WatchlistCard card;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final reaction =
        ref.watch(listReactionsProvider.select((all) => all[card.id])) ??
        ListReaction.ofCard(card);

    Future<void> unsave() async {
      final messenger = ScaffoldMessenger.of(context);
      try {
        await ref
            .read(listReactionsProvider.notifier)
            .toggleSave(card.id, reaction);
      } catch (error) {
        messenger.showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }

    return ListTile(
      leading: PosterCollage(posters: card.posters, size: 48),
      title: Text(card.name, maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text(
        '@${card.owner.userName} · ${l10n.movieCount(card.itemCount)}',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: IconButton(
        tooltip: l10n.watchlistUnsave,
        icon: Icon(
          Icons.bookmark_rounded,
          color: Theme.of(context).colorScheme.primary,
        ),
        onPressed: unsave,
      ),
      onTap: () => openListCard(context, ref, card),
    );
  }
}
