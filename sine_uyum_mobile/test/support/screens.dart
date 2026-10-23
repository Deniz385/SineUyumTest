import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'app_harness.dart';

/// A key screen with sample data, shared by the screenshot and text-scale
/// tests.
class KeyScreen {
  const KeyScreen(
    this.name,
    this.location, {
    this.user,
    this.setUp = _none,
    this.interact,
  });

  final String name;
  final String location;
  final Map<String, Object?>? user;
  final void Function(AppHarness app) setUp;
  final Future<void> Function(WidgetTester tester)? interact;

  static void _none(AppHarness app) {}
}

Map<String, Object?> _author(String id, String name) => {
  'id': id,
  'userName': name,
};

final _feed = {
  'items': [
    {
      'activityAt': '2026-10-20T19:30:00Z',
      'author': _author('u7', 'zeynep'),
      'movie': {'id': 550, 'title': 'Fight Club', 'posterPath': '/fc.jpg'},
      'rating': 9,
      'authorCompatibility': 92,
      'review': {
        'id': 1,
        'movie': {'id': 550, 'title': 'Fight Club', 'posterPath': '/fc.jpg'},
        'author': _author('u7', 'zeynep'),
        'rating': 9,
        'text':
            'Still the sharpest satire of consumer culture — and the twist '
            'lands every time.',
        'likeCount': 12,
        'commentCount': 3,
        'authorCompatibility': 92,
      },
    },
    {
      'activityAt': '2026-10-19T10:00:00Z',
      'author': _author('u8', 'mehmet'),
      'movie': {'id': 13, 'title': 'Forrest Gump', 'posterPath': '/fg.jpg'},
      'rating': 7,
      'authorCompatibility': 61,
    },
    {
      'activityAt': '2026-10-18T10:00:00Z',
      'author': _author('u9', 'elif'),
      'movie': {'id': 680, 'title': 'Pulp Fiction', 'posterPath': '/pf.jpg'},
      'rating': 4,
      'authorCompatibility': 38,
    },
  ],
};

void _richSocial(AppHarness app) {
  app.server.routes['GET /api/v1/feed'] = (200, _feed);
  app.server.routes['GET /api/v1/taste-twin'] = (
    200,
    {
      'user': _author('u7', 'zeynep'),
      'compatibility': 92,
      'commonMovieCount': 14,
    },
  );
}

final _movieDetail = {
  'id': 550,
  'title': 'Fight Club',
  'poster_path': '/fc.jpg',
  'backdrop_path': '/fcb.jpg',
  'release_date': '1999-10-15',
  'runtime': 139,
  'vote_average': 8.4,
  'overview':
      'A ticking-time-bomb insomniac and a slippery soap salesman channel '
      'primal male aggression into a shocking new form of therapy.',
  'genres': [
    {'id': 18, 'name': 'Drama'},
    {'id': 53, 'name': 'Thriller'},
  ],
  'credits': {
    'cast': [
      {'id': 819, 'name': 'Edward Norton', 'character': 'Narrator'},
      {'id': 287, 'name': 'Brad Pitt', 'character': 'Tyler Durden'},
      {'id': 1283, 'name': 'Helena Bonham Carter', 'character': 'Marla'},
    ],
  },
};

void _profile(AppHarness app, String id, String name) {
  app.server.routes['GET /api/v1/profile/$id'] = (
    200,
    {
      'id': id,
      'userName': name,
      'bio': 'Cinephile. Midnight screenings and slow cinema.',
      'ratings': [
        {'movieId': 550, 'rating': 10, 'title': 'Fight Club'},
        {'movieId': 13, 'rating': 8, 'title': 'Forrest Gump'},
        {'movieId': 680, 'rating': 7, 'title': 'Pulp Fiction'},
      ],
      'statistics': {
        'totalRatings': 128,
        'averageRating': 7.4,
        'totalMoviesInWatchlists': 23,
        'topRatedMovies': [
          {'movieId': 550, 'rating': 10, 'title': 'Fight Club'},
        ],
      },
    },
  );
  app.server.routes['GET /api/v1/follow/$id/status'] = (
    200,
    {'isFollowing': false, 'followerCount': 48, 'followingCount': 31},
  );
  app.server.routes['GET /api/v1/compatibility/$id'] = (
    200,
    {'compatibilityScore': 82, 'commonMovieCount': 14},
  );
  app.server.routes['GET /api/v1/profile/$id/identity'] = (
    200,
    {'title': 'The Midnight Dreamer', 'summary': 'Dark, moody dramas.'},
  );
}

