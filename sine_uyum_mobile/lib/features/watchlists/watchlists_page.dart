import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/watchlist_provider.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

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
      await ref.read(myWatchlistsProvider.future);
    }

    return Scaffold(
      body: lists.when(
        loading: () => const LoadingView(),
        error: (error, _) =>
            ErrorView(message: error.toString(), onRetry: refresh),
        data: (lists) => lists.isEmpty && invitations.isEmpty
            ? EmptyView(
                icon: Icons.bookmark_outline,
                message: l10n.watchlistsEmpty,
                action: FilledButton.icon(
                  onPressed: () => _showEditor(context, ref),
                  icon: const Icon(Icons.add),
                  label: Text(l10n.createList),
                ),
              )
            : RefreshIndicator(
                onRefresh: refresh,
                child: ListView(
                  // Room for the FAB under the last list.
                  padding: const EdgeInsets.only(bottom: 88),
                  children: [
                    if (invitations.isNotEmpty)
                      _InvitationsSection(invitations: invitations),
                    if (lists.isEmpty)
                      EmptyView(
                        icon: Icons.bookmark_outline,
                        message: l10n.watchlistsEmpty,
                      ),
                    for (final list in lists)
                      _WatchlistTile(
                        list: list,
                        onEdit: () => _showEditor(context, ref, existing: list),
                        onDelete: () => _confirmDelete(context, ref, list),
                      ),
                  ],
                ),
              ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _showEditor(context, ref),
        tooltip: l10n.newList,
        child: const Icon(Icons.add),
      ),
    );
  }

  Future<void> _showEditor(
    BuildContext context,
    WidgetRef ref, {
    WatchlistSummary? existing,
  }) async {
    final nameController = TextEditingController(text: existing?.name ?? '');
    final descriptionController = TextEditingController(
      text: existing?.description ?? '',
    );
    final formKey = GlobalKey<FormState>();
    final l10n = context.l10n;

    final saved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(existing == null ? l10n.newList : l10n.editList),
        content: Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: nameController,
                autofocus: true,
                maxLength: 100,
                decoration: InputDecoration(labelText: l10n.listNameLabel),
                validator: (value) => (value == null || value.trim().isEmpty)
                    ? l10n.listNameRequired
                    : null,
              ),
              TextFormField(
                controller: descriptionController,
                maxLength: 500,
                decoration: InputDecoration(labelText: l10n.descriptionLabel),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.commonCancel),
          ),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) {
                Navigator.of(context).pop(true);
              }
            },
            child: Text(l10n.commonSave),
          ),
        ],
      ),
    );

    final name = nameController.text.trim();
    final description = descriptionController.text.trim();
    nameController.dispose();
    descriptionController.dispose();

    if (saved != true) return;

    final repository = ref.read(watchlistRepositoryProvider);
    try {
      if (existing == null) {
        await repository.create(name: name, description: description);
      } else {
        await repository.update(
          existing.id,
          name: name,
          description: description,
        );
        ref.invalidate(watchlistDetailProvider(existing.id));
      }
      ref.invalidate(myWatchlistsProvider);
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
      ref.invalidate(myWatchlistsProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
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
        list.isShared ? Icons.group_outlined : Icons.bookmark_outline,
      ),
      title: Row(
        children: [
          Flexible(child: Text(list.name, overflow: TextOverflow.ellipsis)),
          if (list.isShared) ...[
            const SizedBox(width: 8),
            WatchlistPeopleBadge(count: list.memberCount),
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
            Icon(Icons.group, size: 14, color: scheme.onSecondaryContainer),
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
