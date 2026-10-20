import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/offline/network_status.dart';
import 'package:sine_uyum_mobile/widgets/offline_banner.dart';

import 'support/fake_network.dart';

void main() {
  late FakeConnectivity connectivity;

  Future<ProviderContainer> pump(WidgetTester tester) async {
    connectivity = FakeConnectivity();
    final container = ProviderContainer(
      overrides: [connectivitySourceProvider.overrideWithValue(connectivity)],
    );
    addTearDown(container.dispose);
    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp(
          builder: (context, child) => OfflineBanner(child: child!),
          home: const Scaffold(body: Text('içerik')),
        ),
      ),
    );
    await tester.pump();
    return container;
  }

  testWidgets('çevrimiçiyken şerit yok', (tester) async {
    await pump(tester);
    expect(find.byIcon(Icons.cloud_off), findsNothing);
    expect(find.text('içerik'), findsOneWidget);
  });

  testWidgets('cihaz çevrimdışı olunca şerit görünür, dönünce kaybolur', (
    tester,
  ) async {
    await pump(tester);

    connectivity.set(false);
    await tester.pump(); // akış olayı
    await tester.pump(); // yeniden çizim
    expect(
      find.text('Çevrimdışısın — kayıtlı veriler gösteriliyor'),
      findsOneWidget,
    );
    expect(find.text('içerik'), findsOneWidget);

    connectivity.set(true);
    await tester.pump();
    await tester.pump();
    expect(find.byIcon(Icons.cloud_off), findsNothing);
  });

  testWidgets('ağ var ama sunucu yanıt vermiyorsa ayrı mesaj', (tester) async {
    final container = await pump(tester);

    container.read(networkStatusProvider.notifier).reportServer(false);
    await tester.pump();

    expect(
      find.text('Sunucuya ulaşılamıyor — kayıtlı veriler gösteriliyor'),
      findsOneWidget,
    );
  });
}
