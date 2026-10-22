import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/year_in_review.dart';
import 'locale_provider.dart';
import 'repository_providers.dart';

/// Whose recap and which year; a null [year] is the server's current year
/// (the year so far).
typedef YearInReviewKey = ({String userId, int? year});

final yearInReviewProvider = FutureProvider.autoDispose
    .family<YearInReview, YearInReviewKey>(
      (ref, key) => ref
          .watch(yearInReviewRepositoryProvider)
          .forUser(key.userId, year: key.year),
    );

/// "Now", replaceable in tests (the feed banner depends on the date).
final clockProvider = Provider<DateTime Function()>((ref) => DateTime.now);

/// How long a story slide stays before advancing on its own; null turns
/// auto-advance off (tests, and a fixed-step mode if ever needed).
final storySlideDurationProvider = Provider<Duration?>(
  (ref) => const Duration(seconds: 6),
);

/// The recap worth offering on [now]: until January 15 the previous year
/// (the new one has barely started), afterwards the current one.
int recapYearFor(DateTime now) =>
    now.month == 1 && now.day <= 15 ? now.year - 1 : now.year;

/// The feed banner runs from December 1 to January 15.
bool isRecapSeason(DateTime now) =>
    now.month == 12 || (now.month == 1 && now.day <= 15);

/// Whether [key]'s user dismissed the feed banner of [key]'s year; stored
/// with the other preferences so it survives restarts.
final recapBannerDismissedProvider = AsyncNotifierProvider.autoDispose
    .family<RecapBannerDismissed, bool, ({String userId, int year})>(
      RecapBannerDismissed.new,
    );

class RecapBannerDismissed extends AsyncNotifier<bool> {
  RecapBannerDismissed(this.key);

  final ({String userId, int year}) key;

  @override
  Future<bool> build() async =>
      await ref
          .read(preferencesStorageProvider)
          .readDismissedRecapYear(key.userId) ==
      key.year;

  Future<void> dismiss() async {
    state = const AsyncData(true);
    await ref
        .read(preferencesStorageProvider)
        .writeDismissedRecapYear(key.userId, key.year);
  }
}
