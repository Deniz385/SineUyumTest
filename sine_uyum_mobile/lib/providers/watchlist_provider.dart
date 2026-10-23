import 'dart:math' as math;

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_exception.dart';
import '../data/models/app_user.dart';
import '../data/models/watchlist.dart';
import '../data/repositories/watchlist_repository.dart';
import 'repository_providers.dart';
import 'session_provider.dart';

/// A refusal by the server (403/404: not my list, a hidden profile) is
/// final; only failures to reach it are retried, with Riverpod's backoff.
/// Retrying a 404 would also flash the detail screen's read-only fallback.
Duration? retryUnlessRefused(int retryCount, Object error) {
  if (error is ApiException && !error.isNetworkError) return null;
  return ProviderContainer.defaultRetry(retryCount, error);
}

String _myId(Ref ref) {
  final id = ref.read(sessionProvider).user?.id;
  if (id == null || id.isEmpty) throw StateError('Not signed in');
  return id;
}

/// One list with its items and members. Mutations update the loaded state
/// in place (no reload flash) and refresh [myWatchlistsProvider], whose
/// counts change with them. Disposed when the screen closes, so opening a
/// shared list again shows what the others added in the meantime.
final watchlistDetailProvider = AsyncNotifierProvider.autoDispose
    .family<WatchlistDetailNotifier, WatchlistDetail, int>(
      WatchlistDetailNotifier.new,
      retry: retryUnlessRefused,
    );

class WatchlistDetailNotifier extends AsyncNotifier<WatchlistDetail> {
  WatchlistDetailNotifier(this.listId);

  final int listId;

  @override
  Future<WatchlistDetail> build() =>
      ref.watch(watchlistRepositoryProvider).detail(listId);

  void _update(WatchlistDetail Function(WatchlistDetail current) change) {
    final current = state.value;
    if (!ref.mounted || current == null) return;
    state = AsyncData(change(current));
  }

  Future<void> removeMovie(int movieId) async {
    await ref.read(watchlistRepositoryProvider).removeMovie(listId, movieId);
    _update(
      (list) => list.copyWith(
        items: list.items.where((i) => i.movieId != movieId).toList(),
      ),
    );
    if (ref.mounted) ref.invalidate(myWatchlistsProvider);
  }

  /// Owner only: invites [user]; they appear as pending until they accept.
  Future<void> invite(UserSearchResult user) async {
    final member = await ref
        .read(watchlistRepositoryProvider)
        .invite(listId, user.id);
    _update(
      (list) => list.copyWith(
        members: [
          ...list.members.where((m) => m.user.id != member.user.id),
          member,
        ],
      ),
    );
  }

  /// Owner only: removes a member or cancels a pending invitation.
  Future<void> removeMember(String memberId) async {
    await ref.read(watchlistRepositoryProvider).removeMember(listId, memberId);
    _update(
      (list) => list.copyWith(
        members: list.members.where((m) => m.user.id != memberId).toList(),
      ),
    );
    if (ref.mounted) ref.invalidate(myWatchlistsProvider);
  }

  /// A member leaves the list. The screen closes afterwards; its state is
  /// not touched (it would only flash the "no access" error).
  Future<void> leave() async {
    await ref
        .read(watchlistRepositoryProvider)
        .removeMember(listId, _myId(ref));
    if (ref.mounted) ref.invalidate(myWatchlistsProvider);
  }
}

/// Pending invitations to shared lists. Not kept in the offline cache: an
/// answer only works online, and a stale copy would offer invitations that
/// were already answered or cancelled.
final watchlistInvitationsProvider =
    AsyncNotifierProvider<
      WatchlistInvitationsNotifier,
      List<WatchlistInvitation>
    >(WatchlistInvitationsNotifier.new);

