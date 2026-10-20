import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/config/app_config.dart';
import '../../providers/push_provider.dart';
import '../../providers/session_provider.dart';
import '../../providers/theme_provider.dart';

class SettingsPage extends ConsumerWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final themeMode = ref.watch(themeModeProvider);
    final user = ref.watch(sessionProvider).user;

    return Scaffold(
      appBar: AppBar(title: const Text('Ayarlar')),
      body: ListView(
        children: [
          if (user != null)
            ListTile(
              leading: const Icon(Icons.person_outline),
              title: Text(user.userName),
              subtitle: Text(user.email ?? ''),
              onTap: () => context.push('/profile/${user.id}'),
            ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.brightness_6_outlined),
            title: const Text('Tema'),
            subtitle: Text(switch (themeMode) {
              ThemeMode.light => 'Açık',
              ThemeMode.dark => 'Koyu',
              ThemeMode.system => 'Sistem ayarı',
            }),
          ),
          RadioGroup<ThemeMode>(
            groupValue: themeMode,
            onChanged: (mode) {
              if (mode != null) {
                ref.read(themeModeProvider.notifier).set(mode);
              }
            },
            child: const Column(
              children: [
                RadioListTile<ThemeMode>(
                  value: ThemeMode.system,
                  title: Text('Sistem ayarı'),
                ),
                RadioListTile<ThemeMode>(
                  value: ThemeMode.light,
                  title: Text('Açık'),
                ),
                RadioListTile<ThemeMode>(
                  value: ThemeMode.dark,
                  title: Text('Koyu'),
                ),
              ],
            ),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.workspace_premium_outlined),
            title: const Text('Abonelik'),
            subtitle: Text(
              user?.isSubscribed == true ? 'Aktif' : 'Aktif değil',
            ),
            onTap: () => context.push('/subscription'),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.notifications_outlined),
            title: const Text('Anlık bildirimler'),
            subtitle: Text(switch (ref.watch(pushProvider)) {
              PushStatus.registered => 'Açık — uygulama kapalıyken de gelir',
              PushStatus.permissionDenied =>
                'Kapalı — cihaz ayarlarından bildirim izni verin',
              PushStatus.disabled => 'Bu derlemede yapılandırılmamış',
              PushStatus.idle => 'Hazırlanıyor…',
            }),
          ),
          ListTile(
            leading: const Icon(Icons.info_outline),
            title: const Text('Sürüm'),
            subtitle: Text(AppConfig.appVersion),
          ),
          ListTile(
            leading: const Icon(Icons.cloud_outlined),
            title: const Text('API adresi'),
            subtitle: Text(AppConfig.apiUrl),
          ),
          const Divider(),
          ListTile(
            leading: Icon(
              Icons.logout,
              color: Theme.of(context).colorScheme.error,
            ),
            title: Text(
              'Çıkış yap',
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
            onTap: () async {
              final confirmed = await showDialog<bool>(
                context: context,
                builder: (context) => AlertDialog(
                  title: const Text('Çıkış yapılsın mı?'),
                  actions: [
                    TextButton(
                      onPressed: () => Navigator.of(context).pop(false),
                      child: const Text('Vazgeç'),
                    ),
                    FilledButton(
                      onPressed: () => Navigator.of(context).pop(true),
                      child: const Text('Çıkış yap'),
                    ),
                  ],
                ),
              );

              if (confirmed == true) {
                await ref.read(sessionProvider.notifier).logout();
              }
            },
          ),
        ],
      ),
    );
  }
}
