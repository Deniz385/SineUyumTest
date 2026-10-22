import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/movie_widgets.dart';
import '../../widgets/state_views.dart';

class WatchlistDetailPage extends ConsumerWidget {
  const WatchlistDetailPage({super.key, required this.listId});

  final int listId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final detail = ref.watch(watchlistDetailProvider(listId));

    return Scaffold(
      appBar: AppBar(
        title: Text(detail.value?.name ?? l10n.watchlistFallbackTitle),
      ),
      body: detail.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(watchlistDetailProvider(listId)),
        ),
        data: (list) {
          if (list.items.isEmpty) {
            return EmptyView(
              icon: Icons.movie_outlined,
              message: l10n.watchlistEmpty,
            );
          }

          return RefreshIndicator(
            onRefresh: () async {
              ref.invalidate(watchlistDetailProvider(listId));
              await ref.read(watchlistDetailProvider(listId).future);
            },
            child: ListView.builder(
              itemCount: list.items.length,
              itemBuilder: (context, index) {
                final item = list.items[index];
                return MovieListTile(
                  movieId: item.movieId,
                  title: item.title,
                  posterPath: item.posterPath,
                  trailing: IconButton(
                    tooltip: l10n.removeFromList,
                    icon: const Icon(Icons.delete_outline),
                    onPressed: () async {
                      try {
                        await ref
                            .read(watchlistRepositoryProvider)
                            .removeMovie(listId, item.movieId);
                        ref.invalidate(watchlistDetailProvider(listId));
                        ref.invalidate(myWatchlistsProvider);
                      } catch (error) {
                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(content: Text(error.toString())),
                          );
                        }
                      }
                    },
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
