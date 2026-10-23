import 'package:sine_uyum_mobile/data/models/movie.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';

/// Empty movie rows (the rows under a thin feed stay hidden).
final noMovieRows = [
  popularMoviesProvider.overrideWith((ref) async => const <Movie>[]),
  topRatedMoviesProvider.overrideWith((ref) async => const <Movie>[]),
  nowPlayingProvider.overrideWith((ref) async => const <Movie>[]),
];
