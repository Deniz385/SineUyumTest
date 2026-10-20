import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/repositories/auth_repository.dart';
import 'package:sine_uyum_mobile/features/subscription/subscription_page.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

class FakeAuth implements AuthRepository {
  FakeAuth(this.info);
  SubscriptionInfo info;

  @override
  Future<SubscriptionInfo> subscription() async => info;

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeSession extends SessionNotifier {
  FakeSession({required this.subscribed});
  bool subscribed;
  int refreshes = 0;

  @override
  SessionState build() => _state();

  SessionState _state() => SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(id: 'u1', userName: 'ayse', isSubscribed: subscribed),
  );

  @override
  Future<void> refreshUser() async {
    refreshes++;
    subscribed = true; // sunucudan güncel bilgi geldi
    state = _state();
  }
}

void main() {
  // Uygulamada main.dart yükler; testte de Türkçe tarih biçimi gerekli.
  setUpAll(() => initializeDateFormatting('tr'));

  Future<FakeSession> pump(
    WidgetTester tester, {
    required SubscriptionInfo info,
    required bool tokenSaysSubscribed,
  }) async {
    final session = FakeSession(subscribed: tokenSaysSubscribed);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authRepositoryProvider.overrideWithValue(FakeAuth(info)),
          sessionProvider.overrideWith(() => session),
        ],
        child: const MaterialApp(home: SubscriptionPage()),
      ),
    );
    await tester.pumpAndSettle();
    return session;
  }

  testWidgets('abone değilken ödemesiz başlatma düğmesi yok, bilgi notu var', (
    tester,
  ) async {
    await pump(
      tester,
      info: const SubscriptionInfo(isActive: false),
      tokenSaysSubscribed: false,
    );

    expect(find.text('Abone değilsin'), findsOneWidget);
    expect(find.textContaining('Online ödeme çok yakında'), findsOneWidget);
    expect(find.byType(FilledButton), findsNothing);
    expect(find.textContaining('test'), findsNothing);
  });

  testWidgets('sunucuda aktifse bitiş tarihi gösterilir ve oturum tazelenir', (
    tester,
  ) async {
    final session = await pump(
      tester,
      info: SubscriptionInfo(
        isActive: true,
        expiresAt: DateTime.utc(2030, 1, 15),
        provider: 'manual',
      ),
      tokenSaysSubscribed: false, // token eski: yönetici az önce tanımladı
    );

    expect(find.text('Aboneliğin aktif'), findsOneWidget);
    expect(find.textContaining('Bitiş:'), findsOneWidget);
    expect(find.text('Etkinliklere git'), findsOneWidget);
    expect(session.refreshes, 1);
  });

  testWidgets('yalnızca gerçekten var olan özellikler listelenir', (
    tester,
  ) async {
    await pump(
      tester,
      info: const SubscriptionInfo(isActive: false),
      tokenSaysSubscribed: false,
    );

    expect(find.textContaining('Sınırsız izleme listesi'), findsNothing);
    expect(find.textContaining('öncelik'), findsNothing);
    expect(find.textContaining('etkinliklerine katılım'), findsOneWidget);
  });
}
