import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/config/app_config.dart';
import '../../core/theme/tokens.dart';
import '../../core/ui/poster_image.dart';
import '../../l10n/l10n.dart';
import '../../providers/locale_provider.dart';
import '../../providers/push_provider.dart';
import '../../providers/session_provider.dart';
import '../../providers/theme_provider.dart';

class SettingsPage extends ConsumerWidget {
  const SettingsPage({super.key});

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    final l10n = context.l10n;
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
            style: FilledButton.styleFrom(
              backgroundColor: context.colors.accentStrong,
              foregroundColor: Colors.white,
            ),
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.logout),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await ref.read(sessionProvider.notifier).logout();
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final colors = context.colors;
    final themeMode = ref.watch(themeModeProvider);
    final user = ref.watch(sessionProvider).user;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.commonSettings)),
      body: ListView(
        padding: const EdgeInsets.only(bottom: AppSpace.xxl),
        children: [
          _Group(
            title: l10n.settingsAccount,
            children: [
              if (user != null)
                ListTile(
                  leading: UserAvatar(
                    userName: user.userName,
                    imageUrl: user.profileImageUrl,
                  ),
                  title: Text(user.userName),
                  subtitle: Text(user.email ?? ''),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () => context.push('/profile/${user.id}'),
                ),
              ListTile(
                leading: const Icon(Icons.lock_outline_rounded),
                title: Text(l10n.privacyTitle),
                subtitle: Text(l10n.settingsPrivacySubtitle),
                trailing: const Icon(Icons.chevron_right_rounded),
                onTap: () => context.push('/settings/privacy'),
              ),
              ListTile(
                leading: const Icon(Icons.workspace_premium_rounded),
                title: Text(l10n.commonSubscription),
                subtitle: Text(
                  user?.isSubscribed == true
                      ? l10n.subscriptionActive
                      : l10n.subscriptionInactive,
                ),
                trailing: const Icon(Icons.chevron_right_rounded),
                onTap: () => context.push('/subscription'),
              ),
            ],
          ),
          _Group(
            title: l10n.settingsTheme,
            children: [
              Padding(
                padding: const EdgeInsets.all(AppSpace.sm),
                child: SizedBox(
                  width: double.infinity,
                  child: SegmentedButton<ThemeMode>(
                    showSelectedIcon: false,
                    segments: [
                      ButtonSegment(
                        value: ThemeMode.dark,
                        icon: const Icon(Icons.dark_mode_rounded),
                        label: Text(l10n.themeDark),
                      ),
                      ButtonSegment(
                        value: ThemeMode.light,
                        icon: const Icon(Icons.light_mode_rounded),
                        label: Text(l10n.themeLight),
                      ),
                      ButtonSegment(
                        value: ThemeMode.system,
                        icon: const Icon(Icons.brightness_auto_rounded),
                        label: Text(
                          l10n.themeSystem,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                    selected: {themeMode},
                    onSelectionChanged: (selection) => ref
                        .read(themeModeProvider.notifier)
                        .set(selection.first),
                  ),
                ),
              ),
            ],
          ),
          _Group(
            title: l10n.settingsLanguage,
            children: const [_LanguageSection()],
          ),
          _Group(
            title: l10n.settingsAbout,
            children: [
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
            ],
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AppSpace.gutter,
              AppSpace.xl,
              AppSpace.gutter,
              0,
            ),
            child: OutlinedButton.icon(
              style: OutlinedButton.styleFrom(
                foregroundColor: colors.error,
                side: BorderSide(color: colors.error.withValues(alpha: 0.6)),
              ),
              onPressed: () => _logout(context, ref),
              icon: const Icon(Icons.logout_rounded),
              label: Text(l10n.logout),
            ),
          ),
        ],
      ),
    );
  }
}

/// A titled group of settings on a card.
class _Group extends StatelessWidget {
  const _Group({required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AppSpace.gutter,
        AppSpace.lg,
        AppSpace.gutter,
        0,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(left: 4, bottom: AppSpace.xs),
            child: Semantics(
              header: true,
              child: Text(
                context.l10n.upper(title),
                style: context.type.overline,
              ),
            ),
          ),
          // A Material (not a decorated box) so the tiles' ink shows.
          Material(
            clipBehavior: Clip.antiAlias,
            color: colors.surface,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadii.md),
              side: BorderSide(color: colors.border),
            ),
            child: Column(
              children: [
                for (final (i, child) in children.indexed) ...[
                  if (i > 0) const Divider(indent: AppSpace.gutter),
                  child,
                ],
              ],
            ),
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
