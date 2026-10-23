import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/offline/network_status.dart';
import '../l10n/l10n.dart';

/// Puts a "showing saved data" strip below every screen while offline.
/// Used in `MaterialApp.builder`.
class OfflineBanner extends ConsumerWidget {
  const OfflineBanner({super.key, required this.child});

  final Widget child;

  static String? messageFor(NetworkStatus status, AppLocalizations l10n) {
    if (!status.deviceOnline) return l10n.offlineDevice;
    if (!status.serverReachable) return l10n.offlineServer;
    return null;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final message = messageFor(ref.watch(networkStatusProvider), context.l10n);
    if (message == null) return child;

    final colors = Theme.of(context).colorScheme;
    return Column(
      children: [
        // The strip takes the bottom safe area itself; the screen must not add
        // it again.
        Expanded(
          child: MediaQuery.removePadding(
            context: context,
            removeBottom: true,
            child: child,
          ),
        ),
        Material(
          color: colors.inverseSurface,
          child: SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                children: [
                  Icon(
                    Icons.cloud_off_rounded,
                    size: 18,
                    color: colors.onInverseSurface,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      message,
                      style: TextStyle(color: colors.onInverseSurface),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}
