import 'dart:async';
import 'dart:math' as math;

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/diary.dart';
import '../data/repositories/diary_repository.dart';
import 'repository_providers.dart';

/// Whose diary and which year; a null [year] opens the newest year that has
/// entries (the current year when the diary is empty).
typedef DiaryKey = ({String userId, int? year});

class DiaryState {
  const DiaryState({
    required this.summary,
    this.entries = const [],
    this.totalCount = 0,
    this.isLoadingMore = false,
  });

  final DiarySummary summary;

  /// The loaded entries of [year], newest first.
  final List<DiaryEntry> entries;

  /// All entries of [year] on the server (all pages).
  final int totalCount;
  final bool isLoadingMore;

  int get year => summary.year;
  bool get hasMore => entries.length < totalCount;

  DiaryState copyWith({
    List<DiaryEntry>? entries,
    int? totalCount,
    bool? isLoadingMore,
  }) => DiaryState(
    summary: summary,
    entries: entries ?? this.entries,
    totalCount: totalCount ?? this.totalCount,
    isLoadingMore: isLoadingMore ?? this.isLoadingMore,
  );
}

/// One year of a user's diary: the monthly summary and the entries, loaded
/// page by page. Logging a movie elsewhere invalidates the whole family;
/// edits and deletes here update the list at once and then reload it in the
/// background (counts, order and the year list may all change).
final diaryProvider = AsyncNotifierProvider.autoDispose
    .family<DiaryNotifier, DiaryState, DiaryKey>(DiaryNotifier.new);

class DiaryNotifier extends AsyncNotifier<DiaryState> {
  DiaryNotifier(this.key);

  final DiaryKey key;

  static const _pageSize = DiaryRepository.pageSize;

  @override
  Future<DiaryState> build() =>
      _load(ref.watch(diaryRepositoryProvider), key.year, pages: 1);

  Future<DiaryState> _load(
    DiaryRepository repository,
    int? year, {
    required int pages,
  }) async {
    DiarySummary summary;
    DiaryEntriesPage first;
    if (year == null) {
      var current = await repository.summary(key.userId);
      // The server defaults to the current year; with nothing logged yet
      // this year, the newest year that has entries is more useful.
      final years = current.availableYears;
      if (years.isNotEmpty && !years.contains(current.year)) {
        current = await repository.summary(key.userId, year: years.first);
      }
      summary = current;
      first = await repository.entries(key.userId, year: summary.year);
    } else {
      (summary, first) = await (
        repository.summary(key.userId, year: year),
        repository.entries(key.userId, year: year),
      ).wait;
    }

    var entries = first.items;
    var totalCount = first.totalCount;
    for (var page = 2; page <= pages && entries.length < totalCount; page++) {
      final next = await repository.entries(
        key.userId,
        year: summary.year,
        page: page,
      );
      entries = _merge(entries, next.items);
      totalCount = next.totalCount;
    }
    return DiaryState(
      summary: summary,
      entries: entries,
      totalCount: totalCount,
    );
  }

  /// Appends [more], skipping entries already loaded (a delete shifts the
  /// server's pages, so the next page may repeat one).
  static List<DiaryEntry> _merge(
    List<DiaryEntry> loaded,
    List<DiaryEntry> more,
  ) {
    final ids = {for (final e in loaded) e.id};
    return [...loaded, ...more.where((e) => ids.add(e.id))];
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.isLoadingMore) return;
    state = AsyncData(current.copyWith(isLoadingMore: true));
    try {
      // Counted from what is loaded, so after a delete the page that now
      // holds the next entry is requested again.
      final page = await ref
          .read(diaryRepositoryProvider)
          .entries(
            key.userId,
            year: current.year,
            page: current.entries.length ~/ _pageSize + 1,
          );
      if (!ref.mounted) return;
      final entries = _merge(current.entries, page.items);
      state = AsyncData(
        current.copyWith(
          entries: entries,
          // Nothing new: stop asking instead of looping on the same page.
          totalCount: entries.length == current.entries.length
              ? entries.length
              : page.totalCount,
          isLoadingMore: false,
        ),
      );
    } catch (_) {
      // The loaded entries stay; scrolling down retries.
      if (ref.mounted) state = AsyncData(current);
    }
  }

  /// Own diary only. Completes once saved; the reload that follows (the
  /// date may have moved the entry) runs in the background.
  Future<void> updateEntry(int entryId, DiaryDraft draft) async {
    final updated = await ref
        .read(diaryRepositoryProvider)
        .update(entryId, draft);
    _change(
      (s) => s.copyWith(
        entries: [for (final e in s.entries) e.id == entryId ? updated : e],
      ),
    );
    unawaited(reload());
  }

  /// Own diary only; the counts are reloaded in the background.
  Future<void> deleteEntry(int entryId) async {
    await ref.read(diaryRepositoryProvider).delete(entryId);
    _change(
      (s) => s.copyWith(
        entries: s.entries.where((e) => e.id != entryId).toList(),
        totalCount: math.max(0, s.totalCount - 1),
      ),
    );
    unawaited(reload());
  }

  void _change(DiaryState Function(DiaryState current) change) {
    final current = state.value;
    if (!ref.mounted || current == null) return;
    state = AsyncData(change(current));
  }

  /// Fetches the summary and as many entries as are loaded, keeping the
  /// current list on screen meanwhile. A failure keeps the local update.
  Future<void> reload() async {
    final current = state.value;
    if (!ref.mounted || current == null) return;
    try {
      final fresh = await _load(
        ref.read(diaryRepositoryProvider),
        current.year,
        pages: math.max(1, (current.entries.length / _pageSize).ceil()),
      );
      if (ref.mounted) state = AsyncData(fresh);
    } catch (_) {}
  }
}
