import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../data/models/app_user.dart';
import '../../l10n/l10n.dart';
import '../../providers/session_provider.dart';
import '../../core/ui/state_views.dart';

/// The subscription status is read from the server (the single source of
/// truth).
final subscriptionInfoProvider = FutureProvider.autoDispose<SubscriptionInfo>((
  ref,
) async {
  final info = await ref.watch(authRepositoryProvider).subscription();
  // If the session's user disagrees (e.g. an admin just granted it), refresh
  // it so the event screen behaves correctly right away.
  final user = ref.read(sessionProvider).user;
  if (user != null && user.isSubscribed != info.isActive) {
    await ref.read(sessionProvider.notifier).refreshUser();
  }
  return info;
});

/// The payment-free "start subscription" button was removed (G4): a
/// subscription is only enabled by a verified payment or by an admin.
class SubscriptionPage extends ConsumerWidget {
  const SubscriptionPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final info = ref.watch(subscriptionInfoProvider);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.commonSubscription)),
      body: info.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(subscriptionInfoProvider),
        ),
        data: (info) => RefreshIndicator(
          onRefresh: () => ref.refresh(subscriptionInfoProvider.future),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _StatusCard(info: info),
              const SizedBox(height: 20),
              Text(
                l10n.subscriptionBenefitsTitle,
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              // Only paid features that actually exist are listed.
              _Benefit(text: l10n.benefitOpenAirEvents),
              _Benefit(text: l10n.benefitGroupMatching),
              _Benefit(text: l10n.benefitGroupVoting),
              const SizedBox(height: 24),
              if (info.isActive)
                FilledButton(
                  onPressed: () => context.go('/event'),
                  child: Text(l10n.goToEvents),
                )
              else
                _Notice(
                  text: info.availableProviders.isEmpty
                      ? l10n.paymentComingSoon
                      : l10n.paymentPreparing,
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.info});

  final SubscriptionInfo info;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final expires = info.expiresAt;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  info.isActive
                      ? Icons.workspace_premium_rounded
                      : Icons.workspace_premium_rounded,
                  color: Theme.of(context).colorScheme.primary,
                ),
                const SizedBox(width: 8),
                Text(
                  info.isActive
                      ? l10n.subscriptionIsActive
                      : l10n.subscriptionNotSubscribed,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ],
            ),
            if (info.isActive && expires != null) ...[
              const SizedBox(height: 8),
              Text(
                (info.willRenew
                    ? l10n.subscriptionRenews
                    : l10n.subscriptionEnds)(
                  DateFormat.yMMMMd(l10n.localeName).format(expires.toLocal()),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Notice extends StatelessWidget {
  const _Notice({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colors.secondaryContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(Icons.info_outline_rounded, color: colors.onSecondaryContainer),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              text,
              style: TextStyle(color: colors.onSecondaryContainer),
            ),
          ),
        ],
      ),
    );
  }
}

class _Benefit extends StatelessWidget {
  const _Benefit({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          Icon(
            Icons.check_circle_outline_rounded,
            color: Theme.of(context).colorScheme.primary,
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}
