import '../../l10n/l10n.dart';
import 'social.dart' show Author;

Map<String, dynamic>? _map(Object? value) =>
    value is Map ? value.cast<String, dynamic>() : null;

/// Summary record in the `/api/v1/watchlist` list: my own lists and the
/// shared lists I am an accepted member of.
class WatchlistSummary {
  const WatchlistSummary({
    required this.id,
    required this.name,
    this.description,
    this.itemCount = 0,
    this.isShared = false,
    this.isOwner = true,
    this.ownerUsername,
    this.memberCount = 1,
  });

  final int id;
  final String name;
  final String? description;
  final int itemCount;

  /// The list has at least one accepted member, or I am a member of it.
  final bool isShared;
  final bool isOwner;
  final String? ownerUsername;

  /// Accepted people, owner included.
  final int memberCount;

  factory WatchlistSummary.fromJson(Map<String, dynamic> json) {
    return WatchlistSummary(
      id: (json['id'] as num?)?.toInt() ?? 0,
      name: json['name'] as String? ?? L10n.current.untitledWatchlist,
      description: json['description'] as String?,
      itemCount: (json['itemCount'] as num?)?.toInt() ?? 0,
      isShared: json['isShared'] as bool? ?? false,
      // Older responses had no membership fields: every list was my own.
      isOwner: json['isOwner'] as bool? ?? true,
      ownerUsername: json['ownerUsername'] as String?,
      memberCount: (json['memberCount'] as num?)?.toInt() ?? 1,
    );
  }
}

/// Response of `/api/v1/watchlist/{listId}` and
/// `/api/v1/watchlist/public/{listId}` (the public one has no membership
/// fields, only [ownerUsername]).
class WatchlistDetail {
  const WatchlistDetail({
    required this.id,
    required this.name,
    this.description,
    this.ownerUsername,
    this.items = const [],
    this.isOwner = true,
    this.owner,
    this.members = const [],
  });

  final int id;
  final String name;
  final String? description;
  final String? ownerUsername;
  final List<WatchlistItem> items;
  final bool isOwner;
  final Author? owner;

  /// Members excluding the owner; pending invitations are only sent to the
  /// owner.
  final List<WatchlistMember> members;

  List<WatchlistMember> get acceptedMembers =>
      members.where((m) => m.accepted).toList();

  /// Shared with at least one accepted member, or I am a member.
  bool get isShared => !isOwner || members.any((m) => m.accepted);

  /// The list's people: the owner followed by the accepted members.
  List<Author> get people => [
    ?owner,
    for (final member in acceptedMembers) member.user,
  ];

  WatchlistDetail copyWith({
    List<WatchlistItem>? items,
    List<WatchlistMember>? members,
  }) => WatchlistDetail(
    id: id,
    name: name,
    description: description,
    ownerUsername: ownerUsername,
    items: items ?? this.items,
    isOwner: isOwner,
    owner: owner,
    members: members ?? this.members,
  );

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
      isOwner: json['isOwner'] as bool? ?? true,
      owner: _map(json['owner']) == null
          ? null
          : Author.fromJson(_map(json['owner'])),
      members:
          (json['members'] as List?)
              ?.map(
                (e) => WatchlistMember.fromJson(
                  (e as Map).cast<String, dynamic>(),
                ),
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
    this.addedByUsername,
    this.watchedBy = const [],
  });

  final int movieId;
  final String title;
  final String? posterPath;
  final DateTime? addedAt;

  /// Who added it (null in older rows).
  final String? addedByUsername;

  /// Usernames of the list's people (owner + members) who rated the movie.
  final List<String> watchedBy;

  factory WatchlistItem.fromJson(Map<String, dynamic> json) {
    return WatchlistItem(
      movieId: (json['movieId'] as num?)?.toInt() ?? 0,
      title: json['title'] as String? ?? L10n.current.untitledMovie,
      posterPath: json['posterPath'] as String?,
      addedAt: json['addedAt'] == null
          ? null
          : DateTime.tryParse(json['addedAt'] as String),
      addedByUsername: json['addedByUsername'] as String?,
      watchedBy:
          (json['watchedBy'] as List?)?.whereType<String>().toList() ??
          const [],
    );
  }
}

/// A member of a shared list (pending until they accept the invitation).
class WatchlistMember {
  const WatchlistMember({required this.user, this.accepted = false});

  final Author user;
  final bool accepted;

  factory WatchlistMember.fromJson(Map<String, dynamic> json) =>
      WatchlistMember(
        user: Author.fromJson(_map(json['user'])),
        accepted: json['accepted'] as bool? ?? false,
      );
}

/// `/api/v1/watchlist/invitations`: a list I was invited to.
class WatchlistInvitation {
  const WatchlistInvitation({
    required this.listId,
    required this.name,
    required this.owner,
    this.itemCount = 0,
    this.invitedAt,
  });

  final int listId;
  final String name;
  final Author owner;
  final int itemCount;
  final DateTime? invitedAt;

  factory WatchlistInvitation.fromJson(Map<String, dynamic> json) =>
      WatchlistInvitation(
        listId: (json['listId'] as num?)?.toInt() ?? 0,
        name: json['name'] as String? ?? L10n.current.untitledWatchlist,
        owner: Author.fromJson(_map(json['owner'])),
        itemCount: (json['itemCount'] as num?)?.toInt() ?? 0,
        invitedAt: json['invitedAt'] is String
            ? DateTime.tryParse(json['invitedAt'] as String)
            : null,
      );
}