final _newUser = {
  'id': 'u1',
  'userName': 'ayse',
  'roles': ['User'],
  'onboardingCompleted': false,
};

final keyScreens = <KeyScreen>[
  KeyScreen('welcome', '/welcome', user: _newUser),
  KeyScreen(
    'welcome-rate',
    '/welcome',
    user: _newUser,
    interact: (tester) async {
      final start = find.byType(FilledButton);
      await tester.scrollUntilVisible(start, 200);
      await tester.tap(start);
      await AppHarness.settle(tester);
    },
  ),
  KeyScreen('home', '/home', setUp: _richSocial),
  const KeyScreen('home-thin', '/home'),
  const KeyScreen('discover-movies', '/discover'),
  const KeyScreen('discover-people', '/discover?section=people'),
  KeyScreen(
    'discover-lists',
    '/discover?section=lists',
    setUp: (app) => app.server.routes['GET /api/v1/watchlist/discover'] = (
      200,
      {
        'items': [
          {
            'id': 4,
            'name': 'Rainy Sunday comfort films',
            'description': 'Warm blankets, warmer stories.',
            'itemCount': 12,
            'likeCount': 31,
            'saveCount': 9,
            'owner': _author('u8', 'mehmet'),
            'ownerCompatibility': 78,
          },
        ],
        'page': 1,
        'hasMore': false,
      },
    ),
  ),
  KeyScreen(
    'movie',
    '/movie/550',
    setUp: (app) {
      app.server.routes['GET /api/v1/movies/550'] = (200, _movieDetail);
      app.server.routes['GET /api/v1/reviews/movie/550'] = (
        200,
        {
          'myRating': 9,
          'forYou': {'score': 8.7, 'basedOn': 6},
          'reviews': [(_feed['items']! as List).first['review']],
          'totalCount': 1,
        },
      );
    },
  ),
  KeyScreen(
    'profile-own',
    '/profile',
    setUp: (app) => _profile(app, 'u1', 'ayse'),
  ),
  KeyScreen(
    'profile-other',
    '/profile/u7',
    setUp: (app) => _profile(app, 'u7', 'zeynep'),
  ),
  const KeyScreen('my-lists', '/watchlists'),
  KeyScreen(
    'messages',
    '/messages',
    setUp: (app) => app.server.routes['GET /api/v1/messages'] = (
      200,
      [
        {
          'otherUserId': 'u7',
          'otherUserUsername': 'zeynep',
          'lastMessageContent': 'Did you see the new Villeneuve?',
          'lastMessageSent': '2026-10-22T18:00:00Z',
          'isRead': false,
        },
        {
          'otherUserId': 'u8',
          'otherUserUsername': 'mehmet',
          'lastMessageContent': 'Saturday screening works for me',
          'lastMessageSent': '2026-10-20T18:00:00Z',
          'isRead': true,
        },
      ],
    ),
  ),
  const KeyScreen('event', '/event'),
  const KeyScreen('notifications', '/notifications'),
  const KeyScreen('settings', '/settings'),
  KeyScreen(
    'login',
    '/login',
    setUp: (app) => app.tokens.access = app.tokens.refresh = null,
  ),
  KeyScreen(
    'register',
    '/register',
    setUp: (app) => app.tokens.access = app.tokens.refresh = null,
  ),
];

/// A screen by name.
KeyScreen keyScreen(String name) =>
    keyScreens.firstWhere((s) => s.name == name);
