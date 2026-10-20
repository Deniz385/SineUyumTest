import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/profile.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

/// Gizlilik ayarları (G12). Her değişiklik anında kaydedilir; ayarlar
/// birbirinden bağımsızdır. Sunucu reddederse önceki değere dönülür.
class PrivacySettingsPage extends ConsumerStatefulWidget {
  const PrivacySettingsPage({super.key});

  @override
  ConsumerState<PrivacySettingsPage> createState() =>
      _PrivacySettingsPageState();
}

class _PrivacySettingsPageState extends ConsumerState<PrivacySettingsPage> {
  // Kaydedilmekte olan değer; sunucu yanıtına kadar ekranda bu görünür.
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
    final settings = ref.watch(privacySettingsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Gizlilik')),
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
              const _SectionTitle('Puanlarımı ve listelerimi kim görebilir'),
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
                        title: Text(option.label),
                      ),
                  ],
                ),
              ),
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  'Adın, fotoğrafın ve biyografin her zaman görünür.',
                  style: TextStyle(fontSize: 12),
                ),
              ),
              const Divider(height: 32),
              const _SectionTitle('Bana kim mesaj gönderebilir'),
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
                        title: Text(option.label),
                      ),
                  ],
                ),
              ),
              const Divider(height: 32),
              SwitchListTile(
                title: const Text('Kullanıcı aramasında görünme'),
                subtitle: const Text(
                  'Profil bağlantın ve mevcut sohbetlerin çalışmaya devam eder.',
                ),
                value: shown.hiddenFromSearch,
                onChanged: _saving
                    ? null
                    : (value) => save(shown.copyWith(hiddenFromSearch: value)),
              ),
              const Divider(height: 32),
              const _SectionTitle('Engellenen kullanıcılar'),
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
          return const Padding(
            padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Text(
              'Kimseyi engellemedin. Engellemek için kişinin profilini aç.',
            ),
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
                  child: const Text('Engeli kaldır'),
                ),
              ),
          ],
        );
      },
    );
  }
}
