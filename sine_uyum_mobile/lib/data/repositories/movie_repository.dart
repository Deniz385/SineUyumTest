import '../../core/network/api_client.dart';
import '../models/movie.dart';

class MovieRepository {
  MovieRepository(this._client);

  final ApiClient _client;

  Future<List<Movie>> nowPlaying() => _list('/api/v1/movies/now_playing');

  Future<List<Movie>> popular() => _list('/api/v1/movies/popular');

  /// Well-known films of all time (the server serves pages 1–5).
  Future<List<Movie>> topRated({int page = 1}) =>
      _list('/api/v1/movies/top_rated', query: {'page': page});

  Future<List<Movie>> search(String query) async {
    if (query.trim().isEmpty) return const [];
    return _list('/api/v1/movies/search', query: {'query': query});
  }

  Future<Movie> detail(int movieId) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/movies/$movieId',
    );
    return Movie.fromJson(data);
  }

  /// The movie must exist in our own database before it can be rated (the
  /// API enforces a foreign key), so it is saved first.
  Future<void> rate(Movie movie, int rating) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/ratings/addmovie',
      body: movie.toRatingPayload(),
    );
    await _client.post<Map<String, dynamic>>(
      '/api/v1/ratings',
      body: {'movieId': movie.id, 'rating': rating},
    );
  }

  /// Rates by id only: the server adds a movie it doesn't have yet from
  /// TMDb (the first-run welcome rates straight from TMDb lists).
  Future<void> rateById(int movieId, int rating) async {
    await _client.post<Map<String, dynamic>>(
      '/api/v1/ratings',
      body: {'movieId': movieId, 'rating': rating},
    );
  }

  Future<List<Movie>> _list(String path, {Map<String, dynamic>? query}) async {
    final data = await _client.get<Map<String, dynamic>>(path, query: query);
    final results = data['results'] as List? ?? const [];
    return results
        .map((item) => Movie.fromJson((item as Map).cast<String, dynamic>()))
        .toList();
  }
}
