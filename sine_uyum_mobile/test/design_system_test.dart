import 'dart:math' as math;
import 'dart:ui' show SemanticsAction;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/theme/app_theme.dart';
import 'package:sine_uyum_mobile/core/ui/compatibility_badge.dart';
import 'package:sine_uyum_mobile/core/ui/poster_image.dart';
import 'package:sine_uyum_mobile/core/ui/rating_input.dart';
import 'package:sine_uyum_mobile/core/ui/skeleton.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';
import 'package:sine_uyum_mobile/providers/locale_provider.dart';
import 'package:sine_uyum_mobile/providers/theme_provider.dart';

import 'support/fake_network.dart';
import 'support/localized_app.dart';

/// WCAG relative luminance contrast ratio.
double contrast(Color a, Color b) {
  double channel(double c) =>
      c <= 0.03928 ? c / 12.92 : math.pow((c + 0.055) / 1.055, 2.4).toDouble();
  double lum(Color c) =>
      0.2126 * channel(c.r) + 0.7152 * channel(c.g) + 0.0722 * channel(c.b);
  final la = lum(a), lb = lum(b);
  return (math.max(la, lb) + 0.05) / (math.min(la, lb) + 0.05);
}

Widget themed(
  Widget child, {
  AppColors colors = AppColors.dark,
  Locale? locale,
}) {
  L10n.locale = locale ?? en;
  return MaterialApp(
    theme: AppTheme.build(colors),
    locale: locale ?? en,
    supportedLocales: AppLocalizations.supportedLocales,
    localizationsDelegates: AppLocalizations.localizationsDelegates,
    home: Scaffold(body: Center(child: child)),
  );
}

