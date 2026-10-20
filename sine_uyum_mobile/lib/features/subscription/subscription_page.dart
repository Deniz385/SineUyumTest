import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../data/models/app_user.dart';
import '../../providers/session_provider.dart';
import '../../widgets/state_views.dart';

/// Abonelik durumu sunucudan okunur (tek kaynak).
final subscriptionInfoProvider = FutureProvider.autoDispose<SubscriptionInfo>((
  ref,
) async {
  final info = await ref.watch(authRepositoryProvider).subscription();
  // Oturumdaki kullanıcı bilgisi farklıysa (ör. yönetici az önce tanımladı)
  // tazelenir ki etkinlik ekranı hemen doğru davransın.
  final user = ref.read(sessionProvider).user;
  if (user != null && user.isSubscribed != info.isActive) {
    await ref.read(sessionProvider.notifier).refreshUser();
  }
  return info;
});

/// Ödemesiz "aboneliği başlat" düğmesi kaldırıldı (G4): abonelik yalnızca
/// doğrulanmış ödeme ya da yönetici tanımlamasıyla açılır.
class SubscriptionPage extends ConsumerWidget {
  const SubscriptionPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final info = ref.watch(subscriptionInfoProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Abonelik')),
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
                'Abonelikle neler geliyor?',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              // Yalnızca gerçekten var olan ücretli özellikler listelenir.
              const _Benefit(text: 'Açık hava sineması etkinliklerine katılım'),
              const _Benefit(
                text: 'Film zevkine göre otomatik grup eşleştirme',
              ),
              const _Benefit(text: 'Grubunla birlikte film oylaması'),
              const SizedBox(height: 24),
              if (info.isActive)
                FilledButton(
                  onPressed: () => context.go('/event'),
                  child: const Text('Etkinliklere git'),
                )
              else
                _Notice(
                  text: info.availableProviders.isEmpty
                      ? 'Online ödeme çok yakında. Şu an abonelik satın '
                            'alınamıyor.'
                      : 'Ödeme seçenekleri hazırlanıyor.',
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
                      ? Icons.workspace_premium
                      : Icons.workspace_premium_outlined,
                  color: Theme.of(context).colorScheme.primary,
                ),
                const SizedBox(width: 8),
                Text(
                  info.isActive ? 'Aboneliğin aktif' : 'Abone değilsin',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ],
            ),
            if (info.isActive && expires != null) ...[
              const SizedBox(height: 8),
              Text(
                '${info.willRenew ? 'Yenilenme' : 'Bitiş'}: '
                '${DateFormat('d MMMM y', 'tr').format(expires.toLocal())}',
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
          Icon(Icons.info_outline, color: colors.onSecondaryContainer),
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
            Icons.check_circle_outline,
            color: Theme.of(context).colorScheme.primary,
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}
