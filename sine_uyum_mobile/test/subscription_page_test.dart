import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/repositories/auth_repository.dart';
import 'package:sine_uyum_mobile/features/subscription/subscription_page.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/localized_app.dart';

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
    subscribed = true; // fresh information from the server
    state = _state();
  }
}

void main() {
  // main.dart loads these in the app; dates need them in tests too.
  setUpAll(initializeAllDateFormatting);

  Future<FakeSession> pump(
    WidgetTester tester, {
    required SubscriptionInfo info,
    required bool tokenSaysSubscribed,
    Locale locale = tr,
  }) async {
    final session = FakeSession(subscribed: tokenSaysSubscribed);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authRepositoryProvider.overrideWithValue(FakeAuth(info)),
          sessionProvider.overrideWith(() => session),
        ],
        child: localizedApp(locale: locale, home: const SubscriptionPage()),
      ),
    );
    await tester.pumpAndSettle();
    return session;
  }

  testWidgets('when not subscribed: no payment-free start button, '
      'an info note instead', (tester) async {
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

  testWidgets('active on the server: shows the end date and refreshes '
      'the session', (tester) async {
    final session = await pump(
      tester,
      info: SubscriptionInfo(
        isActive: true,
        expiresAt: DateTime.utc(2030, 1, 15),
        provider: 'manual',
      ),
      tokenSaysSubscribed: false, // stale token: an admin just granted it
    );

    expect(find.text('Aboneliğin aktif'), findsOneWidget);
    expect(find.text('Bitiş: 15 Ocak 2030'), findsOneWidget);
    expect(find.text('Etkinliklere git'), findsOneWidget);
    expect(session.refreshes, 1);
  });

  testWidgets('lists only the paid features that actually exist', (
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

  testWidgets('renders in English with an English date', (tester) async {
    await pump(
      tester,
      info: SubscriptionInfo(
        isActive: true,
        expiresAt: DateTime.utc(2030, 1, 15),
        willRenew: true,
      ),
      tokenSaysSubscribed: true,
      locale: en,
    );

    expect(find.text('Your subscription is active'), findsOneWidget);
    expect(find.text('Renews: January 15, 2030'), findsOneWidget);
    expect(find.text('Joining open-air cinema events'), findsOneWidget);
    expect(find.text('Go to events'), findsOneWidget);
  });
}