void main() {
  group('tokens', () {
    test('text pairs pass WCAG AA on both themes', () {
      for (final c in [AppColors.dark, AppColors.light]) {
        for (final surface in [c.bg, c.surface]) {
          expect(contrast(c.text, surface), greaterThanOrEqualTo(4.5));
          expect(contrast(c.textSecondary, surface), greaterThanOrEqualTo(4.5));
          expect(contrast(c.textMuted, surface), greaterThanOrEqualTo(4.5));
          expect(contrast(c.primary, surface), greaterThanOrEqualTo(3));
        }
        expect(contrast(c.onPrimary, c.primary), greaterThanOrEqualTo(4.5));
        expect(
          contrast(Colors.white, c.accentStrong),
          greaterThanOrEqualTo(4.5),
        );
      }
    });

    test(
      'the theme carries the tokens, the bundled fonts and pill buttons',
      () {
        final theme = AppTheme.dark;
        expect(theme.brightness, Brightness.dark);
        expect(theme.scaffoldBackgroundColor, AppColors.dark.bg);
        expect(theme.extension<AppColors>(), AppColors.dark);
        expect(theme.textTheme.displayLarge!.fontFamily, AppFonts.display);
        expect(theme.textTheme.bodyMedium!.fontFamily, AppFonts.text);
        expect(theme.textTheme.bodyMedium!.fontSize, 15);
        final shape = theme.filledButtonTheme.style!.shape!.resolve({});
        expect(
          (shape as RoundedRectangleBorder).borderRadius,
          BorderRadius.circular(AppRadii.pill),
        );
        expect(AppTheme.light.scaffoldBackgroundColor, AppColors.light.bg);
      },
    );
  });

  group('theme setting', () {
    test('dark is the default when nothing is saved', () {
      final container = ProviderContainer(
        overrides: [
          preferencesStorageProvider.overrideWithValue(MemoryPreferences()),
        ],
      );
      addTearDown(container.dispose);
      expect(container.read(themeModeProvider), ThemeMode.dark);
    });

    test('a choice is persisted and read back', () async {
      final preferences = MemoryPreferences();
      final container = ProviderContainer(
        overrides: [preferencesStorageProvider.overrideWithValue(preferences)],
      );
      addTearDown(container.dispose);
      await container.read(themeModeProvider.notifier).set(ThemeMode.light);
      expect(preferences.theme, 'light');

      final next = ProviderContainer(
        overrides: [
          preferencesStorageProvider.overrideWithValue(preferences),
          savedThemeModeProvider.overrideWithValue(
            parseThemeMode(await preferences.readThemeMode()),
          ),
        ],
      );
      addTearDown(next.dispose);
      expect(next.read(themeModeProvider), ThemeMode.light);
      expect(parseThemeMode('system'), ThemeMode.system);
      expect(parseThemeMode('sepia'), isNull);
    });
  });

  group('CompatibilityBadge', () {
    Color iconColor(WidgetTester tester) =>
        tester.widget<Icon>(find.byIcon(Icons.favorite_rounded)).color!;

    for (final (score, token) in [
      (32.0, 'accent'),
      (49.0, 'accent'),
      (50.0, 'primary'),
      (74.0, 'primary'),
      (75.0, 'success'),
      (96.0, 'success'),
    ]) {
      testWidgets('$score% uses $token, with the number', (tester) async {
        await tester.pumpWidget(themed(CompatibilityBadge(score: score)));
        final c = AppColors.dark;
        final expected = switch (token) {
          'accent' => c.accent,
          'primary' => c.primary,
          _ => c.success,
        };
        expect(iconColor(tester), expected);
        expect(find.text('${score.round()}%'), findsOneWidget);
      });
    }

    testWidgets('reads "82% match" and is a ring on profiles (tr)', (
      tester,
    ) async {
      final handle = tester.ensureSemantics();
      await tester.pumpWidget(
        themed(
          const CompatibilityBadge(score: 82, ring: true),
          colors: AppColors.light,
          locale: tr,
        ),
      );
      expect(find.text('%82'), findsOneWidget);
      expect(find.bySemanticsLabel('%82 uyumlu'), findsOneWidget);
      expect(AppColors.light.compatibility(82), AppColors.light.success);
      handle.dispose();
    });
  });

  group('RatingInput', () {
    Widget harness(ValueNotifier<int?> value) => themed(
      ValueListenableBuilder<int?>(
        valueListenable: value,
        builder: (_, v, _) => RatingInput(
          value: v,
          starSize: 40,
          onChanged: (next) => value.value = next,
        ),
      ),
    );

    test('a touch maps to half stars', () {
      expect(RatingInput.valueAt(1, 200), 1);
      expect(RatingInput.valueAt(20, 200), 1);
      expect(RatingInput.valueAt(21, 200), 2);
      expect(RatingInput.valueAt(150, 200), 8);
      expect(RatingInput.valueAt(200, 200), 10);
      expect(RatingInput.valueAt(-5, 200), 1);
      expect(RatingInput.valueAt(500, 200), 10);
    });

    testWidgets('tap sets the rating and shows the number', (tester) async {
      final value = ValueNotifier<int?>(null);
      await tester.pumpWidget(harness(value));
      expect(find.text('–'), findsOneWidget);

      final stars = tester.getTopLeft(find.byType(RatingStars));
      // The 4th star's right half: 8 / 10.
      await tester.tapAt(stars + const Offset(40 * 3 + 30, 20));
      await tester.pump();
      expect(value.value, 8);
      expect(find.text('8'), findsOneWidget);
      expect(find.text('/10'), findsOneWidget);
      // 4 full stars.
      expect(find.byIcon(Icons.star_rounded), findsNWidgets(4));

      // The first star's left half: 1 / 10, a half star.
      await tester.tapAt(stars + const Offset(10, 20));
      await tester.pump();
      expect(value.value, 1);
      expect(find.byIcon(Icons.star_half_rounded), findsOneWidget);
    });

    testWidgets('drag across the stars', (tester) async {
      final value = ValueNotifier<int?>(2);
      await tester.pumpWidget(harness(value));
      final stars = tester.getTopLeft(find.byType(RatingStars));
      final gesture = await tester.startGesture(stars + const Offset(5, 20));
      await gesture.moveBy(const Offset(60, 0));
      await gesture.moveBy(const Offset(60, 0));
      await tester.pump();
      expect(value.value, 7); // 125 / 200 → 6.25 → 7
      await gesture.up();
      await tester.pump();
      expect(find.text('7'), findsOneWidget);
    });

    testWidgets('screen readers hear "8 out of 10" and can adjust it', (
      tester,
    ) async {
      final handle = tester.ensureSemantics();
      final value = ValueNotifier<int?>(8);
      await tester.pumpWidget(harness(value));

      final node = tester.getSemantics(find.byType(RatingInput));
      expect(node.label, 'Your rating');
      expect(node.value, '8 out of 10');
      expect(node.increasedValue, '9 out of 10');
      expect(node.decreasedValue, '7 out of 10');

      tester.semantics.performAction(
        find.semantics.byLabel('Your rating'),
        SemanticsAction.increase,
      );
      await tester.pump();
      expect(value.value, 9);
      handle.dispose();
    });

    testWidgets('arrow keys move the rating', (tester) async {
      final value = ValueNotifier<int?>(5);
      await tester.pumpWidget(harness(value));
      Focus.of(tester.element(find.byType(RatingStars))).requestFocus();
      await tester.pump();
      await tester.sendKeyEvent(LogicalKeyboardKey.arrowRight);
      await tester.pump();
      expect(value.value, 6);
      await tester.sendKeyEvent(LogicalKeyboardKey.arrowLeft);
      await tester.pump();
      await tester.sendKeyEvent(LogicalKeyboardKey.arrowLeft);
      await tester.pump();
      expect(value.value, 4);
    });

    testWidgets('Turkish reading (tr)', (tester) async {
      final handle = tester.ensureSemantics();
      await tester.pumpWidget(
        themed(RatingInput(value: 6, onChanged: (_) {}), locale: tr),
      );
      final node = tester.getSemantics(find.byType(RatingInput));
      expect(node.label, 'Puanın');
      expect(node.value, '10 üzerinden 6');
      handle.dispose();
    });
  });

  group('PosterImage', () {
    testWidgets('announces the title; untitled posters are decorative', (
      tester,
    ) async {
      final handle = tester.ensureSemantics();
      await tester.pumpWidget(
        themed(
          const Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              PosterImage(posterPath: null, title: 'Fight Club', width: 60),
              PosterImage(posterPath: null, width: 60),
            ],
          ),
        ),
      );
      expect(find.bySemanticsLabel('Fight Club'), findsOneWidget);
      // 2:3 with the film placeholder.
      expect(
        tester.getSize(find.byType(PosterImage).first),
        const Size(60, 90),
      );
      expect(find.byIcon(Icons.movie_rounded), findsNWidgets(2));
      handle.dispose();
    });
  });

  group('skeletons', () {
    testWidgets('shimmer, but stand still under reduced motion', (
      tester,
    ) async {
      await tester.pumpWidget(themed(const Shimmer(child: SkeletonBox())));
      expect(tester.hasRunningAnimations, isTrue);
      await tester.pumpWidget(const SizedBox());
      await tester.pump();

      await tester.pumpWidget(
        themed(
          Builder(
            builder: (context) => MediaQuery(
              data: MediaQuery.of(context).copyWith(disableAnimations: true),
              child: const Shimmer(child: SkeletonBox(width: 40)),
            ),
          ),
        ),
      );
      await tester.pump();
      expect(tester.hasRunningAnimations, isFalse);
    });
  });
}
