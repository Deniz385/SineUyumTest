import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/message.dart';
import 'repository_providers.dart';
import 'session_provider.dart';

/// Chat events from SignalR. `NotificationNotifier` owns the connection and
/// pushes events here; screens listen to them.
class ChatEvents {
  final _messages = StreamController<ChatMessage>.broadcast();
  final _reconnected = StreamController<void>.broadcast();
  final _reads = StreamController<MessagesRead>.broadcast();

  Stream<ChatMessage> get messages => _messages.stream;

  /// The other side read the messages ("Seen").
  Stream<MessagesRead> get reads => _reads.stream;

  /// The connection was re-established after a drop: events may have been
  /// missed in between.
  Stream<void> get reconnected => _reconnected.stream;

  void addMessage(ChatMessage message) => _messages.add(message);
  void markReconnected() => _reconnected.add(null);
  void addRead(MessagesRead read) => _reads.add(read);

  void dispose() {
    _messages.close();
    _reads.close();
    _reconnected.close();
  }
}

final chatEventsProvider = Provider<ChatEvents>((ref) {
  final events = ChatEvents();
  ref.onDispose(events.dispose);
  return events;
});

/// The other user of the conversation currently on screen. Message
/// notifications from them aren't shown and count as read (the message is
/// already visible).
final activeConversationProvider =
    NotifierProvider<ActiveConversation, String?>(ActiveConversation.new);

class ActiveConversation extends Notifier<String?> {
  @override
  String? build() => null;

  void open(String otherUserId) => state = otherUserId;

  /// Closes only if the same conversation is still open (so a quick switch
  /// doesn't clear the newly opened one).
  void close(String otherUserId) {
    if (state == otherUserId) state = null;
  }
}

/// A conversation's messages: loaded from the server first, then live
/// messages are appended. Listening stops when the screen closes
/// (autoDispose).
final chatThreadProvider = AsyncNotifierProvider.autoDispose
    .family<ChatThreadNotifier, List<ChatMessage>, String>(
      ChatThreadNotifier.new,
    );

class ChatThreadNotifier extends AsyncNotifier<List<ChatMessage>> {
  ChatThreadNotifier(this.otherUserId);

  final String otherUserId;

  // Live messages that arrive during the first load; added once it finishes.
  final _pending = <ChatMessage>[];

  @override
  Future<List<ChatMessage>> build() async {
    final events = ref.watch(chatEventsProvider);
    final subscriptions = [
      events.messages.listen(_onLive),
      events.reconnected.listen((_) => _refresh()),
      events.reads.listen(_onRead),
    ];
    ref.onDispose(() {
      for (final subscription in subscriptions) {
        subscription.cancel();
      }
    });

    final fetched = await _fetch();
    final merged = _merge(fetched, _pending);
    _pending.clear();
    return merged;
  }

  Future<List<ChatMessage>> _fetch() =>
      ref.read(messageRepositoryProvider).thread(otherUserId);

  // De-duplicates and sorts. Message ids increase, so id order is send order
  // (also when merging the page fetched after a reconnect with the local
  // list).
  static List<ChatMessage> _merge(
    List<ChatMessage> base,
    List<ChatMessage> extra,
  ) {
    final ids = base.map((m) => m.id).toSet();
    return [...base, ...extra.where((m) => ids.add(m.id))]
      ..sort((a, b) => a.id.compareTo(b.id));
  }

  bool _belongsHere(ChatMessage m) {
    final me = ref.read(sessionProvider).user?.id;
    return (m.senderId == otherUserId && m.recipientId == me) ||
        (m.senderId == me && m.recipientId == otherUserId);
  }

  /// Adds a message. The same message can arrive both from the send response
  /// and as a live event (it is pushed to the sender too), so it is
  /// de-duplicated by id.
  void add(ChatMessage message) {
    final current = state.value;
    if (current == null) {
      _pending.add(message);
      return;
    }
    if (current.any((m) => m.id == message.id)) return;
    state = AsyncData([...current, message]);
  }

  void _onLive(ChatMessage message) {
    if (!_belongsHere(message)) return;
    add(message);
    // The screen is open: a message from the other side counts as read.
    if (message.senderId == otherUserId) markAsRead();
  }

  // The other side read: my messages up to that point become "Seen".
  void _onRead(MessagesRead read) {
    final me = ref.read(sessionProvider).user?.id;
    final current = state.value;
    if (current == null ||
        read.readerId != otherUserId ||
        read.otherUserId != me) {
      return;
    }
    state = AsyncData([
      for (final m in current)
        m.senderId == me && m.dateRead == null && m.id <= read.lastReadMessageId
            ? m.markedRead(read.readAt)
            : m,
    ]);
  }

  Future<void> markAsRead() async {
    try {
      await ref.read(messageRepositoryProvider).markThreadAsRead(otherUserId);
      ref.invalidate(conversationsProvider);
    } catch (_) {
      // The conversation can be shown even if marking as read fails.
    }
  }

  // After a reconnect, refreshes from the server for anything missed while
  // disconnected. On error the current list is kept.
  Future<void> _refresh() async {
    final fresh = await AsyncValue.guard(_fetch);
    if (fresh.hasValue) {
      state = AsyncData(_merge(fresh.value!, state.value ?? const []));
    }
  }
}