class WatchlistInvitationsNotifier
    extends AsyncNotifier<List<WatchlistInvitation>> {
  @override
  Future<List<WatchlistInvitation>> build() {
    // Another account signing in must not see the previous one's invitations.
    ref.watch(sessionProvider.select((s) => s.user?.id));
    return ref.watch(watchlistRepositoryProvider).invitations();
  }

  void _drop(int listId) {
    final current = state.value;
    if (!ref.mounted || current == null) return;
    state = AsyncData(current.where((i) => i.listId != listId).toList());
  }

  /// Joins the list: it moves from the invitations to my lists.
  Future<void> accept(int listId) async {
    await ref.read(watchlistRepositoryProvider).accept(listId);
    _drop(listId);
    if (ref.mounted) ref.invalidate(myWatchlistsProvider);
  }

  Future<void> decline(int listId) async {
    await ref
        .read(watchlistRepositoryProvider)
        .removeMember(listId, _myId(ref));
    _drop(listId);
  }
}

// --- Public lists ---

/// My latest like/save state of others' public lists, by list id. Every
/// screen showing such a list (discovery, saved lists, a profile, the
/// read-only view) renders this over its own copy, so a like on one screen
/// shows on the others without refetching them. Updates are optimistic and
/// rolled back when the request fails.
final listReactionsProvider =
    NotifierProvider<ListReactionsNotifier, Map<int, ListReaction>>(
      ListReactionsNotifier.new,
    );

class ListReactionsNotifier extends Notifier<Map<int, ListReaction>> {
  final _pending = <String>{};

  @override
  Map<int, ListReaction> build() {
    // Another account signing in starts from the server's data.
    ref.watch(sessionProvider.select((s) => s.user?.id));
    return const {};
  }

  void _set(int listId, ListReaction reaction) {
    if (ref.mounted) state = {...state, listId: reaction};
  }

  /// [current] is what the screen shows. Ignored while the same change for
  /// that list is still in flight.
  Future<void> toggleLike(int listId, ListReaction current) async {
    final key = 'like $listId';
    if (!_pending.add(key)) return;
    final liked = !current.liked;
    _set(
      listId,
      current.copyWith(
        liked: liked,
        likeCount: math.max(0, current.likeCount + (liked ? 1 : -1)),
      ),
    );
    try {
      final result = await ref
          .read(watchlistRepositoryProvider)
          .setLiked(listId, liked);
      _set(
        listId,
        (state[listId] ?? current).copyWith(
          liked: result.liked,
          likeCount: result.likeCount,
        ),
      );
    } catch (_) {
      _set(
        listId,
        (state[listId] ?? current).copyWith(
          liked: current.liked,
          likeCount: current.likeCount,
        ),
      );
      rethrow;
    } finally {
      _pending.remove(key);
    }
  }

  /// Saving or unsaving also refreshes my saved lists.
  Future<void> toggleSave(int listId, ListReaction current) async {
    final key = 'save $listId';
    if (!_pending.add(key)) return;
    final saved = !current.saved;
    _set(
      listId,
      current.copyWith(
        saved: saved,
        saveCount: math.max(0, current.saveCount + (saved ? 1 : -1)),
      ),
    );
    try {
      final result = await ref
          .read(watchlistRepositoryProvider)
          .setSaved(listId, saved);
      _set(
        listId,
        (state[listId] ?? current).copyWith(
          saved: result.saved,
          saveCount: result.saveCount,
        ),
      );
      if (ref.mounted) ref.invalidate(savedListsProvider);
    } catch (_) {
      _set(
        listId,
        (state[listId] ?? current).copyWith(
          saved: current.saved,
          saveCount: current.saveCount,
        ),
      );
      rethrow;
    } finally {
      _pending.remove(key);
    }
  }
}

/// The pages of list cards loaded so far.
class ListCardsState {
  const ListCardsState({
    this.items = const [],
    this.page = 1,
    this.hasMore = false,
    this.isLoadingMore = false,
    this.loadMoreFailed = false,
  });

  final List<WatchlistCard> items;
  final int page;
  final bool hasMore;
  final bool isLoadingMore;

  /// The last [ListCardsNotifier.loadMore] failed: the screen offers a retry
  /// instead of trying again on its own.
  final bool loadMoreFailed;
}

