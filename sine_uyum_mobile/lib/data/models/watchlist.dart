import '../../l10n/l10n.dart';

/// Summary record in the `/api/v1/watchlist` list.
class WatchlistSummary {
  const WatchlistSummary({
    required this.id,
    required this.name,
    this.description,
    this.itemCount = 0,
  });

  final int id;
  final String name;
  final String? description;
  final int itemCount;

  factory WatchlistSummary.fromJson(Map<String, dynamic> json) {
    return WatchlistSummary(
      id: (json['id'] as num?)?.toInt() ?? 0,
      name: json['name'] as String? ?? L10n.current.untitledWatchlist,
      description: json['description'] as String?,
      itemCount: (json['itemCount'] as num?)?.toInt() ?? 0,
    );
  }
}

/// Response of `/api/v1/watchlist/{listId}` and
/// `/api/v1/watchlist/public/{listId}`.
class WatchlistDetail {
  const WatchlistDetail({
    required this.id,
    required this.name,
    this.description,
    this.ownerUsername,
    this.items = const [],
  });

  final int id;
  final String name;
  final String? description;
  final String? ownerUsername;
  final List<WatchlistItem> items;

  factory WatchlistDetail.fromJson(Map<String, dynamic> json) {
    return WatchlistDetail(
      id: (json['id'] as num?)?.toInt() ?? 0,
      name: json['name'] as String? ?? L10n.current.untitledWatchlist,
      description: json['description'] as String?,
      ownerUsername: json['ownerUsername'] as String?,
      items:
          (json['items'] as List?)
              ?.map(
                (e) =>
                    WatchlistItem.fromJson((e as Map).cast<String, dynamic>()),
              )
              .toList() ??
          const [],
    );
  }
}

class WatchlistItem {
  const WatchlistItem({
    required this.movieId,
    required this.title,
    this.posterPath,
    this.addedAt,
  });

  final int movieId;
  final String title;
  final String? posterPath;
  final DateTime? addedAt;

  factory WatchlistItem.fromJson(Map<String, dynamic> json) {
    return WatchlistItem(
      movieId: (json['movieId'] as num?)?.toInt() ?? 0,
      title: json['title'] as String? ?? L10n.current.untitledMovie,
      posterPath: json['posterPath'] as String?,
      addedAt: json['addedAt'] == null
          ? null
          : DateTime.tryParse(json['addedAt'] as String),
    );
  }
}
