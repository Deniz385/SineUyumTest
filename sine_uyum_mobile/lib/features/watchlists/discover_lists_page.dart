import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/repositories/watchlist_repository.dart';
import '../../l10n/l10n.dart';
import '../../providers/watchlist_provider.dart';
import '../../widgets/state_views.dart';
import 'list_cards.dart';

/// Others' public lists: trending this week, popular, new and "for you",
/// with a search over names and descriptions.
class DiscoverListsPage extends ConsumerStatefulWidget {
  const DiscoverListsPage({super.key});

  @override
  ConsumerState<DiscoverListsPage> createState() => _DiscoverListsPageState();
}

class _DiscoverListsPageState extends ConsumerState<DiscoverListsPage> {
  final _search = TextEditingController();
  Timer? _debounce;
  String _query = '';
  DiscoverSort _sort = DiscoverSort.trending;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      if (mounted) setState(() => _query = value.trim());
    });
    // The clear button follows the text right away.
    setState(() {});
  }

  void _clearSearch() {
    _debounce?.cancel();
    _search.clear();
    setState(() => _query = '');
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Scaffold(
      appBar: AppBar(title: Text(l10n.discoverListsTitle)),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: TextField(
              controller: _search,
              onChanged: _onSearchChanged,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: l10n.discoverSearchHint,
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _search.text.isEmpty
                    ? null
                    : IconButton(
                        tooltip: l10n.commonCancel,
                        icon: const Icon(Icons.close),
                        onPressed: _clearSearch,
                      ),
                border: const OutlineInputBorder(),
                isDense: true,
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
            child: SizedBox(
              width: double.infinity,
              child: SegmentedButton<DiscoverSort>(
                showSelectedIcon: false,
                segments: [
                  for (final sort in DiscoverSort.values)
                    ButtonSegment(value: sort, label: Text(_label(l10n, sort))),
                ],
                selected: {_sort},
                onSelectionChanged: (selection) =>
                    setState(() => _sort = selection.first),
              ),
            ),
          ),
          Expanded(
            child: _DiscoverResults(query: (sort: _sort, query: _query)),
          ),
        ],
      ),
    );
  }

  static String _label(AppLocalizations l10n, DiscoverSort sort) =>
      switch (sort) {
        DiscoverSort.trending => l10n.discoverSortTrending,
        DiscoverSort.popular => l10n.discoverSortPopular,
        DiscoverSort.newest => l10n.discoverSortNew,
        DiscoverSort.forYou => l10n.discoverSortForYou,
      };
}

class _DiscoverResults extends ConsumerWidget {
  const _DiscoverResults({required this.query});

  final DiscoverQuery query;

  String _emptyMessage(AppLocalizations l10n) {
    if (query.query.isNotEmpty) return l10n.discoverEmptySearch(query.query);
    return switch (query.sort) {
      DiscoverSort.trending => l10n.discoverEmptyTrending,
      DiscoverSort.popular => l10n.discoverEmptyPopular,
      DiscoverSort.newest => l10n.discoverEmptyNew,
      DiscoverSort.forYou => l10n.discoverEmptyForYou,
    };
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = discoverListsProvider(query);
    final results = ref.watch(provider);

    Future<void> refresh() async {
      ref.invalidate(provider);
      await ref.read(provider.future);
    }

    return results.when(
      loading: () => const LoadingView(),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.invalidate(provider),
      ),
      data: (state) => RefreshIndicator(
        onRefresh: refresh,
        child: state.items.isEmpty
            ? ListView(
                children: [
                  const SizedBox(height: 32),
                  EmptyView(
                    icon: query.sort == DiscoverSort.forYou
                        ? Icons.favorite_border
                        : Icons.travel_explore,
                    message: _emptyMessage(context.l10n),
                  ),
                ],
              )
            : ListView.builder(
                padding: const EdgeInsets.fromLTRB(12, 4, 12, 16),
                itemCount: state.items.length + (state.hasMore ? 1 : 0),
                itemBuilder: (context, index) {
                  if (index < state.items.length) {
                    final card = state.items[index];
                    return WatchlistCardTile(
                      key: ValueKey(card.id),
                      card: card,
                    );
                  }
                  return LoadMoreFooter(
                    failed: state.loadMoreFailed,
                    onLoadMore: () => ref.read(provider.notifier).loadMore(),
                  );
                },
              ),
      ),
    );
  }
}

/// The end of a paged list: loads the next page as soon as it is built, or
/// offers a retry when that failed (instead of retrying in a loop).
class LoadMoreFooter extends StatefulWidget {
  const LoadMoreFooter({
    super.key,
    required this.failed,
    required this.onLoadMore,
  });

  final bool failed;
  final VoidCallback onLoadMore;

  @override
  State<LoadMoreFooter> createState() => _LoadMoreFooterState();
}

class _LoadMoreFooterState extends State<LoadMoreFooter> {
  @override
  void initState() {
    super.initState();
    if (!widget.failed) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) widget.onLoadMore();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Center(
        child: widget.failed
            ? OutlinedButton.icon(
                onPressed: widget.onLoadMore,
                icon: const Icon(Icons.refresh),
                label: Text(context.l10n.commonRetry),
              )
            : const CircularProgressIndicator(),
      ),
    );
  }
}
