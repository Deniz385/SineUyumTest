import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/skeleton.dart';
import '../../core/ui/state_views.dart';
import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/watchlist_provider.dart';
import 'list_cards.dart';

/// Lists on a profile. Mine: "My lists" (managed on `/watchlists`) and the
/// lists I saved; someone else's: their public lists.
class ProfileListsSection extends StatelessWidget {
  const ProfileListsSection({
    super.key,
    required this.userId,
    required this.isMine,
  });

  final String userId;
  final bool isMine;

  @override
  Widget build(BuildContext context) => isMine
      ? const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [_MyLists(), _SavedLists()],
        )
      : _PublicLists(userId: userId);
}

class _MyLists extends ConsumerWidget {
  const _MyLists();

  static const _shown = 5;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final lists = ref.watch(myWatchlistsProvider);
    final invitations =
        ref.watch(watchlistInvitationsProvider).value?.length ?? 0;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(
          title: l10n.myListsTitle,
          onSeeAll: () => context.push('/watchlists'),
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.md,
            AppSpace.xs,
            AppSpace.xxs,
          ),
        ),
        if (invitations > 0)
          ListTile(
            leading: const Icon(Icons.mail_outline_rounded),
            title: Text(l10n.watchlistInvitationsHeader(invitations)),
            trailing: const Icon(Icons.chevron_right_rounded),
            onTap: () => context.push('/watchlists'),
          ),
        lists.when(
          loading: () => const Shimmer(
            child: Column(children: [SkeletonListTile(), SkeletonListTile()]),
          ),
          error: (error, _) => ErrorView(
            message: error.toString(),
            onRetry: () => ref.invalidate(myWatchlistsProvider),
          ),
          data: (lists) => lists.isEmpty
              ? EmptyView(
                  icon: Icons.bookmark_outline_rounded,
                  message: l10n.watchlistsEmpty,
                  action: FilledButton.icon(
                    onPressed: () => context.push('/watchlists'),
                    icon: const Icon(Icons.add_rounded),
                    label: Text(l10n.createList),
                  ),
                )
              : Column(
                  children: [
                    for (final list in lists.take(_shown))
                      _MyListTile(list: list),
                    if (lists.length > _shown)
                      TextButton(
                        onPressed: () => context.push('/watchlists'),
                        child: Text(l10n.manageLists),
                      ),
                  ],
                ),
        ),
      ],
    );
  }
}

class _MyListTile extends StatelessWidget {
  const _MyListTile({required this.list});

  final WatchlistSummary list;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return ListTile(
      leading: Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: colors.surfaceRaised,
          borderRadius: BorderRadius.circular(AppRadii.sm),
        ),
        child: Icon(
          list.isShared ? Icons.group_rounded : Icons.bookmark_rounded,
          color: colors.primary,
        ),
      ),
      title: Row(
        children: [
          Flexible(child: Text(list.name, overflow: TextOverflow.ellipsis)),
          if (list.isPublic) ...[
            const SizedBox(width: 8),
            const PublicListBadge(),
          ],
        ],
      ),
      subtitle: Text(context.l10n.movieCount(list.itemCount)),
      trailing: const Icon(Icons.chevron_right_rounded),
      onTap: () => context.push('/watchlist/${list.id}'),
    );
  }
}

class _SavedLists extends ConsumerWidget {
  const _SavedLists();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final saved = ref.watch(savedListsProvider).value;
    final reactions = ref.watch(listReactionsProvider);
    if (saved == null) return const SizedBox.shrink();
    final cards = saved.items
        .where((card) => reactions[card.id]?.saved ?? card.savedByMe)
        .toList();
    if (cards.isEmpty) return const SizedBox.shrink();
    return _ListCarousel(title: context.l10n.savedListsTitle, cards: cards);
  }
}

/// The profile's public lists. Hidden while loading, when there are none
/// and when the server refuses them (403/404: the same rule as the profile
/// details).
class _PublicLists extends ConsumerWidget {
  const _PublicLists({required this.userId});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final page = ref.watch(userPublicListsProvider(userId)).value;
    if (page == null || page.items.isEmpty) return const SizedBox.shrink();
    return _ListCarousel(
      title: context.l10n.publicListsTitle,
      cards: page.items,
    );
  }
}

class _ListCarousel extends StatelessWidget {
  const _ListCarousel({required this.title, required this.cards});

  final String title;
  final List<WatchlistCard> cards;

  @override
  Widget build(BuildContext context) {
    final scaler = MediaQuery.textScalerOf(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(
          title: title,
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.lg,
            AppSpace.gutter,
            AppSpace.xs,
          ),
        ),
        SizedBox(
          height: WatchlistCardCompact.width + 12 + scaler.scale(1) * 42,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: AppSpace.gutter),
            itemCount: cards.length,
            separatorBuilder: (_, _) => const SizedBox(width: AppSpace.sm),
            itemBuilder: (context, index) =>
                WatchlistCardCompact(card: cards[index]),
          ),
        ),
      ],
    );
  }
}
