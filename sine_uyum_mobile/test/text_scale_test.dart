import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';
import 'support/localized_app.dart';
import 'support/screens.dart';

/// The system text size up to 1.3× must not clip any key screen
/// (docs/DESIGN-SYSTEM.md §2 and §9): a RenderFlex overflow is reported as
/// an exception, which fails the test.
void main() {
  setUpAll(initializeAllDateFormatting);

  for (final screen in keyScreens) {
    for (final locale in [tr, en]) {
      testWidgets('${screen.name} at 1.3× text (${locale.languageCode})', (
        tester,
      ) async {
        final app = AppHarness(user: screen.user);
        screen.setUp(app);
        await app.pump(
          tester,
          locale: locale,
          textScale: 1.3,
          initialLocation: screen.location,
        );
        await screen.interact?.call(tester);
        await AppHarness.settle(tester, 4);
        final error = tester.takeException();
        expect(
          error,
          isNull,
          reason: error is FlutterError ? error.toStringDeep() : '$error',
        );
        expect(
          MediaQuery.textScalerOf(tester.element(find.byType(Scaffold).last))
              .scale(10),
          closeTo(13, 0.01),
        );
      });
    }
  }
}
