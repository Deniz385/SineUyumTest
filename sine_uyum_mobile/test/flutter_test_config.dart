import 'dart:async';

import 'package:sine_uyum_mobile/core/ui/poster_image.dart';

/// Runs before every test file: posters and avatars render their
/// placeholders instead of downloading (no network, no disk cache).
Future<void> testExecutable(FutureOr<void> Function() testMain) async {
  NetworkArtwork.enabled = false;
  await testMain();
}
