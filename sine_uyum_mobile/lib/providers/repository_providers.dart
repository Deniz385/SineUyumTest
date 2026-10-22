import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_client.dart';
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
import 'locale_provider.dart';
import 'session_provider.dart';

// Watching the API language rebuilds every repository on a language change,
// so the data providers below (which watch them) refetch server-localized
// content instead of keeping it in the old language.
ApiClient _client(Ref ref) {
  ref.watch(apiLanguageProvider);
  return ref.watch(apiClientProvider);
}

final movieRepositoryProvider = Provider<MovieRepository>(
  (ref) => MovieRepository(_client(ref)),
);

final socialRepositoryProvider = Provider<SocialRepository>(
  (ref) => SocialRepository(_client(ref)),
);

final watchlistRepositoryProvider = Provider<WatchlistRepository>(
  (ref) => WatchlistRepository(_client(ref)),
);

final messageRepositoryProvider = Provider<MessageRepository>(
  (ref) => MessageRepository(_client(ref)),
);

final eventRepositoryProvider = Provider<EventRepository>(
  (ref) => EventRepository(_client(ref)),
);

final notificationRepositoryProvider = Provider<NotificationRepository>(
  (ref) => NotificationRepository(_client(ref)),
);

// --- Data providers watched directly by the screens ---

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

final conversationsProvider = FutureProvider<List<Conversation>>(
  (ref) => ref.watch(messageRepositoryProvider).conversations(),
);

final myEventProvider = FutureProvider<EventState>(
  (ref) => ref.watch(eventRepositoryProvider).myStatus(),
);

final privacySettingsProvider = FutureProvider<PrivacySettings>(
  (ref) => ref.watch(socialRepositoryProvider).privacy(),
);

final blockedUsersProvider = FutureProvider<List<BlockedUser>>(
  (ref) => ref.watch(socialRepositoryProvider).blockedUsers(),
);
