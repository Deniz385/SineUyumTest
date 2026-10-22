import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/profile.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

/// Privacy settings (G12). Every change is saved immediately; the settings
/// are independent. If the server rejects a change, the previous value comes
/// back.
class PrivacySettingsPage extends ConsumerStatefulWidget {
  const PrivacySettingsPage({super.key});

  @override
  ConsumerState<PrivacySettingsPage> createState() =>
      _PrivacySettingsPageState();
}

class _PrivacySettingsPageState extends ConsumerState<PrivacySettingsPage> {
  // The value being saved; shown until the server responds.
  PrivacySettings? _pending;
  bool _saving = false;

  Future<void> _save(PrivacySettings next) async {
    setState(() {
      _pending = next;
      _saving = true;
    });
    try {
      await ref.read(socialRepositoryProvider).updatePrivacy(next);
      ref.invalidate(privacySettingsProvider);
      await ref.read(privacySettingsProvider.future);
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    } finally {
      if (mounted) {
        setState(() {
          _pending = null;
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final settings = ref.watch(privacySettingsProvider);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.privacyTitle)),
      body: settings.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(privacySettingsProvider),
        ),
        data: (saved) {
          final shown = _pending ?? saved;
          void save(PrivacySettings next) {
            if (!_saving) _save(next);
          }

          return ListView(
            children: [
              _SectionTitle(l10n.privacyWhoSeesRatings),
              RadioGroup<ProfileVisibility>(
                groupValue: shown.profileVisibility,
                onChanged: (value) {
                  if (value != null) {
                    save(shown.copyWith(profileVisibility: value));
                  }
                },
                child: Column(
                  children: [
                    for (final option in ProfileVisibility.values)
                      RadioListTile<ProfileVisibility>(
                        value: option,
                        enabled: !_saving,
                        title: Text(option.label(l10n)),
                      ),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  l10n.privacyAlwaysVisible,
                  style: const TextStyle(fontSize: 12),
                ),
              ),
              const Divider(height: 32),
              _SectionTitle(l10n.privacyWhoCanMessage),
              RadioGroup<MessagePermission>(
                groupValue: shown.messagePermission,
                onChanged: (value) {
                  if (value != null) {
                    save(shown.copyWith(messagePermission: value));
                  }
                },
                child: Column(
                  children: [
                    for (final option in MessagePermission.values)
                      RadioListTile<MessagePermission>(
                        value: option,
                        enabled: !_saving,
                        title: Text(option.label(l10n)),
                      ),
                  ],
                ),
              ),
              const Divider(height: 32),
              SwitchListTile(
                title: Text(l10n.privacyHideFromSearch),
                subtitle: Text(l10n.privacyHideFromSearchHint),
                value: shown.hiddenFromSearch,
                onChanged: _saving
                    ? null
                    : (value) => save(shown.copyWith(hiddenFromSearch: value)),
              ),
              SwitchListTile(
                title: Text(l10n.privacyReadReceipts),
                subtitle: Text(l10n.privacyReadReceiptsHint),
                value: shown.showReadReceipts,
                onChanged: _saving
                    ? null
                    : (value) => save(shown.copyWith(showReadReceipts: value)),
              ),
              const Divider(height: 32),
              _SectionTitle(l10n.privacyBlockedUsers),
              const _BlockedUsers(),
            ],
          );
        },
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Text(text, style: Theme.of(context).textTheme.titleSmall),
    );
  }
}

class _BlockedUsers extends ConsumerWidget {
  const _BlockedUsers();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final blocked = ref.watch(blockedUsersProvider);

    return blocked.when(
      loading: () => const Padding(
        padding: EdgeInsets.all(16),
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.invalidate(blockedUsersProvider),
      ),
      data: (users) {
        if (users.isEmpty) {
          return Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Text(context.l10n.privacyNoBlockedUsers),
          );
        }
        return Column(
          children: [
            for (final user in users)
              ListTile(
                leading: UserAvatar(
                  userName: user.userName,
                  imageUrl: user.profileImageUrl,
                ),
                title: Text(user.userName),
                onTap: () => context.push('/profile/${user.id}'),
                trailing: TextButton(
                  onPressed: () async {
                    try {
                      await ref.read(socialRepositoryProvider).unblock(user.id);
                      ref.invalidate(blockedUsersProvider);
                      ref.invalidate(userProfileProvider(user.id));
                    } catch (error) {
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(content: Text(error.toString())),
                        );
                      }
                    }
                  },
                  child: Text(context.l10n.commonUnblock),
                ),
              ),
          ],
        );
      },
    );
  }
}

extension on ProfileVisibility {
  String label(AppLocalizations l10n) => switch (this) {
    ProfileVisibility.public => l10n.visibilityPublic,
    ProfileVisibility.following => l10n.visibilityFollowing,
    ProfileVisibility.private => l10n.visibilityPrivate,
  };
}

extension on MessagePermission {
  String label(AppLocalizations l10n) => switch (this) {
    MessagePermission.everyone => l10n.messagePermissionEveryone,
    MessagePermission.following => l10n.messagePermissionFollowing,
    MessagePermission.nobody => l10n.messagePermissionNobody,
  };
}
