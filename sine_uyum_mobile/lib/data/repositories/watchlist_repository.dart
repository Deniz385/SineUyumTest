import '../../core/network/api_client.dart';
import '../models/movie.dart';
import '../models/watchlist.dart';

/// Discovery orders: trending (most saved/liked this week), popular (all
/// time), new (recently made public) and "for you" (by owners with a taste
/// like mine).
enum DiscoverSort {
  trending('trending'),
  popular('popular'),
  newest('new'),
  forYou('forYou');

  const DiscoverSort(this.apiValue);

  final String apiValue;
}

class WatchlistRepository {
  WatchlistRepository(this._client);

  final ApiClient _client;

  Future<List<WatchlistSummary>> myLists() async {
    final data = await _client.get<List<dynamic>>('/api/v1/watchlist');
    return data
        .map(
          (item) =>
              WatchlistSummary.fromJson((item as Map).cast<String, dynamic>()),
        )
        .toList();
  }

  Future<WatchlistDetail> detail(int listId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId',
    );
    return WatchlistDetail.fromJson(data);
  }

  Future<WatchlistDetail> publicDetail(int listId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/watchlist/public/$listId',
    );
    return WatchlistDetail.fromJson(data);
  }

  Future<void> create({
    required String name,
    String? description,
    WatchlistVisibility visibility = WatchlistVisibility.private,
  }) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/watchlist',
      body: {
        'name': name,
        'description': description,
        'visibility': visibility.apiValue,
      },
    );
  }

  /// Owner only. A null [visibility] keeps the current one.
  Future<void> update(
    int listId, {
    required String name,
    String? description,
    WatchlistVisibility? visibility,
  }) async {
    await _client.put<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId',
      body: {
        'name': name,
        'description': description,
        'visibility': ?visibility?.apiValue,
      },
    );
  }

  Future<void> remove(int listId) async {
    await _client.delete<Map<String, dynamic>>('/api/v1/watchlist/$listId');
  }

  Future<void> addMovie(int listId, Movie movie) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId/movies',
      body: {
        'movieId': movie.id,
        'title': movie.title,
        'posterPath': movie.posterPath,
      },
    );
  }

  Future<void> removeMovie(int listId, int movieId) async {
    await _client.delete<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId/movies/$movieId',
    );
  }

  // --- Shared lists ---

  /// Lists I was invited to and have not answered yet.
  Future<List<WatchlistInvitation>> invitations() async {
    final data = await _client.get<List<dynamic>>(
      '/api/v1/watchlist/invitations',
    );
    return data
        .map(
          (item) => WatchlistInvitation.fromJson(
            (item as Map).cast<String, dynamic>(),
          ),
        )
        .toList();
  }

  /// Owner only. Returns the new (pending) member.
  Future<WatchlistMember> invite(int listId, String userId) async {
    final data = await _client.post<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId/members',
      body: {'userId': userId},
    );
    return WatchlistMember.fromJson(data);
  }

  Future<void> accept(int listId) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId/accept',
    );
  }

  /// The owner removes a member or cancels an invitation; with my own id it
  /// leaves the list or declines the invitation.
  Future<void> removeMember(int listId, String memberId) async {
    await _client.delete<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId/members/${Uri.encodeComponent(memberId)}',
    );
  }

  // --- Public lists ---

  static const pageSize = 20;

  Future<WatchlistCardPage> _cards(
    String path,
    Map<String, dynamic> query,
  ) async {
    final data = await _client.get<Map<String, dynamic>>(path, query: query);
    return WatchlistCardPage.fromJson(data);
  }

  /// Others' public lists; [query] searches the name and description.
  Future<WatchlistCardPage> discover({
    DiscoverSort sort = DiscoverSort.trending,
    String? query,
    int page = 1,
  }) {
    final q = query?.trim() ?? '';
    return _cards('/api/v1/watchlist/discover', {
      'sort': sort.apiValue,
      if (q.isNotEmpty) 'q': q,
      'page': page,
      'pageSize': pageSize,
    });
  }

  /// A profile's public lists (403/404 like the profile details).
  Future<WatchlistCardPage> userLists(String userId, {int page = 1}) => _cards(
    '/api/v1/watchlist/user/${Uri.encodeComponent(userId)}',
    {'page': page, 'pageSize': pageSize},
  );

  /// Lists I saved, latest saved first.
  Future<WatchlistCardPage> saved({int page = 1}) =>
      _cards('/api/v1/watchlist/saved', {'page': page, 'pageSize': pageSize});

  Future<({bool liked, int likeCount})> setLiked(int listId, bool liked) async {
    final path = '/api/v1/watchlist/$listId/like';
    final data = liked
        ? await _client.post<Map<String, dynamic>>(path)
        : await _client.delete<Map<String, dynamic>>(path);
    return (
      liked: data['liked'] as bool? ?? liked,
      likeCount: (data['likeCount'] as num?)?.toInt() ?? 0,
    );
  }

  Future<({bool saved, int saveCount})> setSaved(int listId, bool saved) async {
    final path = '/api/v1/watchlist/$listId/save';
    final data = saved
        ? await _client.post<Map<String, dynamic>>(path)
        : await _client.delete<Map<String, dynamic>>(path);
    return (
      saved: data['saved'] as bool? ?? saved,
      saveCount: (data['saveCount'] as num?)?.toInt() ?? 0,
    );
  }
}
