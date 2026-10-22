import '../../core/network/api_client.dart';
import '../models/diary.dart';

class DiaryRepository {
  DiaryRepository(this._client);

  final ApiClient _client;

  static const pageSize = 20;

  static String? _note(String? note) {
    final trimmed = note?.trim();
    return trimmed == null || trimmed.isEmpty ? null : trimmed;
  }

  /// Logs a watch. A [DiaryDraft.rating] also sets my rating of the movie.
  /// 409 when the movie is already logged on that day.
  Future<DiaryEntry> create(int movieId, DiaryDraft draft) async {
    final data = await _client.post<Map<String, dynamic>>(
      '/api/v1/diary',
      body: {
        'movieId': movieId,
        'watchedOn': formatDiaryDate(draft.watchedOn),
        'rewatch': draft.rewatch,
        'note': _note(draft.note),
        if (draft.rating != null) 'rating': draft.rating,
      },
    );
    return DiaryEntry.fromJson(data);
  }

  /// Replaces the date, rewatch flag and note (the rating is not part of
  /// an entry).
  Future<DiaryEntry> update(int entryId, DiaryDraft draft) async {
    final data = await _client.put<Map<String, dynamic>>(
      '/api/v1/diary/$entryId',
      body: {
        'watchedOn': formatDiaryDate(draft.watchedOn),
        'rewatch': draft.rewatch,
        'note': _note(draft.note),
      },
    );
    return DiaryEntry.fromJson(data);
  }

  Future<void> delete(int entryId) async {
    await _client.delete<Map<String, dynamic>>('/api/v1/diary/$entryId');
  }

  /// [userId]'s entries, newest first; only [year] when given.
  Future<DiaryEntriesPage> entries(
    String userId, {
    int? year,
    int page = 1,
    int pageSize = pageSize,
  }) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/diary/user/${Uri.encodeComponent(userId)}',
      query: {'year': ?year, 'page': page, 'pageSize': pageSize},
    );
    return DiaryEntriesPage.fromJson(data);
  }

  /// My entries for [movieId], newest first (empty when never logged).
  ///
  /// Not in the offline cache: it only pre-selects "Rewatch" in the add
  /// sheet, whose save needs the network anyway, and a stale copy could
  /// suggest a wrong rewatch state.
  Future<List<DiaryEntry>> movieEntries(int movieId) async {
    final data = await _client.get<List<dynamic>>(
      '/api/v1/diary/movie/$movieId',
    );
    return data
        .map(
          (item) => DiaryEntry.fromJson((item as Map).cast<String, dynamic>()),
        )
        .toList();
  }

  /// Counts per month for [year] (the server's current year by default).
  Future<DiarySummary> summary(String userId, {int? year}) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/diary/user/${Uri.encodeComponent(userId)}/summary',
      query: {'year': ?year},
    );
    return DiarySummary.fromJson(data);
  }
}
