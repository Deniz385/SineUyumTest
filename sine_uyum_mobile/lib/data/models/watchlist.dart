import '../../l10n/l10n.dart';
import 'social.dart' show Author;

Map<String, dynamic>? _map(Object? value) =>
    value is Map ? value.cast<String, dynamic>() : null;

int _int(Object? value) => (value as num?)?.toInt() ?? 0;

/// Who can see a list: only its people (default) or, when public, anyone
/// who may see the owner's profile details (discovery, profile, likes and
/// saves).
enum WatchlistVisibility {
  private('Private'),
  public('Public');

  const WatchlistVisibility(this.apiValue);

  /// The value the API sends and accepts.
  final String apiValue;

  /// Unknown or missing values (older servers) are private.
  static WatchlistVisibility fromJson(Object? value) => switch (value) {
    final String text when text.toLowerCase() == 'public' => public,
    1 => public,
    _ => private,
  };
}

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
    this.visibility = WatchlistVisibility.private,
    this.likeCount = 0,
    this.saveCount = 0,
  });

  final int id;
  final String name;
  final String? description;
  final int itemCount;
  final WatchlistVisibility visibility;
  final int likeCount;
  final int saveCount;

  bool get isPublic => visibility == WatchlistVisibility.public;

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
      visibility: WatchlistVisibility.fromJson(json['visibility']),
      likeCount: _int(json['likeCount']),
      saveCount: _int(json['saveCount']),
    );
  }
}

/// Response of `/api/v1/watchlist/{listId}` and
/// `/api/v1/watchlist/public/{listId}` (the read-only one has no membership
/// fields; it adds [likedByMe], [savedByMe] and [canEdit]).
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
    this.visibility = WatchlistVisibility.private,
    this.likeCount = 0,
    this.saveCount = 0,
    this.likedByMe = false,
    this.savedByMe = false,
    this.canEdit = false,
  });

  final int id;
  final String name;
  final String? description;
  final String? ownerUsername;
  final List<WatchlistItem> items;
  final bool isOwner;
  final Author? owner;
  final WatchlistVisibility visibility;
  final int likeCount;
  final int saveCount;

  /// Read-only view only.
  final bool likedByMe;
  final bool savedByMe;

  /// Read-only view: I am the owner or an accepted member, so the editable
  /// view can be opened.
  final bool canEdit;

  bool get isPublic => visibility == WatchlistVisibility.public;

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
    visibility: visibility,
    likeCount: likeCount,
    saveCount: saveCount,
    likedByMe: likedByMe,
    savedByMe: savedByMe,
    canEdit: canEdit,
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
      visibility: WatchlistVisibility.fromJson(json['visibility']),
      likeCount: _int(json['likeCount']),
      saveCount: _int(json['saveCount']),
      likedByMe: json['likedByMe'] as bool? ?? false,
      savedByMe: json['savedByMe'] as bool? ?? false,
      canEdit: json['canEdit'] as bool? ?? false,
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

/// A public list in discovery, on a profile or among my saved lists.
class WatchlistCard {
  const WatchlistCard({
    required this.id,
    required this.name,
    required this.owner,
    this.description,
    this.itemCount = 0,
    this.likeCount = 0,
    this.saveCount = 0,
    this.likedByMe = false,
    this.savedByMe = false,
    this.posters = const [],
    this.publishedAt,
    this.ownerCompatibility,
  });

  final int id;
  final String name;
  final String? description;
  final Author owner;
  final int itemCount;
  final int likeCount;
  final int saveCount;
  final bool likedByMe;
  final bool savedByMe;

  /// Up to four posters of the latest movies (null: the movie has none).
  final List<String?> posters;
  final DateTime? publishedAt;

  /// My compatibility with the owner (0-100); null when we have too few
  /// movies in common.
  final double? ownerCompatibility;

  factory WatchlistCard.fromJson(Map<String, dynamic> json) => WatchlistCard(
    id: _int(json['id']),
    name: json['name'] as String? ?? L10n.current.untitledWatchlist,
    description: json['description'] as String?,
    owner: Author.fromJson(_map(json['owner'])),
    itemCount: _int(json['itemCount']),
    likeCount: _int(json['likeCount']),
    saveCount: _int(json['saveCount']),
    likedByMe: json['likedByMe'] as bool? ?? false,
    savedByMe: json['savedByMe'] as bool? ?? false,
    posters:
        (json['posters'] as List?)
            ?.map((p) => p is String && p.isNotEmpty ? p : null)
            .take(4)
            .toList() ??
        const [],
    publishedAt: json['publishedAt'] is String
        ? DateTime.tryParse(json['publishedAt'] as String)
        : null,
    ownerCompatibility: (json['ownerCompatibility'] as num?)?.toDouble(),
  );
}

/// One page of [WatchlistCard]s (`discover`, `user/{id}`, `saved`).
class WatchlistCardPage {
  const WatchlistCardPage({
    this.items = const [],
    this.page = 1,
    this.hasMore = false,
  });

  final List<WatchlistCard> items;
  final int page;
  final bool hasMore;

  factory WatchlistCardPage.fromJson(Map<String, dynamic> json) =>
      WatchlistCardPage(
        items:
            (json['items'] as List?)
                ?.map((e) => WatchlistCard.fromJson(_map(e) ?? const {}))
                .toList() ??
            const [],
        page: (json['page'] as num?)?.toInt() ?? 1,
        hasMore: json['hasMore'] as bool? ?? false,
      );
}

/// My like and save of someone's public list, with the counts.
class ListReaction {
  const ListReaction({
    this.liked = false,
    this.likeCount = 0,
    this.saved = false,
    this.saveCount = 0,
  });

  final bool liked;
  final int likeCount;
  final bool saved;
  final int saveCount;

  factory ListReaction.ofCard(WatchlistCard card) => ListReaction(
    liked: card.likedByMe,
    likeCount: card.likeCount,
    saved: card.savedByMe,
    saveCount: card.saveCount,
  );

  factory ListReaction.ofDetail(WatchlistDetail list) => ListReaction(
    liked: list.likedByMe,
    likeCount: list.likeCount,
    saved: list.savedByMe,
    saveCount: list.saveCount,
  );

  ListReaction copyWith({
    bool? liked,
    int? likeCount,
    bool? saved,
    int? saveCount,
  }) => ListReaction(
    liked: liked ?? this.liked,
    likeCount: likeCount ?? this.likeCount,
    saved: saved ?? this.saved,
    saveCount: saveCount ?? this.saveCount,
  );

  @override
  bool operator ==(Object other) =>
      other is ListReaction &&
      other.liked == liked &&
      other.likeCount == likeCount &&
      other.saved == saved &&
      other.saveCount == saveCount;

  @override
  int get hashCode => Object.hash(liked, likeCount, saved, saveCount);
}
