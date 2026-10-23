import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../providers/session_provider.dart';
import '../../providers/year_in_review_provider.dart';

/// Feed card offering the signed-in user's recap from December 1 to
/// January 15 (in January, of the year that just ended). Shown only once the
/// recap is ready; hiding it is remembered for that year.
/// Text on the marquee gradient (AA on all of its stops).
const _ink = Color(0xFF1A1300);

class YearInReviewBanner extends ConsumerWidget {
  const YearInReviewBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final now = ref.watch(clockProvider)();
    // Out of season nothing else is read (no session, storage or request).
    if (!isRecapSeason(now)) return const SizedBox.shrink();
    final userId = ref.watch(sessionProvider.select((s) => s.user?.id));
    if (userId == null) return const SizedBox.shrink();

    final year = recapYearFor(now);
    final dismissedKey = (userId: userId, year: year);
    // Hidden while unknown, so it never flashes before a dismissal is read.
    if (ref.watch(recapBannerDismissedProvider(dismissedKey)).value != false) {
      return const SizedBox.shrink();
    }
    final recap = ref
        .watch(yearInReviewProvider((userId: userId, year: year)))
        .value;
    if (recap == null || !recap.ready) return const SizedBox.shrink();

    final l10n = context.l10n;
    return Container(
      margin: const EdgeInsets.only(bottom: AppSpace.sm),
      padding: const EdgeInsets.fromLTRB(16, 12, 4, 16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadii.lg),
        gradient: AppGradients.marquee,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Padding(
                padding: EdgeInsets.only(top: 10),
                child: Icon(Icons.auto_awesome_rounded, color: _ink),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    l10n.recapBannerTitle(year),
                    style: context.type.h2.copyWith(color: _ink),
                  ),
                ),
              ),
              IconButton(
                tooltip: l10n.recapBannerDismiss,
                icon: const Icon(Icons.close_rounded, color: _ink),
                onPressed: () => ref
                    .read(recapBannerDismissedProvider(dismissedKey).notifier)
                    .dismiss(),
              ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: Text(
              l10n.recapBannerBody,
              style: context.type.body.copyWith(color: _ink),
            ),
          ),
          const SizedBox(height: 12),
          FilledButton.icon(
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.dark.bg,
              foregroundColor: AppColors.dark.text,
            ),
            onPressed: () =>
                context.push('/profile/$userId/year-in-review?year=$year'),
            icon: const Icon(Icons.play_arrow_rounded),
            label: Text(l10n.recapBannerOpen),
          ),
        ],
      ),
    );
  }
}
