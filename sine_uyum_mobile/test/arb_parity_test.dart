import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Every translation must have exactly the template's keys, with the same
/// placeholders, so adding a language can't silently ship missing strings.
void main() {
  const template = 'lib/l10n/app_en.arb';

  Map<String, dynamic> read(String path) =>
      jsonDecode(File(path).readAsStringSync()) as Map<String, dynamic>;

  // Message keys only (no `@@locale` / `@key` metadata).
  Map<String, String> messages(Map<String, dynamic> arb) => {
    for (final entry in arb.entries)
      if (!entry.key.startsWith('@')) entry.key: entry.value as String,
  };

  // Argument names used in an ICU message: `{name}` and `{count, plural,
  // ...}`, including those nested in plural branches.
  Set<String> placeholdersIn(String message) =>
      RegExp(r'\{\s*(\w+)\s*[,}]')
          .allMatches(message)
          .map((m) => m.group(1)!)
          .toSet();

  final translations =
      Directory('lib/l10n')
          .listSync()
          .whereType<File>()
          .map((f) => f.path.replaceAll(r'\', '/'))
          .where((p) => p.endsWith('.arb') && p != template)
          .toList()
        ..sort();

  final english = read(template);
  final englishMessages = messages(english);

  test('there is at least one translation besides the template', () {
    expect(translations, contains('lib/l10n/app_tr.arb'));
  });

  test('template placeholders are declared in its metadata', () {
    for (final MapEntry(:key, :value) in englishMessages.entries) {
      final used = placeholdersIn(value);
      final meta = english['@$key'] as Map<String, dynamic>?;
      final declared =
          (meta?['placeholders'] as Map<String, dynamic>?)?.keys.toSet() ??
          <String>{};
      expect(declared, used, reason: 'placeholders of "$key"');
    }
  });

  for (final path in translations) {
    group(path, () {
      final arb = read(path);
      final translated = messages(arb);

      test('@@locale matches the file name', () {
        final code = RegExp(r'app_(\w+)\.arb$').firstMatch(path)!.group(1);
        expect(arb['@@locale'], code);
      });

      test('has every template key and no extra keys', () {
        final missing = englishMessages.keys.toSet().difference(
          translated.keys.toSet(),
        );
        final extra = translated.keys.toSet().difference(
          englishMessages.keys.toSet(),
        );
        expect(missing, isEmpty, reason: 'missing in $path');
        expect(extra, isEmpty, reason: 'not in the template');
      });

      test('uses the same placeholders as the template', () {
        for (final MapEntry(:key, :value) in translated.entries) {
          final expected = englishMessages[key];
          if (expected == null) continue;
          expect(
            placeholdersIn(value),
            placeholdersIn(expected),
            reason: 'placeholders of "$key"',
          );
        }
      });

      test('no message is empty', () {
        for (final MapEntry(:key, :value) in translated.entries) {
          expect(value.trim(), isNotEmpty, reason: key);
        }
      });
    });
  }
}
