import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/app_user.dart';
import '../../data/models/social.dart';
import '../../data/models/watchlist.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/watchlist_provider.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

/// What the sheet asks its page to do after closing.
enum WatchlistMembersAction { leave }

/// A shared list's people. The owner sees pending invitations too, can
/// remove members / cancel invitations and invite people by username; a
/// member can leave the list.
class WatchlistMembersSheet extends ConsumerStatefulWidget {
  const WatchlistMembersSheet({super.key, required this.listId});

  final int listId;

  static Future<WatchlistMembersAction?> show(
    BuildContext context, {
    required int listId,
  }) => showModalBottomSheet<WatchlistMembersAction>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => WatchlistMembersSheet(listId: listId),
  );

  @override
  ConsumerState<WatchlistMembersSheet> createState() =>
      _WatchlistMembersSheetState();
}

class _WatchlistMembersSheetState extends ConsumerState<WatchlistMembersSheet> {
  final _search = TextEditingController();
  Timer? _debounce;
  String _query = '';

  /// Last result shown inside the sheet (a SnackBar would be hidden behind
  /// it). Server error messages are shown as-is.
  String? _status;
  bool _statusIsError = false;
  final _busy = <String>{};

  WatchlistDetailNotifier get _notifier =>
      ref.read(watchlistDetailProvider(widget.listId).notifier);

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
  }

  Future<void> _run(
    String userId,
    Future<void> Function() action,
    String success,
  ) async {
    setState(() => _busy.add(userId));
    try {
      await action();
      if (!mounted) return;
      setState(() {
        _status = success;
        _statusIsError = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _status = error.toString();
        _statusIsError = true;
      });
    } finally {
      if (mounted) setState(() => _busy.remove(userId));
    }
  }

  Future<void> _invite(UserSearchResult user) async {
    final l10n = context.l10n;
    await _run(
      user.id,
      () => _notifier.invite(user),
      l10n.watchlistInvited(user.userName),
    );
    if (mounted && !_statusIsError) {
      _debounce?.cancel();
      _search.clear();
      setState(() => _query = '');
    }
  }

  Future<void> _remove(WatchlistMember member) async {
    final l10n = context.l10n;
    final name = member.user.userName;
    if (member.accepted) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(l10n.watchlistRemoveMemberTitle(name)),
          content: Text(l10n.watchlistRemoveMemberBody),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: Text(l10n.commonCancel),
            ),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: Text(l10n.watchlistRemoveMember),
            ),
          ],
        ),
      );
      if (confirmed != true || !mounted) return;
    }
    await _run(
      member.user.id,
      () => _notifier.removeMember(member.user.id),
      member.accepted
          ? l10n.watchlistMemberRemoved(name)
          : l10n.watchlistInvitationCancelled(name),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final list = ref.watch(watchlistDetailProvider(widget.listId)).value;
    if (list == null) return const SizedBox(height: 160, child: LoadingView());

    final theme = Theme.of(context);
    final owner = list.owner;
    final pending = list.members.where((m) => !m.accepted).length;

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.sizeOf(context).height * 0.85,
        ),
        child: ListView(
          shrinkWrap: true,
          padding: const EdgeInsets.fromLTRB(0, 0, 0, 16),
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                l10n.watchlistMembers,
                style: theme.textTheme.titleLarge,
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
              child: Text(
                [
                  l10n.watchlistPeopleCount(list.people.length),
                  if (list.isOwner && pending > 0)
                    l10n.watchlistPendingCount(pending),
                ].join(' · '),
                style: theme.textTheme.bodySmall,
              ),
            ),
            if (list.isOwner) ..._inviteSection(list),
            if (_status != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 4),
                child: Text(
                  _status!,
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: _statusIsError
                        ? theme.colorScheme.error
                        : theme.colorScheme.primary,
                  ),
                ),
              ),
            if (owner != null)
              _PersonTile(user: owner, status: l10n.watchlistOwnerRole),
            for (final member in list.members)
              _PersonTile(
                user: member.user,
                status: member.accepted
                    ? l10n.watchlistMemberAccepted
                    : l10n.watchlistMemberPending,
                pending: !member.accepted,
                trailing: list.isOwner
                    ? IconButton(
                        tooltip: member.accepted
                            ? l10n.watchlistRemoveMember
                            : l10n.watchlistCancelInvitation,
                        icon: Icon(
                          member.accepted
                              ? Icons.person_remove_outlined
                              : Icons.close,
                        ),
                        onPressed: _busy.contains(member.user.id)
                            ? null
                            : () => _remove(member),
                      )
                    : null,
              ),
            if (list.isOwner && list.members.isEmpty)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                child: Text(
                  l10n.watchlistNoMembersYet,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
            if (!list.isOwner)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: theme.colorScheme.error,
                  ),
                  onPressed: () =>
                      Navigator.of(context).pop(WatchlistMembersAction.leave),
                  icon: const Icon(Icons.logout),
                  label: Text(l10n.watchlistLeave),
                ),
              ),
          ],
        ),
      ),
    );
  }

  List<Widget> _inviteSection(WatchlistDetail list) {
    final l10n = context.l10n;
    final inList = {
      ?list.owner?.id,
      for (final member in list.members) member.user.id,
    };

    return [
      Padding(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 4),
        child: TextField(
          controller: _search,
          onChanged: _onSearchChanged,
          textInputAction: TextInputAction.search,
          decoration: InputDecoration(
            hintText: l10n.watchlistInviteHint,
            prefixIcon: const Icon(Icons.person_add_alt_1_outlined),
          ),
        ),
      ),
      if (_query.isNotEmpty)
        ref
            .watch(userSearchProvider(_query))
            .when(
              loading: () => const LoadingView(),
              error: (error, _) => Padding(
                padding: const EdgeInsets.all(16),
                child: Text(error.toString()),
              ),
              data: (users) => users.isEmpty
                  ? Padding(
                      padding: const EdgeInsets.all(16),
                      child: Text(l10n.userSearchEmpty),
                    )
                  : Column(
                      children: [
                        for (final user in users.take(8))
                          _PersonTile(
                            user: Author(
                              id: user.id,
                              userName: user.userName,
                              imageUrl: user.profileImageUrl,
                            ),
                            trailing: inList.contains(user.id)
                                ? Text(l10n.watchlistAlreadyInList)
                                : FilledButton.tonal(
                                    onPressed: _busy.contains(user.id)
                                        ? null
                                        : () => _invite(user),
                                    child: Text(l10n.watchlistInvite),
                                  ),
                          ),
                        const Divider(),
                      ],
                    ),
            ),
    ];
  }
}

class _PersonTile extends StatelessWidget {
  const _PersonTile({
    required this.user,
    this.status,
    this.pending = false,
    this.trailing,
  });

  final Author user;
  final String? status;
  final bool pending;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return ListTile(
      leading: Opacity(
        opacity: pending ? 0.5 : 1,
        child: UserAvatar(userName: user.userName, imageUrl: user.imageUrl),
      ),
      title: Text('@${user.userName}'),
      subtitle: status == null
          ? null
          : Row(
              children: [
                if (pending) ...[
                  const Icon(Icons.schedule, size: 14),
                  const SizedBox(width: 4),
                ],
                Flexible(
                  child: Text(
                    status!,
                    style: pending
                        ? theme.textTheme.bodySmall?.copyWith(
                            fontStyle: FontStyle.italic,
                          )
                        : null,
                  ),
                ),
              ],
            ),
      trailing: trailing,
    );
  }
}
