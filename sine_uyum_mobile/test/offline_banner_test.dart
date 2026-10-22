import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/widgets/offline_banner.dart';

import 'support/fake_network.dart';
import 'support/localized_app.dart';

void main() {
  late FakeConnectivity connectivity;

  Future<ProviderContainer> pump(
    WidgetTester tester, {
    Locale locale = tr,
  }) async {
    connectivity = FakeConnectivity();
    final container = ProviderContainer(
      overrides: [connectivitySourceProvider.overrideWithValue(connectivity)],
    );
    addTearDown(container.dispose);
    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: localizedApp(
          locale: locale,
          builder: (context, child) => OfflineBanner(child: child!),
          home: const Scaffold(body: Text('content')),
        ),
      ),
    );
    await tester.pump();
    return container;
  }

  testWidgets('no strip while online', (tester) async {
    await pump(tester);
    expect(find.byIcon(Icons.cloud_off), findsNothing);
    expect(find.text('content'), findsOneWidget);
  });

  testWidgets('strip appears when the device goes offline and disappears '
      'when it is back', (tester) async {
    await pump(tester);

    connectivity.set(false);
    await tester.pump(); // stream event
    await tester.pump(); // rebuild
    expect(
      find.text('Çevrimdışısın — kayıtlı veriler gösteriliyor'),
      findsOneWidget,
    );
    expect(find.text('content'), findsOneWidget);

    connectivity.set(true);
    await tester.pump();
    await tester.pump();
    expect(find.byIcon(Icons.cloud_off), findsNothing);
  });

  testWidgets('separate message when the network is up but the server '
      'does not answer', (tester) async {
    final container = await pump(tester);

    container.read(networkStatusProvider.notifier).reportServer(false);
    await tester.pump();

    expect(
      find.text('Sunucuya ulaşılamıyor — kayıtlı veriler gösteriliyor'),
      findsOneWidget,
    );
  });

  testWidgets('renders in English', (tester) async {
    final container = await pump(tester, locale: en);

    connectivity.set(false);
    await tester.pump();
    await tester.pump();
    expect(find.text("You're offline — showing saved data"), findsOneWidget);

    connectivity.set(true);
    await tester.pump();
    container.read(networkStatusProvider.notifier).reportServer(false);
    await tester.pump();
    expect(
      find.text("Can't reach the server — showing saved data"),
      findsOneWidget,
    );
  });
}
