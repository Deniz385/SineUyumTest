import 'movie.dart';

/// Kullanıcının etkinlik durumu — `/api/event/my-status`.
enum EventStatus {
  /// Gruba atanmış, film oylaması açık.
  matched,

  /// Etkinliğe katılmış, eşleştirme bekliyor.
  pending,

  /// Katılabileceği bir etkinlik var.
  available,

  /// Katılabileceği etkinlik yok.
  none;

  static EventStatus parse(String? value) {
    switch (value) {
      case 'MATCHED':
        return EventStatus.matched;
      case 'PENDING':
        return EventStatus.pending;
      case 'AVAILABLE':
        return EventStatus.available;
      default:
        return EventStatus.none;
    }
  }
}

class EventState {
  const EventState({
    required this.status,
    this.message,
    this.event,
    this.group,
    this.votes = const [],
    this.suggestedMovies = const [],
  });

  final EventStatus status;
  final String? message;
  final CinemaEvent? event;
  final EventGroup? group;
  final List<EventVote> votes;
  final List<Movie> suggestedMovies;

  factory EventState.fromJson(Map<String, dynamic> json) {
    final eventJson = (json['event'] as Map?)?.cast<String, dynamic>();
    final groupJson = (json['group'] as Map?)?.cast<String, dynamic>();

    return EventState(
      status: EventStatus.parse(json['status'] as String?),
      message: json['message'] as String?,
      event: eventJson == null ? null : CinemaEvent.fromJson(eventJson),
      group: groupJson == null ? null : EventGroup.fromJson(groupJson),
      votes:
          (json['votes'] as List?)
              ?.map(
                (e) => EventVote.fromJson((e as Map).cast<String, dynamic>()),
              )
              .toList() ??
          const [],
      suggestedMovies:
          (json['suggestedMovies'] as List?)
              ?.map((e) => Movie.fromJson((e as Map).cast<String, dynamic>()))
              .toList() ??
          const [],
    );
  }
}

class CinemaEvent {
  const CinemaEvent({
    required this.id,
    required this.locationName,
    this.address,
    this.eventDate,
    this.groupSize = 0,
  });

  final int id;
  final String locationName;
  final String? address;
  final DateTime? eventDate;
  final int groupSize;

  factory CinemaEvent.fromJson(Map<String, dynamic> json) {
    return CinemaEvent(
      id: (json['id'] as num?)?.toInt() ?? 0,
      locationName: json['locationName'] as String? ?? 'Sinema etkinliği',
      address: json['address'] as String?,
      eventDate: json['eventDate'] == null
          ? null
          : DateTime.tryParse(json['eventDate'] as String),
      groupSize: (json['groupSize'] as num?)?.toInt() ?? 0,
    );
  }
}

class EventGroup {
  const EventGroup({required this.id, this.members = const []});

  final int id;
  final List<EventGroupMember> members;

  factory EventGroup.fromJson(Map<String, dynamic> json) {
    return EventGroup(
      id: (json['id'] as num?)?.toInt() ?? 0,
      members:
          (json['members'] as List?)
              ?.map(
                (e) => EventGroupMember.fromJson(
                  (e as Map).cast<String, dynamic>(),
                ),
              )
              .toList() ??
          const [],
    );
  }
}

class EventGroupMember {
  const EventGroupMember({
    required this.id,
    required this.userName,
    this.profileImageUrl,
  });

  final String id;
  final String userName;
  final String? profileImageUrl;

  factory EventGroupMember.fromJson(Map<String, dynamic> json) {
    return EventGroupMember(
      id: json['id'] as String? ?? '',
      userName: json['userName'] as String? ?? '',
      profileImageUrl: json['profileImageUrl'] as String?,
    );
  }
}

class EventVote {
  const EventVote({required this.userId, required this.movieId});

  final String userId;
  final int movieId;

  factory EventVote.fromJson(Map<String, dynamic> json) {
    return EventVote(
      userId: json['userId'] as String? ?? '',
      movieId: (json['movieId'] as num?)?.toInt() ?? 0,
    );
  }
}
