import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/watchlist.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/state_views.dart';

class WatchlistsPage extends ConsumerWidget {
  const WatchlistsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
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
                message: 'Henüz izleme listen yok.',
                action: FilledButton.icon(
                  onPressed: () => _showEditor(context, ref),
                  icon: const Icon(Icons.add),
                  label: const Text('Liste oluştur'),
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
                            ? '${list.itemCount} film · ${list.description}'
                            : '${list.itemCount} film',
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
                        itemBuilder: (context) => const [
                          PopupMenuItem(value: 'edit', child: Text('Düzenle')),
                          PopupMenuItem(value: 'delete', child: Text('Sil')),
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
        tooltip: 'Yeni liste',
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

    final saved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(existing == null ? 'Yeni liste' : 'Listeyi düzenle'),
        content: Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: nameController,
                autofocus: true,
                maxLength: 100,
                decoration: const InputDecoration(labelText: 'Liste adı'),
                validator: (value) => (value == null || value.trim().isEmpty)
                    ? 'Liste adı zorunludur.'
                    : null,
              ),
              TextFormField(
                controller: descriptionController,
                maxLength: 500,
                decoration: const InputDecoration(labelText: 'Açıklama'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) {
                Navigator.of(context).pop(true);
              }
            },
            child: const Text('Kaydet'),
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
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Liste silinsin mi?'),
        content: Text('"${list.name}" listesi kalıcı olarak silinecek.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Sil'),
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
