import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/message.dart';
import 'repository_providers.dart';
import 'session_provider.dart';

/// SignalR'dan gelen sohbet olayları. Bağlantıyı [NotificationNotifier]
/// yönetir ve olayları buraya iter; ekranlar buradan dinler.
class ChatEvents {
  final _messages = StreamController<ChatMessage>.broadcast();
  final _reconnected = StreamController<void>.broadcast();

  Stream<ChatMessage> get messages => _messages.stream;

  /// Bağlantı koptuktan sonra yeniden kuruldu: arada kaçan olaylar olabilir.
  Stream<void> get reconnected => _reconnected.stream;

  void addMessage(ChatMessage message) => _messages.add(message);
  void markReconnected() => _reconnected.add(null);

  void dispose() {
    _messages.close();
    _reconnected.close();
  }
}

final chatEventsProvider = Provider<ChatEvents>((ref) {
  final events = ChatEvents();
  ref.onDispose(events.dispose);
  return events;
});

/// Şu an ekranda açık olan sohbetin karşı kullanıcısı. Ondan gelen mesaj
/// bildirimi gösterilmez, okundu sayılır (mesaj zaten ekranda).
final activeConversationProvider =
    NotifierProvider<ActiveConversation, String?>(ActiveConversation.new);

class ActiveConversation extends Notifier<String?> {
  @override
  String? build() => null;

  void open(String otherUserId) => state = otherUserId;

  /// Yalnızca hâlâ aynı sohbet açıksa kapatır (hızlı geçişlerde yeni açılanı
  /// silmemek için).
  void close(String otherUserId) {
    if (state == otherUserId) state = null;
  }
}

/// Bir sohbetin mesajları: önce sunucudan yüklenir, sonra canlı mesajlar
/// eklenir. Ekran kapanınca (autoDispose) dinleme biter.
final chatThreadProvider = AsyncNotifierProvider.autoDispose
    .family<ChatThreadNotifier, List<ChatMessage>, String>(
      ChatThreadNotifier.new,
    );

class ChatThreadNotifier extends AsyncNotifier<List<ChatMessage>> {
  ChatThreadNotifier(this.otherUserId);

  final String otherUserId;

  // İlk yükleme sürerken gelen canlı mesajlar; yükleme bitince eklenir.
  final _pending = <ChatMessage>[];

  @override
  Future<List<ChatMessage>> build() async {
    final events = ref.watch(chatEventsProvider);
    final subscriptions = [
      events.messages.listen(_onLive),
      events.reconnected.listen((_) => _refresh()),
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

  // Tekilleştirip sıralar. Mesaj id'leri artan olduğu için id sırası gönderim
  // sırasıdır (yeniden bağlanınca gelen sayfa ile yerel liste birleşirken de).
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

  /// Mesajı ekler. Aynı mesaj hem gönderme yanıtından hem canlı olaydan
  /// (gönderenin kendisine de itilir) gelebilir: id ile tekilleştirilir.
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
    // Ekran açık: karşı taraftan gelen mesaj okundu sayılır.
    if (message.senderId == otherUserId) markAsRead();
  }

  Future<void> markAsRead() async {
    try {
      await ref.read(messageRepositoryProvider).markThreadAsRead(otherUserId);
      ref.invalidate(conversationsProvider);
    } catch (_) {
      // Okundu işaretlemesi başarısız olsa da sohbet görüntülenebilir.
    }
  }

  // Yeniden bağlanınca kopuklukta kaçanlar için sunucudan tazelenir. Hata
  // olursa mevcut liste korunur.
  Future<void> _refresh() async {
    final fresh = await AsyncValue.guard(_fetch);
    if (fresh.hasValue) {
      state = AsyncData(_merge(fresh.value!, state.value ?? const []));
    }
  }
}
