import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/app_user.dart';
import '../data/models/watchlist.dart';
import 'repository_providers.dart';
import 'session_provider.dart';

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
