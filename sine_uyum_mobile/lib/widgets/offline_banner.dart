import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/offline/network_status.dart';

/// Tüm ekranların altına, bağlantı yokken "kayıtlı veriler gösteriliyor"
/// şeridi koyar. `MaterialApp.builder` içinde kullanılır.
class OfflineBanner extends ConsumerWidget {
  const OfflineBanner({super.key, required this.child});

  final Widget child;

  static String? messageFor(NetworkStatus status) {
    if (!status.deviceOnline) {
      return 'Çevrimdışısın — kayıtlı veriler gösteriliyor';
    }
    if (!status.serverReachable) {
      return 'Sunucuya ulaşılamıyor — kayıtlı veriler gösteriliyor';
    }
    return null;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final message = messageFor(ref.watch(networkStatusProvider));
    if (message == null) return child;

    final colors = Theme.of(context).colorScheme;
    return Column(
      children: [
        // Şerit alt güvenli alanı kendisi kullanır; ekran onu tekrar eklemesin.
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
                    Icons.cloud_off,
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
