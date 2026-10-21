import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/social.dart';
import '../../providers/social_provider.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';
import 'review_card.dart';

/// Takip ettiklerinin (ve kendi) puanları ve incelemeleri.
class FeedView extends ConsumerWidget {
  const FeedView({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feed = ref.watch(feedProvider);

    return feed.when(
      loading: () => const LoadingView(),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.invalidate(feedProvider),
      ),
      data: (state) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(feedProvider);
          await ref.read(feedProvider.future);
        },
        child: state.items.isEmpty
            ? ListView(
                children: const [
                  SizedBox(height: 48),
                  EmptyView(
                    icon: Icons.dynamic_feed_outlined,
                    message:
                        'Akışın henüz boş. Keşfet sekmesinden sana uyumlu '
                        'kişileri takip et ya da bir filme ilk incelemeyi '
                        'sen yaz.',
                  ),
                ],
              )
            : ListView.builder(
                padding: const EdgeInsets.all(12),
                itemCount: state.items.length + (state.hasMore ? 1 : 0),
                itemBuilder: (context, index) {
                  if (index == state.items.length) {
                    // Sona gelince sonraki sayfa.
                    WidgetsBinding.instance.addPostFrameCallback(
                      (_) => ref.read(feedProvider.notifier).loadMore(),
                    );
                    return const Padding(
                      padding: EdgeInsets.all(16),
                      child: Center(child: CircularProgressIndicator()),
                    );
                  }
                  final item = state.items[index];
                  final review = item.review;
                  return review != null
                      ? ReviewCard(
                          review: review,
                          showMovie: true,
                          subtitle: 'bir inceleme yazdı',
                        )
                      : _RatedCard(item: item);
                },
              ),
      ),
    );
  }
}

class _RatedCard extends StatelessWidget {
  const _RatedCard({required this.item});

  final FeedItem item;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AuthorRow(
              author: item.author,
              rating: item.rating,
              compatibility: item.authorCompatibility,
              subtitle: 'bir filmi puanladı',
              when: item.activityAt,
            ),
            const SizedBox(height: 10),
            InkWell(
              onTap: () => context.push('/movie/${item.movie.id}'),
              child: Row(
                children: [
                  PosterImage(
                    posterPath: item.movie.posterPath,
                    width: 40,
                    height: 60,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      item.movie.title,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
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
