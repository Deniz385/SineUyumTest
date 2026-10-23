import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';
import 'support/localized_app.dart';
import 'support/screens.dart';

/// Renders the key screens with the bundled fonts in both themes and both
/// languages, failing on any layout error (overflow included). With
/// `SCREENSHOT_DIR` set, each frame is also written there as a PNG to look
/// at — golden-free: nothing is compared.
///
///   SCREENSHOT_DIR=/tmp/shots flutter test test/screenshots_test.dart
void main() {
  setUpAll(() async {
    await initializeAllDateFormatting();
    await loadAppFonts();
  });

  final dir = Platform.environment['SCREENSHOT_DIR'];

  for (final screen in keyScreens) {
    for (final (theme, locale) in [
      (ThemeMode.dark, en),
      (ThemeMode.light, tr),
      (ThemeMode.dark, tr),
    ]) {
      final name = '${screen.name}-${theme.name}-${locale.languageCode}';
      testWidgets('renders $name', (tester) async {
        usePhone(tester);
        final app = AppHarness(user: screen.user);
        screen.setUp(app);
        await app.pump(
          tester,
          locale: locale,
          themeMode: theme,
          initialLocation: screen.location,
        );
        await screen.interact?.call(tester);
        await AppHarness.settle(tester, 4);
        expect(tester.takeException(), isNull);
        if (dir != null) await capture(tester, app.screenKey, '$dir/$name.png');
      });
    }
  }
}

void usePhone(WidgetTester tester) {
  tester.view.physicalSize = const Size(1170, 2532);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
}

/// The bundled Inter and Bricolage Grotesque plus Material icons (tests use
/// a placeholder font otherwise).
Future<void> loadAppFonts() async {
  Future<void> family(String name, List<String> assets) async {
    final loader = FontLoader(name);
    for (final asset in assets) {
      loader.addFont(rootBundle.load(asset));
    }
    await loader.load();
  }

  await family('Inter', [
    for (final w in ['Regular', 'Medium', 'SemiBold', 'Bold'])
      'assets/fonts/inter/Inter-$w.ttf',
  ]);
  await family('BricolageGrotesque', [
    for (final w in ['SemiBold', 'Bold', 'ExtraBold'])
      'assets/fonts/bricolage_grotesque/BricolageGrotesque-$w.ttf',
  ]);
  final icons = FontLoader('MaterialIcons');
  final sdk = Platform.environment['FLUTTER_ROOT'];
  final file = sdk == null
      ? null
      : File(
          '$sdk/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
        );
  if (file != null && file.existsSync()) {
    icons.addFont(Future.value(ByteData.sublistView(file.readAsBytesSync())));
  } else {
    icons.addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'));
  }
  await icons.load();
}

Future<void> capture(WidgetTester tester, GlobalKey key, String path) async {
  final boundary =
      key.currentContext!.findRenderObject()! as RenderRepaintBoundary;
  await tester.runAsync(() async {
    final image = await boundary.toImage(pixelRatio: 1.5);
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    File(path)
      ..createSync(recursive: true)
      ..writeAsBytesSync(bytes!.buffer.asUint8List());
  });
}
