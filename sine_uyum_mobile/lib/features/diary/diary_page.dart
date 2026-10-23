import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/network/api_exception.dart';
import '../../data/models/diary.dart';
import '../../l10n/l10n.dart';
import '../../providers/diary_provider.dart';
import '../../providers/session_provider.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/state_views.dart';
import 'diary_entry_sheet.dart';

/// A user's film diary, one year at a time: a month chart, the totals and
/// the entries grouped by month. The owner can edit and delete entries.
class DiaryPage extends ConsumerStatefulWidget {
  const DiaryPage({super.key, required this.userId});

  final String userId;

  @override
  ConsumerState<DiaryPage> createState() => _DiaryPageState();
}

class _DiaryPageState extends ConsumerState<DiaryPage> {
  /// Null until a year is picked: the newest year with entries is shown.
  int? _year;

  DiaryKey get _key => (userId: widget.userId, year: _year);

  /// A private profile (403) or a block (404): the server explains which.
  static bool _isAccessDenied(Object error) =>
      error is ApiException &&
      (error.statusCode == 403 || error.statusCode == 404);

  Future<void> _edit(DiaryEntry entry) async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    final notifier = ref.read(diaryProvider(_key).notifier);
    final saved = await DiaryEntrySheet.show(
      context,
      movieTitle: entry.movie.title,
      entry: entry,
      onSubmit: (draft) => notifier.updateEntry(entry.id, draft),
    );
    if (saved == true) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.diaryEntryUpdated)));
    }
  }

  Future<void> _delete(DiaryEntry entry) async {
    final l10n = context.l10n;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.diaryDeleteTitle),
        content: Text(
          l10n.diaryDeleteBody(
            entry.movie.title,
            DateFormat.yMMMMd(l10n.localeName).format(entry.watchedOn),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.commonCancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.commonDelete),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(diaryProvider(_key).notifier).deleteEntry(entry.id);
      messenger.showSnackBar(SnackBar(content: Text(l10n.diaryEntryDeleted)));
    } catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(error.toString())));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final isOwn = ref.watch(sessionProvider).user?.id == widget.userId;
    final diary = ref.watch(diaryProvider(_key));

    return Scaffold(
      appBar: AppBar(title: Text(isOwn ? l10n.myDiary : l10n.diaryTitle)),
      body: diary.when(
        loading: () => const LoadingView(),
        error: (error, _) => _isAccessDenied(error)
            ? EmptyView(
                icon: Icons.lock_outline_rounded,
                message: error.toString(),
              )
            : ErrorView(
                message: error.toString(),
                onRetry: () => ref.invalidate(diaryProvider(_key)),
              ),
        data: (state) => _buildDiary(state, isOwn: isOwn),
      ),
    );
  }

  Widget _buildDiary(DiaryState state, {required bool isOwn}) {
    final l10n = context.l10n;
    final summary = state.summary;
    final years = summary.availableYears.isEmpty
        ? const <int>[]
        : ({...summary.availableYears, summary.year}.toList()
            ..sort((a, b) => b.compareTo(a)));

    final String? emptyMessage = state.entries.isNotEmpty
        ? null
        : summary.availableYears.isEmpty
        ? (isOwn ? l10n.diaryEmptyOwn : l10n.diaryEmptyOther)
        : l10n.diaryEmptyYear(state.year);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(diaryProvider(_key));
        await ref.read(diaryProvider(_key).future);
      },
      child: NotificationListener<ScrollNotification>(
        onNotification: (notification) {
          if (notification.metrics.extentAfter < 400) {
            ref.read(diaryProvider(_key).notifier).loadMore();
          }
          return false;
        },
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.only(bottom: 24),
          children: [
            if (years.isNotEmpty)
              _YearSelector(
                years: years,
                selected: state.year,
                onSelected: (year) => setState(() => _year = year),
              ),
            if (summary.totalCount > 0) _SummaryCard(summary: summary),
            if (emptyMessage != null)
              EmptyView(icon: Icons.menu_book_rounded, message: emptyMessage)
            else
              for (final group in _byMonth(state.entries)) ...[
                _MonthHeader(
                  month: group.first.watchedOn,
                  count: group.first.watchedOn.year == summary.year
                      ? summary.countFor(group.first.watchedOn.month)
                      : null,
                ),
                for (final entry in group)
                  _EntryTile(
                    key: ValueKey(entry.id),
                    entry: entry,
                    onEdit: isOwn ? () => _edit(entry) : null,
                    onDelete: isOwn ? () => _delete(entry) : null,
                  ),
              ],
            if (state.hasMore)
              Padding(
                padding: const EdgeInsets.all(16),
                child: Center(
                  child: state.isLoadingMore
                      ? const CircularProgressIndicator()
                      : TextButton(
                          onPressed: () =>
                              ref.read(diaryProvider(_key).notifier).loadMore(),
                          child: Text(l10n.diaryLoadMore),
                        ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  /// Consecutive entries of the same month (the list is newest first).
  static List<List<DiaryEntry>> _byMonth(List<DiaryEntry> entries) {
    final groups = <List<DiaryEntry>>[];
    for (final entry in entries) {
      final last = groups.isEmpty ? null : groups.last.first.watchedOn;
      if (last != null &&
          last.year == entry.watchedOn.year &&
          last.month == entry.watchedOn.month) {
        groups.last.add(entry);
      } else {
        groups.add([entry]);
      }
    }
    return groups;
  }
}

class _YearSelector extends StatelessWidget {
  const _YearSelector({
    required this.years,
    required this.selected,
    required this.onSelected,
  });

  final List<int> years;
  final int selected;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
      child: Row(
        children: [
          for (final year in years)
            Padding(
              padding: const EdgeInsetsDirectional.only(end: 8),
              child: ChoiceChip(
                // A year is not a quantity: no digit grouping ("2.026").
                label: Text('$year'),
                selected: year == selected,
                onSelected: (_) {
                  if (year != selected) onSelected(year);
                },
              ),
            ),
        ],
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.summary});

  final DiarySummary summary;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 8),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              l10n.diaryYearTotal(summary.totalCount, summary.year),
              style: theme.textTheme.titleMedium,
            ),
            if (summary.rewatchCount > 0) ...[
              const SizedBox(height: 2),
              Row(
                children: [
                  Icon(
                    Icons.replay_rounded,
                    size: 14,
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    l10n.diaryRewatchCount(summary.rewatchCount),
                    style: theme.textTheme.bodySmall,
                  ),
                ],
              ),
            ],
            const SizedBox(height: 16),
            DiaryMonthChart(summary: summary),
          ],
        ),
      ),
    );
  }
}

