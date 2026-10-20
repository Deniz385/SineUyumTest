import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/offline/network_status.dart';

/// Saklı token okunurken gösterilen açılış ekranı. İlk açılışta ağ yoksa
/// oturum doğrulanamaz; token'lar korunur ve ağ dönünce devam edilir.
class SplashPage extends ConsumerWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final offline = ref.watch(networkStatusProvider).isOffline;
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.movie_filter_outlined,
              size: 64,
              color: Theme.of(context).colorScheme.primary,
            ),
            const SizedBox(height: 16),
            Text('SineUyum', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 24),
            const CircularProgressIndicator(),
            if (offline) ...[
              const SizedBox(height: 16),
              const Text('Bağlantı bekleniyor…'),
            ],
          ],
        ),
      ),
    );
  }
}
