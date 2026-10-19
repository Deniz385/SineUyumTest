import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';

class EditProfilePage extends ConsumerStatefulWidget {
  const EditProfilePage({super.key});

  @override
  ConsumerState<EditProfilePage> createState() => _EditProfilePageState();
}

class _EditProfilePageState extends ConsumerState<EditProfilePage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _bioController;
  late final TextEditingController _imageUrlController;

  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    final user = ref.read(sessionProvider).user;
    _bioController = TextEditingController(text: user?.bio ?? '');
    _imageUrlController =
        TextEditingController(text: user?.profileImageUrl ?? '');
  }

  @override
  void dispose() {
    _bioController.dispose();
    _imageUrlController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSubmitting = true);
    try {
      await ref.read(socialRepositoryProvider).updateProfile(
            bio: _bioController.text.trim(),
            profileImageUrl: _imageUrlController.text.trim(),
          );

      await ref.read(sessionProvider.notifier).refreshUser();

      final userId = ref.read(sessionProvider).user?.id;
      if (userId != null) ref.invalidate(userProfileProvider(userId));

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Profil güncellendi.')),
      );
      Navigator.of(context).pop();
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
    return Scaffold(
      appBar: AppBar(title: const Text('Profili düzenle')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _bioController,
                maxLines: 4,
                maxLength: 500,
                decoration: const InputDecoration(
                  labelText: 'Hakkında',
                  alignLabelWithHint: true,
                ),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _imageUrlController,
                keyboardType: TextInputType.url,
                decoration: const InputDecoration(
                  labelText: 'Profil fotoğrafı adresi',
                  // API şu an dosya yükleme desteklemiyor, yalnızca URL alıyor.
                  // Galeriden seçim için upload endpoint'i gerekir
                  // (bkz. docs/ANALIZ-VE-YOL-HARITASI.md → B8).
                  helperText: 'https:// ile başlayan bir görsel adresi',
                ),
                validator: (value) {
                  final text = value?.trim() ?? '';
                  if (text.isEmpty) return null;
                  final uri = Uri.tryParse(text);
                  if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
                    return 'Geçerli bir adres giriniz.';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _isSubmitting ? null : _submit,
                child: _isSubmitting
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Kaydet'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