/// Twelve bars, one per month, scaled to the busiest month.
class DiaryMonthChart extends StatelessWidget {
  const DiaryMonthChart({super.key, required this.summary});

  final DiarySummary summary;

  static const _barHeight = 72.0;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final monthFormat = DateFormat.MMM(l10n.localeName);
    final busiest = math.max(1, summary.monthCounts.reduce(math.max));

    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        for (var month = 1; month <= 12; month++)
          Expanded(
            child: Builder(
              builder: (context) {
                final count = summary.countFor(month);
                final label = monthFormat.format(DateTime(summary.year, month));
                return Semantics(
                  label: '$label: ${l10n.movieCount(count)}',
                  excludeSemantics: true,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      SizedBox(
                        height: _barHeight + 18,
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.end,
                          children: [
                            if (count > 0)
                              FittedBox(
                                fit: BoxFit.scaleDown,
                                child: Text(
                                  '$count',
                                  style: theme.textTheme.labelSmall,
                                ),
                              ),
                            const SizedBox(height: 2),
                            Container(
                              height: count == 0
                                  ? 2
                                  : math.max(4, _barHeight * count / busiest),
                              margin: const EdgeInsets.symmetric(horizontal: 3),
                              decoration: BoxDecoration(
                                color: count == 0
                                    ? theme.colorScheme.surfaceContainerHighest
                                    : theme.colorScheme.primary,
                                borderRadius: const BorderRadius.vertical(
                                  top: Radius.circular(4),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 4),
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        child: Text(label, style: theme.textTheme.labelSmall),
                      ),
                    ],
                  ),
                );
              },
            ),
          ),
      ],
    );
  }
}

class _MonthHeader extends StatelessWidget {
  const _MonthHeader({required this.month, this.count});

  final DateTime month;

  /// Entries of the whole month (the loaded ones may be fewer).
  final int? count;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Row(
        children: [
          Expanded(
            child: Text(
              DateFormat.yMMMM(l10n.localeName).format(month),
              style: theme.textTheme.titleSmall,
            ),
          ),
          if (count != null && count! > 0)
            Text(l10n.movieCount(count!), style: theme.textTheme.labelSmall),
        ],
      ),
    );
  }
}

class _EntryTile extends StatelessWidget {
  const _EntryTile({
    super.key,
    required this.entry,
    this.onEdit,
    this.onDelete,
  });

  final DiaryEntry entry;

  /// Own diary only; without them there is no menu.
  final VoidCallback? onEdit;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final muted = theme.colorScheme.onSurfaceVariant;
    final rating = entry.rating;
    final note = entry.note;

    return InkWell(
      onTap: () => context.push('/movie/${entry.movie.id}'),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 4, 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 36,
              child: Column(
                children: [
                  Text(
                    '${entry.watchedOn.day}',
                    style: theme.textTheme.titleLarge,
                  ),
                  Text(
                    DateFormat.E(l10n.localeName).format(entry.watchedOn),
                    style: theme.textTheme.labelSmall?.copyWith(color: muted),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            PosterImage(
              posterPath: entry.movie.posterPath,
              width: 44,
              height: 66,
              borderRadius: 6,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    entry.movie.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall,
                  ),
                  if (rating != null || entry.rewatch) ...[
                    const SizedBox(height: 4),
                    Row(
                      children: [
                        if (rating != null) ...[
                          const Icon(
                            Icons.star_rounded,
                            size: 14,
                            color: Colors.amber,
                          ),
                          const SizedBox(width: 2),
                          Text(
                            l10n.ratingOutOfTen(rating),
                            style: theme.textTheme.bodySmall,
                          ),
                          const SizedBox(width: 10),
                        ],
                        if (entry.rewatch)
                          Tooltip(
                            message: l10n.diaryRewatch,
                            child: Icon(
                              Icons.replay_rounded,
                              size: 16,
                              color: muted,
                            ),
                          ),
                      ],
                    ),
                  ],
                  if (note != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      note,
                      maxLines: 4,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.bodySmall?.copyWith(
                        fontStyle: FontStyle.italic,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            if (onEdit != null || onDelete != null)
              PopupMenuButton<String>(
                tooltip: l10n.moreTooltip,
                onSelected: (action) =>
                    action == 'edit' ? onEdit?.call() : onDelete?.call(),
                itemBuilder: (_) => [
                  PopupMenuItem(value: 'edit', child: Text(l10n.commonEdit)),
                  PopupMenuItem(
                    value: 'delete',
                    child: Text(l10n.commonDelete),
                  ),
                ],
              )
            else
              const SizedBox(width: 12),
          ],
        ),
      ),
    );
  }
}
