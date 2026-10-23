import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/movie_widgets.dart';
import '../../core/ui/search_field.dart';
import '../../core/ui/skeleton.dart';
import '../../core/ui/state_views.dart';
import '../../core/ui/user_row.dart';
import '../../data/models/movie.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/social_provider.dart';
import '../shell/tab_scaffold.dart';
import '../social/follow_button.dart';
import '../watchlists/discover_lists_page.dart';

/// The Discover tab's sections; `/discover?section=lists` opens one.
enum DiscoverSection {
  movies,
  lists,
  people;

  static DiscoverSection parse(String? value) => DiscoverSection.values
      .firstWhere((s) => s.name == value, orElse: () => DiscoverSection.movies);
}

/// Discover: search movies, public lists and people (docs §7).
class DiscoverPage extends StatefulWidget {
  const DiscoverPage({super.key, this.section = DiscoverSection.movies});

  final DiscoverSection section;

  @override
  State<DiscoverPage> createState() => _DiscoverPageState();
}

class _DiscoverPageState extends State<DiscoverPage>
    with SingleTickerProviderStateMixin {
  late final _tabs = TabController(
    length: DiscoverSection.values.length,
    initialIndex: widget.section.index,
    vsync: this,
  );

  @override
  void didUpdateWidget(DiscoverPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    // A deep link to another section while the tab is already open.
    if (oldWidget.section != widget.section) {
      _tabs.animateTo(widget.section.index);
    }
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return TabScaffold(
      title: l10n.navDiscover,
      body: Column(
        children: [
          TabBar(
            controller: _tabs,
            tabs: [
              Tab(text: l10n.discoverTabMovies),
              Tab(text: l10n.discoverTabLists),
              Tab(text: l10n.discoverTabPeople),
            ],
          ),
          Expanded(
            child: TabBarView(
              controller: _tabs,
              children: const [
                DiscoverMoviesView(),
                DiscoverListsView(),
                DiscoverPeopleView(),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Movie search; without a query, rows of popular, now playing and top
/// rated films.
class DiscoverMoviesView extends ConsumerStatefulWidget {
  const DiscoverMoviesView({super.key});

  @override
  ConsumerState<DiscoverMoviesView> createState() => _DiscoverMoviesViewState();
}

class _DiscoverMoviesViewState extends ConsumerState<DiscoverMoviesView>
    with AutomaticKeepAliveClientMixin {
  String _query = '';

  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final l10n = context.l10n;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.sm,
            AppSpace.gutter,
            0,
          ),
          child: SearchField(
            hint: l10n.movieSearchHint,
            onQuery: (query) => setState(() => _query = query),
          ),
        ),
        Expanded(
          child: _query.isEmpty
              ? const _MovieRows()
              : _MovieResults(query: _query),
        ),
      ],
    );
  }
}

class _MovieRows extends ConsumerWidget {
  const _MovieRows();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    Widget row(String title, FutureProvider<List<Movie>> provider) {
      final value = ref.watch(provider);
      return MovieRow(
        title: title,
        movies: value.value,
        error: value.hasError && !value.hasValue ? value.error : null,
        onRetry: () => ref.invalidate(provider),
      );
    }

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(popularMoviesProvider);
        ref.invalidate(nowPlayingProvider);
        ref.invalidate(topRatedMoviesProvider);
      },
      child: ListView(
        padding: const EdgeInsets.only(bottom: AppSpace.xxl),
        children: [
          row(l10n.homePopular, popularMoviesProvider),
          row(l10n.homeNowPlaying, nowPlayingProvider),
          row(l10n.homeTopRated, topRatedMoviesProvider),
        ],
      ),
    );
  }
}

class _MovieResults extends ConsumerWidget {
  const _MovieResults({required this.query});

  final String query;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    return ref
        .watch(movieSearchProvider(query))
        .when(
          loading: () => const SkeletonPosterGrid(),
          error: (error, _) => ErrorView(
            message: error.toString(),
            onRetry: () => ref.invalidate(movieSearchProvider(query)),
          ),
          data: (movies) => movies.isEmpty
              ? EmptyView(
                  icon: Icons.search_off_rounded,
                  message: l10n.movieSearchEmpty,
                )
              : PosterGrid(movies: movies),
        );
  }
}

/// Movies as a grid of poster cards (three across on phones).
class PosterGrid extends StatelessWidget {
  const PosterGrid({super.key, required this.movies, this.padding});

