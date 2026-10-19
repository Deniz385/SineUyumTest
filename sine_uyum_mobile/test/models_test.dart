import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/event.dart';
import 'package:sine_uyum_mobile/data/models/movie.dart';

void main() {
  group('Movie.fromJson', () {
    test('TMDb snake_case alanlarını okur', () {
      final movie = Movie.fromJson({
        'id': 550,
        'title': 'Dövüş Kulübü',
        'poster_path': '/poster.jpg',
        'release_date': '1999-10-15',
        'vote_average': 8.4,
      });

      expect(movie.id, 550);
      expect(movie.posterPath, '/poster.jpg');
      expect(movie.year, '1999');
      expect(movie.voteAverage, 8.4);
    });

    test('kendi API camelCase alanlarını okur', () {
      final movie = Movie.fromJson({
        'id': 550,
        'title': 'Dövüş Kulübü',
        'posterPath': '/poster.jpg',
      });

      expect(movie.posterPath, '/poster.jpg');
      expect(movie.year, isNull);
    });

    test('eksik alanlarda patlamaz', () {
      final movie = Movie.fromJson(const {});
      expect(movie.id, 0);
      expect(movie.title, 'Başlıksız');
      expect(movie.posterPath, isNull);
    });

    test('genres hem nesne hem metin listesini kabul eder', () {
      final fromObjects = Movie.fromJson({
        'id': 1,
        'genres': [
          {'id': 18, 'name': 'Dram'},
          {'id': 53, 'name': 'Gerilim'},
        ],
      });
      expect(fromObjects.genres, ['Dram', 'Gerilim']);
    });
  });

  group('AuthResult.fromJson', () {
    test('token, refresh token ve kullanıcıyı ayrıştırır', () {
      final result = AuthResult.fromJson({
        'token': 'jwt-token',
        'refreshToken': 'refresh-token',
        'expiresAt': '2026-10-19T12:00:00Z',
        'user': {
          'id': 'user-1',
          'userName': 'deneme',
          'isSubscribed': true,
          'roles': ['User', 'Admin'],
        },
      });

      expect(result.token, 'jwt-token');
      expect(result.refreshToken, 'refresh-token');
      expect(result.expiresAt?.isUtc, isTrue);
      expect(result.user.userName, 'deneme');
      expect(result.user.isSubscribed, isTrue);
      expect(result.user.isAdmin, isTrue);
    });

    test('user alanı yoksa boş kullanıcı döner', () {
      final result = AuthResult.fromJson({'token': 't', 'refreshToken': 'r'});
      expect(result.user.id, '');
      expect(result.user.isAdmin, isFalse);
    });
  });

  group('EventState.fromJson', () {
    test('MATCHED durumunu grup, oy ve önerilerle ayrıştırır', () {
      final state = EventState.fromJson({
        'status': 'MATCHED',
        'event': {
          'id': 3,
          'locationName': 'Kadıköy Sineması',
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
          {'id': 550, 'title': 'Dövüş Kulübü', 'posterPath': '/p.jpg'},
        ],
      });

      expect(state.status, EventStatus.matched);
      expect(state.event?.locationName, 'Kadıköy Sineması');
      expect(state.group?.members.length, 2);
      expect(state.votes.single.movieId, 550);
      expect(state.suggestedMovies.single.title, 'Dövüş Kulübü');
    });

    test('bilinmeyen durum none olarak ele alınır', () {
      final state = EventState.fromJson({'status': 'SOMETHING_ELSE'});
      expect(state.status, EventStatus.none);
    });

    test('NONE durumundaki mesajı taşır', () {
      final state = EventState.fromJson({
        'status': 'NONE',
        'message': 'Katılabileceğin yeni bir etkinlik bulunmuyor.',
      });
      expect(state.status, EventStatus.none);
      expect(state.message, contains('etkinlik bulunmuyor'));
    });
  });
}
