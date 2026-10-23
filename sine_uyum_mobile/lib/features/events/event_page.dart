import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/network/api_exception.dart';
import '../../data/models/event.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/state_views.dart';

/// Cinema event screen: joining, waiting to be matched, the group and the
/// movie vote.
class EventPage extends ConsumerWidget {
  const EventPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final event = ref.watch(myEventProvider);

    return event.when(
      loading: () => const LoadingView(),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.invalidate(myEventProvider),
      ),
      data: (state) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myEventProvider);
          await ref.read(myEventProvider.future);
        },
        child: switch (state.status) {
          EventStatus.matched => _MatchedView(state: state),
          EventStatus.pending => _PendingView(event: state.event),
          EventStatus.available => _AvailableView(event: state.event),
          EventStatus.none => _NoneView(message: state.message),
        },
      ),
    );
  }
}

class _NoneView extends StatelessWidget {
  const _NoneView({this.message});

  final String? message;

  @override
  Widget build(BuildContext context) {
    return ListView(
      children: [
        SizedBox(height: MediaQuery.sizeOf(context).height * 0.2),
        EmptyView(
          icon: Icons.event_busy_rounded,
          message: message ?? context.l10n.eventNoneAvailable,
        ),
      ],
    );
  }
}

class _AvailableView extends ConsumerStatefulWidget {
  const _AvailableView({this.event});

  final CinemaEvent? event;

  @override
  ConsumerState<_AvailableView> createState() => _AvailableViewState();
}

class _AvailableViewState extends ConsumerState<_AvailableView> {
  bool _isJoining = false;

  Future<void> _join() async {
    final event = widget.event;
    if (event == null) return;

    setState(() => _isJoining = true);
    try {
      await ref.read(eventRepositoryProvider).join(event.id);
      ref.invalidate(myEventProvider);
    } catch (error) {
      if (mounted) _showActionError(context, error);
    } finally {
      if (mounted) setState(() => _isJoining = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final event = widget.event;
    if (event == null) return const _NoneView();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _EventCard(event: event),
        const SizedBox(height: 16),
        FilledButton.icon(
          onPressed: _isJoining ? null : _join,
          icon: const Icon(Icons.how_to_reg_rounded),
          label: Text(_isJoining ? l10n.eventJoining : l10n.eventJoin),
        ),
        const SizedBox(height: 8),
        Text(
          l10n.eventJoinHint,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ],
    );
  }
}

class _PendingView extends StatelessWidget {
  const _PendingView({this.event});

  final CinemaEvent? event;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (event != null) _EventCard(event: event!),
        const SizedBox(height: 24),
        const Icon(Icons.hourglass_top_rounded, size: 40),
        const SizedBox(height: 12),
        Text(
          l10n.eventAwaitingMatch,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        Text(
          l10n.eventAwaitingMatchHint,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ],
    );
  }
}

class _MatchedView extends ConsumerStatefulWidget {
  const _MatchedView({required this.state});

  final EventState state;

  @override
  ConsumerState<_MatchedView> createState() => _MatchedViewState();
}

class _MatchedViewState extends ConsumerState<_MatchedView> {
  bool _isVoting = false;

  Future<void> _vote(int movieId) async {
    final groupId = widget.state.group?.id;
    if (groupId == null || _isVoting) return;

    setState(() => _isVoting = true);
    try {
      await ref
          .read(eventRepositoryProvider)
          .vote(groupId: groupId, movieId: movieId);
      ref.invalidate(myEventProvider);
    } catch (error) {
      if (mounted) _showActionError(context, error);
    } finally {
      if (mounted) setState(() => _isVoting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final state = widget.state;
    final group = state.group;
    final currentUserId = ref.watch(sessionProvider).user?.id;

    final myVote = state.votes
        .where((vote) => vote.userId == currentUserId)
        .map((vote) => vote.movieId)
        .firstOrNull;

    // Votes per movie.
    final voteCounts = <int, int>{};
    for (final vote in state.votes) {
      voteCounts[vote.movieId] = (voteCounts[vote.movieId] ?? 0) + 1;
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (state.event != null) _EventCard(event: state.event!),
        const SizedBox(height: 20),
        Text(
          l10n.eventYourGroup,
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        if (group == null || group.members.isEmpty)
          Text(l10n.eventLoadingMembers)
        else
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              for (final member in group.members)
                InkWell(
                  onTap: () => context.push('/profile/${member.id}'),
                  child: Column(
                    children: [
                      UserAvatar(
                        userName: member.userName,
                        imageUrl: member.profileImageUrl,
                        radius: 24,
                      ),
                      const SizedBox(height: 4),
                      SizedBox(
                        width: 64,
                        child: Text(
                          member.userName,
                          textAlign: TextAlign.center,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: Theme.of(context).textTheme.labelSmall,
                        ),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        const SizedBox(height: 24),
        Text(
          l10n.eventMovieVote,
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 4),
        Text(l10n.eventVoteHint, style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(height: 12),
        if (state.suggestedMovies.isEmpty)
          Text(l10n.eventNoSuggestions)
        else
          for (final movie in state.suggestedMovies)
            Card(
              margin: const EdgeInsets.only(bottom: 10),
              child: ListTile(
                leading: PosterImage(
                  posterPath: movie.posterPath,
                  width: 44,
                  height: 66,
                ),
                title: Text(movie.title),
                subtitle: Text(l10n.eventVoteCount(voteCounts[movie.id] ?? 0)),
                trailing: Icon(
                  myVote == movie.id
                      ? Icons.check_circle_rounded
                      : Icons.radio_button_unchecked_rounded,
                  color: myVote == movie.id
                      ? Theme.of(context).colorScheme.primary
                      : null,
                ),
                onTap: _isVoting ? null : () => _vote(movie.id),
              ),
            ),
      ],
    );
  }
}

class _EventCard extends StatelessWidget {
  const _EventCard({required this.event});

  final CinemaEvent event;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              event.locationName,
              style: Theme.of(context).textTheme.titleMedium,
            ),
            if (event.eventDate != null) ...[
              const SizedBox(height: 8),
              Row(
                children: [
                  const Icon(Icons.calendar_today_rounded, size: 16),
                  const SizedBox(width: 6),
                  Text(
                    DateFormat.yMMMMd(l10n.localeName)
                        .add_Hm()
                        .format(event.eventDate!.toLocal()),
                  ),
                ],
              ),
            ],
            if (event.address != null && event.address!.isNotEmpty) ...[
              const SizedBox(height: 6),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(Icons.place_rounded, size: 16),
                  const SizedBox(width: 6),
                  Expanded(child: Text(event.address!)),
                ],
              ),
            ],
            if (event.groupSize > 0) ...[
              const SizedBox(height: 6),
              Row(
                children: [
                  const Icon(Icons.groups_rounded, size: 16),
                  const SizedBox(width: 6),
                  Text(l10n.eventGroupSize(event.groupSize)),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The server enforces the paid feature itself: on a 402 the message is shown
/// with an action leading to the subscription page.
void _showActionError(BuildContext context, Object error) {
  final needsSubscription = error is ApiException && error.statusCode == 402;
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text(error.toString()),
      action: needsSubscription
          ? SnackBarAction(
              label: context.l10n.commonSubscription,
              onPressed: () => context.push('/subscription'),
            )
          : null,
    ),
  );
}
