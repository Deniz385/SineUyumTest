import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/event.dart';
import 'package:sine_uyum_mobile/data/models/movie.dart';
import 'package:sine_uyum_mobile/data/models/watchlist.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';

import 'support/localized_app.dart';

void main() {
  tearDown(() => L10n.locale = en);

  group('Movie.fromJson', () {
    test("reads TMDb's snake_case fields", () {
      final movie = Movie.fromJson({
        'id': 550,
        'title': 'Fight Club',
        'poster_path': '/poster.jpg',
        'release_date': '1999-10-15',
        'vote_average': 8.4,
      });

      expect(movie.id, 550);
      expect(movie.posterPath, '/poster.jpg');
      expect(movie.year, '1999');
      expect(movie.voteAverage, 8.4);
    });

    test("reads our own API's camelCase fields", () {
      final movie = Movie.fromJson({
        'id': 550,
        'title': 'Fight Club',
        'posterPath': '/poster.jpg',
      });

      expect(movie.posterPath, '/poster.jpg');
      expect(movie.year, isNull);
    });

    test('does not break on missing fields', () {
      final movie = Movie.fromJson(const {});
      expect(movie.id, 0);
      expect(movie.title, 'Untitled');
      expect(movie.posterPath, isNull);
    });

    test('the missing-title fallback follows the app language', () {
      L10n.locale = tr;
      expect(Movie.fromJson(const {}).title, 'Başlıksız');
      expect(WatchlistSummary.fromJson(const {}).name, 'İsimsiz liste');

      L10n.locale = en;
      expect(WatchlistSummary.fromJson(const {}).name, 'Untitled list');
    });

    test('genres accept both objects and plain strings', () {
      final fromObjects = Movie.fromJson({
        'id': 1,
        'genres': [
          {'id': 18, 'name': 'Drama'},
          {'id': 53, 'name': 'Thriller'},
        ],
      });
      expect(fromObjects.genres, ['Drama', 'Thriller']);
    });
  });

  group('AuthResult.fromJson', () {
    test('parses the token, refresh token and user', () {
      final result = AuthResult.fromJson({
        'token': 'jwt-token',
        'refreshToken': 'refresh-token',
        'expiresAt': '2026-10-19T12:00:00Z',
        'user': {
          'id': 'user-1',
          'userName': 'tester',
          'isSubscribed': true,
          'roles': ['User', 'Admin'],
        },
      });

      expect(result.token, 'jwt-token');
      expect(result.refreshToken, 'refresh-token');
      expect(result.expiresAt?.isUtc, isTrue);
      expect(result.user.userName, 'tester');
      expect(result.user.isSubscribed, isTrue);
      expect(result.user.isAdmin, isTrue);
    });

    test('returns an empty user when the user field is missing', () {
      final result = AuthResult.fromJson({'token': 't', 'refreshToken': 'r'});
      expect(result.user.id, '');
      expect(result.user.isAdmin, isFalse);
    });
  });

  group('EventState.fromJson', () {
    test('parses MATCHED with group, votes and suggestions', () {
      final state = EventState.fromJson({
        'status': 'MATCHED',
        'event': {
          'id': 3,
          'locationName': 'Kadikoy Cinema',
          'eventDate': '2026-11-01T19:30:00Z',
          'groupSize': 4,
        },
        'group': {
          'id': 9,
          'members': [
            {'id': 'u1', 'userName': 'ali'},
            {'id': 'u2', 'userName': 'ayse'},
          ],
        },
        'votes': [
          {'userId': 'u1', 'movieId': 550},
        ],
        'suggestedMovies': [
          {'id': 550, 'title': 'Fight Club', 'posterPath': '/p.jpg'},
        ],
      });

      expect(state.status, EventStatus.matched);
      expect(state.event?.locationName, 'Kadikoy Cinema');
      expect(state.group?.members.length, 2);
      expect(state.votes.single.movieId, 550);
      expect(state.suggestedMovies.single.title, 'Fight Club');
    });

    test('an unknown status is treated as none', () {
      final state = EventState.fromJson({'status': 'SOMETHING_ELSE'});
      expect(state.status, EventStatus.none);
    });

    test('carries the message of the NONE status', () {
      final state = EventState.fromJson({
        'status': 'NONE',
        'message': "There's no new event you can join right now.",
      });
      expect(state.status, EventStatus.none);
      expect(state.message, contains('no new event'));
    });
  });
}
