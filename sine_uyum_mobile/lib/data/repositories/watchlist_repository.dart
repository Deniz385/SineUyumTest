import '../../core/network/api_client.dart';
import '../models/movie.dart';
import '../models/watchlist.dart';

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

  Future<void> create({required String name, String? description}) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/watchlist',
      body: {'name': name, 'description': description},
    );
  }

  Future<void> update(
    int listId, {
    required String name,
    String? description,
  }) async {
    await _client.put<Map<String, dynamic>>(
      '/api/v1/watchlist/$listId',
      body: {'name': name, 'description': description},
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
}