/// Loads the first page of cards; [loadMore] appends the next one.
abstract class ListCardsNotifier extends AsyncNotifier<ListCardsState> {
  Future<WatchlistCardPage> fetch(WatchlistRepository repository, int page);

  @override
  Future<ListCardsState> build() async {
    // Watched: the repository is rebuilt on a language change (posters are
    // localized).
    final page = await fetch(ref.watch(watchlistRepositoryProvider), 1);
    return ListCardsState(
      items: page.items,
      page: page.page,
      hasMore: page.hasMore,
    );
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.isLoadingMore) return;
    state = AsyncData(
      ListCardsState(
        items: current.items,
        page: current.page,
        hasMore: current.hasMore,
        isLoadingMore: true,
      ),
    );
    try {
      final next = await fetch(
        ref.read(watchlistRepositoryProvider),
        current.page + 1,
      );
      if (!ref.mounted) return;
      // A list that moved up between two requests is not shown twice.
      final seen = {for (final card in current.items) card.id};
      state = AsyncData(
        ListCardsState(
          items: [
            ...current.items,
            ...next.items.where((c) => !seen.contains(c.id)),
          ],
          page: next.page,
          hasMore: next.hasMore,
        ),
      );
    } catch (_) {
      // The loaded pages are kept.
      if (ref.mounted) {
        state = AsyncData(
          ListCardsState(
            items: current.items,
            page: current.page,
            hasMore: current.hasMore,
            loadMoreFailed: true,
          ),
        );
      }
    }
  }
}

/// One discovery tab with its search text.
typedef DiscoverQuery = ({DiscoverSort sort, String query});

/// Discovery, per tab and search. Disposed when the screen closes, so it is
/// fresh the next time.
final discoverListsProvider = AsyncNotifierProvider.autoDispose
    .family<DiscoverListsNotifier, ListCardsState, DiscoverQuery>(
      DiscoverListsNotifier.new,
      retry: retryUnlessRefused,
    );

class DiscoverListsNotifier extends ListCardsNotifier {
  DiscoverListsNotifier(this.query);

  final DiscoverQuery query;

  @override
  Future<WatchlistCardPage> fetch(WatchlistRepository repository, int page) =>
      repository.discover(sort: query.sort, query: query.query, page: page);
}

/// Lists I saved (on the Lists tab). Refreshed by [ListReactionsNotifier]
/// after every save or unsave.
final savedListsProvider =
    AsyncNotifierProvider<SavedListsNotifier, ListCardsState>(
      SavedListsNotifier.new,
      retry: retryUnlessRefused,
    );

class SavedListsNotifier extends ListCardsNotifier {
  @override
  Future<ListCardsState> build() {
    // Another account signing in must not see the previous one's lists.
    ref.watch(sessionProvider.select((s) => s.user?.id));
    return super.build();
  }

  @override
  Future<WatchlistCardPage> fetch(WatchlistRepository repository, int page) =>
      repository.saved(page: page);
}

/// A profile's public lists (the first page, for its carousel). Fails with
/// 403/404 when the profile's details are hidden from me.
final userPublicListsProvider = FutureProvider.autoDispose
    .family<WatchlistCardPage, String>(
      (ref, userId) => ref.watch(watchlistRepositoryProvider).userLists(userId),
      retry: retryUnlessRefused,
    );

/// The read-only view of a list: someone else's public list, or one sent to
/// me in a message.
final publicWatchlistProvider = FutureProvider.autoDispose
    .family<WatchlistDetail, int>(
      (ref, listId) =>
          ref.watch(watchlistRepositoryProvider).publicDetail(listId),
      retry: retryUnlessRefused,
    );

/// After I create, edit or delete one of my lists: my lists, that list and
/// my profile's public lists may have changed.
void refreshMyWatchlists(WidgetRef ref, {int? listId}) {
  ref.invalidate(myWatchlistsProvider);
  if (listId != null) ref.invalidate(watchlistDetailProvider(listId));
  final myId = ref.read(sessionProvider).user?.id;
  if (myId != null) ref.invalidate(userPublicListsProvider(myId));
}
