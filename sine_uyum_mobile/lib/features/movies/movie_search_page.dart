import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/movie_widgets.dart';
import '../../widgets/state_views.dart';

class MovieSearchPage extends ConsumerStatefulWidget {
  const MovieSearchPage({super.key});

  @override
  ConsumerState<MovieSearchPage> createState() => _MovieSearchPageState();
}

class _MovieSearchPageState extends ConsumerState<MovieSearchPage> {
  final _controller = TextEditingController();
  Timer? _debounce;
  String _query = '';

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  /// Debounced by 400 ms so TMDb isn't queried on every keystroke.
  void _onChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      if (mounted) setState(() => _query = value.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _controller,
          autofocus: true,
          textInputAction: TextInputAction.search,
          onChanged: _onChanged,
          decoration: InputDecoration(
            hintText: l10n.movieSearchHint,
            border: InputBorder.none,
            filled: false,
            suffixIcon: _controller.text.isEmpty
                ? null
                : IconButton(
                    icon: const Icon(Icons.clear),
                    onPressed: () {
                      _controller.clear();
                      setState(() => _query = '');
                    },
                  ),
          ),
        ),
      ),
      body: _query.isEmpty
          ? EmptyView(icon: Icons.search, message: l10n.movieSearchPrompt)
          : ref
                .watch(movieSearchProvider(_query))
                .when(
                  loading: () => const LoadingView(),
                  error: (error, _) => ErrorView(
                    message: error.toString(),
                    onRetry: () => ref.invalidate(movieSearchProvider(_query)),
                  ),
                  data: (movies) => movies.isEmpty
                      ? EmptyView(message: l10n.movieSearchEmpty)
                      : ListView.builder(
                          itemCount: movies.length,
                          itemBuilder: (context, index) {
                            final movie = movies[index];
                            return MovieListTile(
                              movieId: movie.id,
                              title: movie.title,
                              posterPath: movie.posterPath,
                              subtitle: movie.year,
                            );
                          },
                        ),
                ),
    );
  }
}
