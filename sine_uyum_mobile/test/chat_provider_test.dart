import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/message.dart';
import 'package:sine_uyum_mobile/data/models/notification.dart';
import 'package:sine_uyum_mobile/data/repositories/message_repository.dart';
import 'package:sine_uyum_mobile/data/repositories/notification_repository.dart';
import 'package:sine_uyum_mobile/providers/chat_provider.dart';
import 'package:sine_uyum_mobile/providers/notification_provider.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/fake_network.dart';

ChatMessage msg(int id, String from, String to, [String? content]) =>
    ChatMessage(
      id: id,
      senderId: from,
      senderUsername: from,
      recipientId: to,
      content: content ?? 'm$id',
    );

class FakeMessages implements MessageRepository {
  List<ChatMessage> server = [];
  Completer<void>? gate; // ilk yüklemeyi bekletmek için
  bool failFetch = false;
  int readMarks = 0;

  @override
  Future<List<ChatMessage>> thread(
    String otherUserId, {
    int page = 1,
    int pageSize = 100,
  }) async {
    if (gate != null) await gate!.future;
    if (failFetch) throw Exception('ağ yok');
    return List.of(server);
  }

  @override
  Future<void> markThreadAsRead(String otherUserId) async => readMarks++;

  @override
  Future<List<Conversation>> conversations() async => const [];

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeNotifications implements NotificationRepository {
  final marked = <int>[];

  @override
  Future<void> markAsRead(int notificationId) async =>
      marked.add(notificationId);

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class SignedInAs extends SessionNotifier {
  SignedInAs(this.id, {this.authenticated = true});
  final String id;
  final bool authenticated;

  @override
  SessionState build() => authenticated
      ? SessionState(
          status: SessionStatus.authenticated,
          user: AppUser(id: id, userName: id),
        )
      // Bildirim sağlayıcısı oturum yokken SignalR'a bağlanmaya çalışmaz.
      : const SessionState.signedOut();
}

void main() {
  late FakeMessages messages;
  late FakeNotifications notifications;
  late ProviderContainer container;
  const me = 'me', ali = 'ali', zeynep = 'zeynep';

  ProviderContainer make({bool authenticated = true}) {
    final c = ProviderContainer(
      overrides: [
        messageRepositoryProvider.overrideWithValue(messages),
        notificationRepositoryProvider.overrideWithValue(notifications),
        sessionProvider.overrideWith(
          () => SignedInAs(me, authenticated: authenticated),
        ),
        connectivitySourceProvider.overrideWithValue(FakeConnectivity()),
      ],
    );
    addTearDown(c.dispose);
    return c;
  }

  setUp(() {
    messages = FakeMessages();
    notifications = FakeNotifications();
    container = make();
  });

  Future<List<ChatMessage>> load() {
    container.listen(chatThreadProvider(ali), (_, _) {}); // ekran açık
    return container.read(chatThreadProvider(ali).future);
  }

  List<int> ids() =>
      container.read(chatThreadProvider(ali)).value!.map((m) => m.id).toList();

  ChatEvents events() => container.read(chatEventsProvider);

  Future<void> tick() => Future<void>.delayed(Duration.zero);

  test('karşı taraftan gelen canlı mesaj eklenir ve okundu sayılır', () async {
    messages.server = [msg(1, ali, me)];
    await load();

    events().addMessage(msg(2, ali, me, 'canlı'));
    await tick();

    expect(ids(), [1, 2]);
    expect(messages.readMarks, 1);
  });

  test('karşı taraf okuyunca o ana kadarki mesajlarım görüldü olur', () async {
    messages.server = [msg(1, me, ali), msg(2, ali, me), msg(3, me, ali)];
    await load();
    events().addMessage(msg(4, me, ali)); // okunmadan sonra gönderilen
    await tick();

    final at = DateTime.utc(2026, 10, 20, 12, 30);
    events().addRead(
      MessagesRead(
        readerId: ali,
        otherUserId: me,
        lastReadMessageId: 3,
        readAt: at,
      ),
    );
    await tick();

    final thread = container.read(chatThreadProvider(ali)).value!;
    expect(thread.map((m) => m.dateRead).toList(), [at, null, at, null]);
  });

  test('başka sohbetin ya da benim okumam görüldü yazmaz', () async {
    messages.server = [msg(1, me, ali)];
    await load();

    final at = DateTime.utc(2026, 10, 20);
    // zeynep başka bir sohbeti okudu; ben ali'nin mesajlarını okudum.
    events().addRead(
      MessagesRead(
        readerId: zeynep,
        otherUserId: me,
        lastReadMessageId: 9,
        readAt: at,
      ),
    );
    events().addRead(
      MessagesRead(
        readerId: me,
        otherUserId: ali,
        lastReadMessageId: 9,
        readAt: at,
      ),
    );
    await tick();

    expect(
      container.read(chatThreadProvider(ali)).value!.single.dateRead,
      null,
    );
  });

  test('olay ayrıştırma: eksik alanlı olay yok sayılır', () {
    expect(MessagesRead.tryParse({'readerId': 'a'}), isNull);
    final parsed = MessagesRead.tryParse({
      'readerId': 'a',
      'otherUserId': 'b',
      'lastReadMessageId': 7,
      'readAt': '2026-10-20T12:30:00Z',
    })!;
    expect(parsed.lastReadMessageId, 7);
    expect(parsed.readAt, DateTime.utc(2026, 10, 20, 12, 30));
    expect(
      ChatMessage.fromJson({'id': 1, 'dateRead': '2026-10-20T12:31:00Z'})
          .dateRead,
      DateTime.utc(2026, 10, 20, 12, 31),
    );
  });

  test('başka sohbetin mesajı bu sohbete eklenmez', () async {
    await load();

    events().addMessage(msg(5, zeynep, me));
    events().addMessage(msg(6, me, zeynep));
    await tick();

    expect(ids(), isEmpty);
    expect(messages.readMarks, 0);
  });

  test('gönderme yanıtı ve canlı yankı tek mesaj olur', () async {
    await load();
    final sent = msg(3, me, ali, 'selam');

    container.read(chatThreadProvider(ali).notifier).add(sent);
    events().addMessage(sent); // sunucu gönderene de iter
    await tick();

    expect(ids(), [3]);
    // Kendi mesajımız için okundu işareti gönderilmez.
    expect(messages.readMarks, 0);
  });

  test('ilk yükleme sürerken gelen mesaj kaybolmaz', () async {
    messages.server = [msg(1, ali, me)];
    messages.gate = Completer<void>();
    container.listen(chatThreadProvider(ali), (_, _) {});
    await tick();

    events().addMessage(msg(2, ali, me));
    await tick();
    messages.gate!.complete();
    await container.read(chatThreadProvider(ali).future);

    expect(ids(), [1, 2]);
  });

  test('yeniden bağlanınca kaçan mesajlar sıralı ve tekil gelir', () async {
    messages.server = [msg(1, ali, me)];
    await load();
    events().addMessage(msg(4, me, ali)); // yerelde var
    await tick();

    // Kopukken sunucuya 2 ve 3 geldi; 4 de sunucuda.
    messages.server = [
      msg(1, ali, me),
      msg(2, ali, me),
      msg(3, ali, me),
      msg(4, me, ali),
    ];
    events().markReconnected();
    await tick();
    await tick();

    expect(ids(), [1, 2, 3, 4]);
  });

  test('yeniden bağlanmada tazeleme başarısızsa liste korunur', () async {
    messages.server = [msg(1, ali, me)];
    await load();

    messages.failFetch = true;
    events().markReconnected();
    await tick();
    await tick();

    expect(ids(), [1]);
  });

  test('aktif sohbet yalnızca aynı sohbet kapanırsa sıfırlanır', () {
    final active = container.read(activeConversationProvider.notifier);
    active.open(ali);
    active.open(zeynep); // hızlı geçiş
    active.close(ali); // eski ekranın dispose'u geç geldi
    expect(container.read(activeConversationProvider), zeynep);
    active.close(zeynep);
    expect(container.read(activeConversationProvider), isNull);
  });

  group('açık sohbetin bildirimi', () {
    setUp(() => container = make(authenticated: false));

    AppNotification note(int id, String url) =>
        AppNotification(id: id, message: 'n$id', relatedUrl: url);

    test('açık sohbetten gelen bildirim okundu olarak eklenir', () async {
      container.read(activeConversationProvider.notifier).open(ali);
      final notifier = container.read(notificationsProvider.notifier);

      notifier.onNotification(note(7, '/messages/$ali'));
      await tick();

      final state = container.read(notificationsProvider);
      expect(state.items.single.isRead, isTrue);
      expect(state.unreadCount, 0);
      expect(notifications.marked, [7]);
    });

    test('başka sohbetin bildirimi okunmamış kalır', () async {
      container.read(activeConversationProvider.notifier).open(ali);
      final notifier = container.read(notificationsProvider.notifier);

      notifier.onNotification(note(8, '/messages/$zeynep'));
      notifier.onNotification(note(9, '/profile/$zeynep'));
      await tick();

      expect(container.read(notificationsProvider).unreadCount, 2);
      expect(notifications.marked, isEmpty);
    });
  });
}
