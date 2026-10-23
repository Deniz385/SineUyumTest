import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/config/app_config.dart';
import '../../l10n/l10n.dart';
import '../../providers/locale_provider.dart';
import '../../providers/push_provider.dart';
import '../../providers/session_provider.dart';
import '../../providers/theme_provider.dart';

class SettingsPage extends ConsumerWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final themeMode = ref.watch(themeModeProvider);
    final user = ref.watch(sessionProvider).user;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.commonSettings)),
      body: ListView(
        children: [
          if (user != null)
            ListTile(
              leading: const Icon(Icons.person_outline_rounded),
              title: Text(user.userName),
              subtitle: Text(user.email ?? ''),
              onTap: () => context.push('/profile/${user.id}'),
            ),
          ListTile(
            leading: const Icon(Icons.lock_outline_rounded),
            title: Text(l10n.privacyTitle),
            subtitle: Text(l10n.settingsPrivacySubtitle),
            onTap: () => context.push('/settings/privacy'),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.brightness_6_rounded),
            title: Text(l10n.settingsTheme),
            subtitle: Text(switch (themeMode) {
              ThemeMode.light => l10n.themeLight,
              ThemeMode.dark => l10n.themeDark,
              ThemeMode.system => l10n.themeSystem,
            }),
          ),
          RadioGroup<ThemeMode>(
            groupValue: themeMode,
            onChanged: (mode) {
              if (mode != null) {
                ref.read(themeModeProvider.notifier).set(mode);
              }
            },
            child: Column(
              children: [
                RadioListTile<ThemeMode>(
                  value: ThemeMode.system,
                  title: Text(l10n.themeSystem),
                ),
                RadioListTile<ThemeMode>(
                  value: ThemeMode.light,
                  title: Text(l10n.themeLight),
                ),
                RadioListTile<ThemeMode>(
                  value: ThemeMode.dark,
                  title: Text(l10n.themeDark),
                ),
              ],
            ),
          ),
          const Divider(),
          const _LanguageSection(),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.workspace_premium_rounded),
            title: Text(l10n.commonSubscription),
            subtitle: Text(
              user?.isSubscribed == true
                  ? l10n.subscriptionActive
                  : l10n.subscriptionInactive,
            ),
            onTap: () => context.push('/subscription'),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.notifications_rounded),
            title: Text(l10n.settingsPush),
            subtitle: Text(switch (ref.watch(pushProvider)) {
              PushStatus.registered => l10n.pushRegistered,
              PushStatus.permissionDenied => l10n.pushPermissionDenied,
              PushStatus.disabled => l10n.pushDisabled,
              PushStatus.idle => l10n.pushIdle,
            }),
          ),
          ListTile(
            leading: const Icon(Icons.info_outline_rounded),
            title: Text(l10n.settingsVersion),
            subtitle: Text(AppConfig.appVersion),
          ),
          ListTile(
            leading: const Icon(Icons.cloud_rounded),
            title: Text(l10n.settingsApiUrl),
            subtitle: Text(AppConfig.apiUrl),
          ),
          const Divider(),
          ListTile(
            leading: Icon(
              Icons.logout_rounded,
              color: Theme.of(context).colorScheme.error,
            ),
            title: Text(
              l10n.logout,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
            onTap: () async {
              final confirmed = await showDialog<bool>(
                context: context,
                builder: (context) => AlertDialog(
                  title: Text(l10n.logoutConfirmTitle),
                  actions: [
                    TextButton(
                      onPressed: () => Navigator.of(context).pop(false),
                      child: Text(l10n.commonCancel),
                    ),
                    FilledButton(
                      onPressed: () => Navigator.of(context).pop(true),
                      child: Text(l10n.logout),
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

/// "System default" plus every supported language. Each language is listed
/// under its own name (the `languageName` key of its ARB file), so adding a
/// translation file adds it here automatically.
class _LanguageSection extends ConsumerWidget {
  const _LanguageSection();

  // Radio value for "follow the device"; languages use their code.
  static const _system = '';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final chosen = ref.watch(localeProvider);

    return Column(
      children: [
        ListTile(
          leading: const Icon(Icons.language_rounded),
          title: Text(l10n.settingsLanguage),
          subtitle: Text(
            chosen == null
                ? l10n.languageSystem
                : lookupAppLocalizations(chosen).languageName,
          ),
        ),
        RadioGroup<String>(
          groupValue: chosen?.languageCode ?? _system,
          onChanged: (code) {
            if (code == null) return;
            ref
                .read(localeProvider.notifier)
                .set(code == _system ? null : L10n.supported(code));
          },
          child: Column(
            children: [
              RadioListTile<String>(
                value: _system,
                title: Text(l10n.languageSystem),
              ),
              for (final locale in AppLocalizations.supportedLocales)
                RadioListTile<String>(
                  value: locale.languageCode,
                  title: Text(lookupAppLocalizations(locale).languageName),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
