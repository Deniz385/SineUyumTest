import '../../core/network/api_client.dart';
import '../models/movie.dart';

class MovieRepository {
  MovieRepository(this._client);

  final ApiClient _client;

  Future<List<Movie>> nowPlaying() => _list('/api/movies/now_playing');

  Future<List<Movie>> popular() => _list('/api/movies/popular');

  Future<List<Movie>> search(String query) async {
    if (query.trim().isEmpty) return const [];
    return _list('/api/movies/search', query: {'query': query});
  }

  Future<Movie> detail(int movieId) async {
    final data = await _client.get<Map<String, dynamic>>('/api/movies/$movieId');
    return Movie.fromJson(data);
  }

  /// Puan vermeden önce filmin kendi veritabanımızda bulunması gerekiyor
  /// (API tarafı yabancı anahtar kontrolü yapıyor), bu yüzden önce kaydedilir.
  Future<void> rate(Movie movie, int rating) async {
    await _client.post<Map<String, dynamic>>(
      '/api/ratings/addmovie',
      body: movie.toRatingPayload(),
    );
    await _client.post<Map<String, dynamic>>(
      '/api/ratings',
      body: {'movieId': movie.id, 'rating': rating},
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
