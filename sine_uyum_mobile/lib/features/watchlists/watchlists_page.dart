import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/state_views.dart';

class WatchlistsPage extends ConsumerWidget {
  const WatchlistsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final lists = ref.watch(myWatchlistsProvider);

    return Scaffold(
      body: lists.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(myWatchlistsProvider),
        ),
        data: (lists) => lists.isEmpty
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
                onRefresh: () async {
                  ref.invalidate(myWatchlistsProvider);
                  await ref.read(myWatchlistsProvider.future);
                },
                child: ListView.builder(
                  itemCount: lists.length,
                  itemBuilder: (context, index) {
                    final list = lists[index];
                    return ListTile(
                      leading: const Icon(Icons.bookmark_outline),
                      title: Text(list.name),
                      subtitle: Text(
                        list.description?.isNotEmpty == true
                            ? '${l10n.movieCount(list.itemCount)} · '
                                  '${list.description}'
                            : l10n.movieCount(list.itemCount),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      trailing: PopupMenuButton<String>(
                        onSelected: (value) {
                          if (value == 'edit') {
                            _showEditor(context, ref, existing: list);
                          } else if (value == 'delete') {
                            _confirmDelete(context, ref, list);
                          }
                        },
                        itemBuilder: (context) => [
                          PopupMenuItem(
                            value: 'edit',
                            child: Text(l10n.commonEdit),
                          ),
                          PopupMenuItem(
                            value: 'delete',
                            child: Text(l10n.commonDelete),
                          ),
                        ],
                      ),
                      onTap: () => context.push('/watchlist/${list.id}'),
                    );
                  },
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
