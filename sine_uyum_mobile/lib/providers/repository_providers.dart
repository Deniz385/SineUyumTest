import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/app_user.dart';
import '../data/models/event.dart';
import '../data/models/message.dart';
import '../data/models/movie.dart';
import '../data/models/profile.dart';
import '../data/models/watchlist.dart';
import '../data/repositories/event_repository.dart';
import '../data/repositories/message_repository.dart';
import '../data/repositories/movie_repository.dart';
import '../data/repositories/notification_repository.dart';
import '../data/repositories/social_repository.dart';
import '../data/repositories/watchlist_repository.dart';
import 'session_provider.dart';

final movieRepositoryProvider = Provider<MovieRepository>(
  (ref) => MovieRepository(ref.watch(apiClientProvider)),
);

final socialRepositoryProvider = Provider<SocialRepository>(
  (ref) => SocialRepository(ref.watch(apiClientProvider)),
);

final watchlistRepositoryProvider = Provider<WatchlistRepository>(
  (ref) => WatchlistRepository(ref.watch(apiClientProvider)),
);

final messageRepositoryProvider = Provider<MessageRepository>(
  (ref) => MessageRepository(ref.watch(apiClientProvider)),
);

final eventRepositoryProvider = Provider<EventRepository>(
  (ref) => EventRepository(ref.watch(apiClientProvider)),
);

final notificationRepositoryProvider = Provider<NotificationRepository>(
  (ref) => NotificationRepository(ref.watch(apiClientProvider)),
);

// --- Ekranların doğrudan izlediği veri sağlayıcıları ---

final nowPlayingProvider = FutureProvider<List<Movie>>(
  (ref) => ref.watch(movieRepositoryProvider).nowPlaying(),
);

final popularMoviesProvider = FutureProvider<List<Movie>>(
  (ref) => ref.watch(movieRepositoryProvider).popular(),
);

final movieDetailProvider = FutureProvider.family<Movie, int>(
  (ref, movieId) => ref.watch(movieRepositoryProvider).detail(movieId),
);

final movieSearchProvider = FutureProvider.family<List<Movie>, String>(
  (ref, query) => ref.watch(movieRepositoryProvider).search(query),
);

final userSearchProvider =
    FutureProvider.family<List<UserSearchResult>, String>(
      (ref, query) => ref.watch(socialRepositoryProvider).searchUsers(query),
    );

final userProfileProvider = FutureProvider.family<UserProfile, String>(
  (ref, userId) => ref.watch(socialRepositoryProvider).profile(userId),
);

final followStatusProvider = FutureProvider.family<FollowStatus, String>(
  (ref, userId) => ref.watch(socialRepositoryProvider).followStatus(userId),
);

final compatibilityProvider =
    FutureProvider.family<CompatibilityResult, String>(
      (ref, userId) =>
          ref.watch(socialRepositoryProvider).compatibility(userId),
    );

final myWatchlistsProvider = FutureProvider<List<WatchlistSummary>>(
  (ref) => ref.watch(watchlistRepositoryProvider).myLists(),
);

final watchlistDetailProvider = FutureProvider.family<WatchlistDetail, int>(
  (ref, listId) => ref.watch(watchlistRepositoryProvider).detail(listId),
);

final conversationsProvider = FutureProvider<List<Conversation>>(
  (ref) => ref.watch(messageRepositoryProvider).conversations(),
);

final messageThreadProvider = FutureProvider.family<List<ChatMessage>, String>(
  (ref, otherUserId) =>
      ref.watch(messageRepositoryProvider).thread(otherUserId),
);

final myEventProvider = FutureProvider<EventState>(
  (ref) => ref.watch(eventRepositoryProvider).myStatus(),
);
