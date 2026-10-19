import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../providers/session_provider.dart';

class SubscriptionPage extends ConsumerStatefulWidget {
  const SubscriptionPage({super.key});

  @override
  ConsumerState<SubscriptionPage> createState() => _SubscriptionPageState();
}

class _SubscriptionPageState extends ConsumerState<SubscriptionPage> {
  bool _isSubmitting = false;

  Future<void> _activate() async {
    setState(() => _isSubmitting = true);
    try {
      await ref.read(sessionProvider.notifier).activateSubscription();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Abonelik aktifleştirildi.')),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(sessionProvider).user;
    final isSubscribed = user?.isSubscribed ?? false;

    return Scaffold(
      appBar: AppBar(title: const Text('Abonelik')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(
                        isSubscribed
                            ? Icons.workspace_premium
                            : Icons.workspace_premium_outlined,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                      const SizedBox(width: 8),
                      Text(
                        isSubscribed ? 'Aboneliğin aktif' : 'Abone değilsin',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ],
                  ),
                  if (isSubscribed && user?.subscriptionExpires != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      'Bitiş: ${DateFormat('d MMMM y', 'tr').format(user!.subscriptionExpires!.toLocal())}',
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),
          Text(
            'Abonelikle neler geliyor?',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          const _Benefit(text: 'Sinema etkinliklerine katılım'),
          const _Benefit(text: 'Grup eşleştirmede öncelik'),
          const _Benefit(text: 'Sınırsız izleme listesi'),
          const SizedBox(height: 24),
          // UYARI: Sunucu tarafı henüz ödeme doğrulaması yapmıyor; bu buton
          // aboneliği doğrudan aktifleştiriyor. Ödeme sağlayıcı entegrasyonu
          // yapılmadan yayına alınmamalı
          // (bkz. docs/ANALIZ-VE-YOL-HARITASI.md → G4).
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.errorContainer,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                Icon(
                  Icons.warning_amber_outlined,
                  color: Theme.of(context).colorScheme.onErrorContainer,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Ödeme entegrasyonu henüz tamamlanmadı. Bu ekran test '
                    'amaçlıdır.',
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.onErrorContainer,
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: _isSubmitting || isSubscribed ? null : _activate,
            child: Text(
              isSubscribed ? 'Zaten abonesin' : 'Aboneliği başlat (test)',
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
          const Icon(Icons.check_circle_outline, size: 18),
          const SizedBox(width: 8),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}