  final List<Movie> movies;
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      const gap = AppSpace.sm;
      final columns = constraints.maxWidth >= 600 ? 5 : 3;
      final cell =
          (constraints.maxWidth - AppSpace.gutter * 2 - gap * (columns - 1)) /
          columns;
      return GridView.builder(
        padding: padding ?? const EdgeInsets.all(AppSpace.gutter),
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: columns,
          crossAxisSpacing: gap,
          mainAxisSpacing: AppSpace.md,
          mainAxisExtent: PosterCard.heightFor(context, cell),
        ),
        itemCount: movies.length,
        itemBuilder: (context, index) => PosterCard.movie(
          movies[index],
          size: PosterCardSize.s,
          width: cell,
        ),
      );
    },
  );
}

/// People: suggestions to follow; with a query, a username search sorted by
/// compatibility.
class DiscoverPeopleView extends ConsumerStatefulWidget {
  const DiscoverPeopleView({super.key});

  @override
  ConsumerState<DiscoverPeopleView> createState() => _DiscoverPeopleViewState();
}

class _DiscoverPeopleViewState extends ConsumerState<DiscoverPeopleView>
    with AutomaticKeepAliveClientMixin {
  String _query = '';

  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final l10n = context.l10n;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.sm,
            AppSpace.gutter,
            0,
          ),
          child: SearchField(
            hint: l10n.userSearchHint,
            onQuery: (query) => setState(() => _query = query),
          ),
        ),
        Expanded(
          child: _query.isEmpty
              ? const PeopleSuggestionsList()
              : _UserResults(query: _query),
        ),
      ],
    );
  }
}

class _UserResults extends ConsumerWidget {
  const _UserResults({required this.query});

  final String query;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    return ref
        .watch(userSearchProvider(query))
        .when(
          loading: () => const LoadingView(),
          error: (error, _) => ErrorView(
            message: error.toString(),
            onRetry: () => ref.invalidate(userSearchProvider(query)),
          ),
          data: (users) => users.isEmpty
              ? EmptyView(
                  icon: Icons.person_search_rounded,
                  message: l10n.userSearchEmpty,
                )
              : ListView.builder(
                  padding: const EdgeInsets.symmetric(vertical: AppSpace.xs),
                  itemCount: users.length,
                  itemBuilder: (context, index) {
                    final user = users[index];
                    return UserRow(
                      userId: user.id,
                      userName: user.userName,
                      imageUrl: user.profileImageUrl,
                      subtitle: user.bio,
                      compatibility: user.compatibilityScore,
                      commonMovieCount: user.commonMovieCount,
                    );
                  },
                ),
        );
  }
}

/// `GET /people/suggestions` with follow buttons: by taste when possible
/// ("92% compatible · 4 films in common"), otherwise popular people.
class PeopleSuggestionsList extends ConsumerWidget {
  const PeopleSuggestionsList({super.key, this.shrinkWrap = false});

  /// Inside another scrollable (the first-run welcome).
  final bool shrinkWrap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final suggestions = ref.watch(peopleSuggestionsProvider);
    return suggestions.when(
      loading: () => const LoadingView(rows: 5),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.invalidate(peopleSuggestionsProvider),
      ),
      data: (result) {
        if (result.items.isEmpty) {
          return EmptyView(
            icon: Icons.people_outline_rounded,
            message: l10n.peopleSuggestionsEmpty,
          );
        }
        final children = [
          SectionHeader(
            title: result.byTaste
                ? l10n.peopleSuggestedTaste
                : l10n.peopleSuggestedPopular,
            padding: const EdgeInsets.fromLTRB(
              AppSpace.gutter,
              AppSpace.md,
              AppSpace.gutter,
              0,
            ),
          ),
          if (!result.byTaste)
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpace.gutter,
                AppSpace.xxs,
                AppSpace.gutter,
                AppSpace.xs,
              ),
              child: Text(
                l10n.peopleSuggestedPopularHint,
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          for (final person in result.items)
            UserRow(
              key: ValueKey(person.user.id),
              userId: person.user.id,
              userName: person.user.userName,
              imageUrl: person.user.imageUrl,
              subtitle: person.compatibility != null
                  ? l10n.peopleTasteLine(
                      l10n.percent(person.compatibility!),
                      person.commonMovieCount,
                    )
                  : [
                      l10n.followerCount(person.followerCount),
                      if (person.bio?.isNotEmpty == true) person.bio!,
                    ].join(' · '),
              trailing: FollowButton(
                userId: person.user.id,
                userName: person.user.userName,
              ),
            ),
        ];
        return shrinkWrap
            ? Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: children,
              )
            : RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(peopleSuggestionsProvider);
                  await ref.read(peopleSuggestionsProvider.future);
                },
                child: ListView(
                  padding: const EdgeInsets.only(bottom: AppSpace.xl),
                  children: children,
                ),
              );
      },
    );
  }
}
