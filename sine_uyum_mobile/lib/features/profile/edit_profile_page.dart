import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../widgets/poster_image.dart';

/// Galeri/kamera erişimi; testlerde sahtesiyle değiştirilir.
final imagePickerProvider = Provider<ImagePicker>((ref) => ImagePicker());

class EditProfilePage extends ConsumerStatefulWidget {
  const EditProfilePage({super.key});

  @override
  ConsumerState<EditProfilePage> createState() => _EditProfilePageState();
}

class _EditProfilePageState extends ConsumerState<EditProfilePage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _bioController;

  bool _isSubmitting = false;
  bool _isUploading = false;

  @override
  void initState() {
    super.initState();
    final user = ref.read(sessionProvider).user;
    _bioController = TextEditingController(text: user?.bio ?? '');
  }

  @override
  void dispose() {
    _bioController.dispose();
    super.dispose();
  }

  void _showMessage(String text) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
  }

  Future<void> _refreshAfterChange() async {
    await ref.read(sessionProvider.notifier).refreshUser();
    final userId = ref.read(sessionProvider).user?.id;
    if (userId != null) ref.invalidate(userProfileProvider(userId));
  }

  Future<void> _pickAndUpload(ImageSource source) async {
    // Platform seçiciler bu ayarlarla JPEG'e çevirir (iOS'ta HEIC dahil) ve
    // küçültür; sunucu yine de doğrulayıp 512x512'ye indirir.
    final file = await ref
        .read(imagePickerProvider)
        .pickImage(
          source: source,
          maxWidth: 1600,
          maxHeight: 1600,
          imageQuality: 85,
          preferredCameraDevice: CameraDevice.front,
        );
    if (file == null) return; // kullanıcı vazgeçti

    setState(() => _isUploading = true);
    try {
      await ref.read(socialRepositoryProvider).uploadProfilePhoto(file.path);
      await _refreshAfterChange();
      _showMessage('Profil fotoğrafı güncellendi.');
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isUploading = false);
    }
  }

  Future<void> _removePhoto() async {
    setState(() => _isUploading = true);
    try {
      await ref.read(socialRepositoryProvider).deleteProfilePhoto();
      await _refreshAfterChange();
      _showMessage('Profil fotoğrafı kaldırıldı.');
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isUploading = false);
    }
  }

  Future<void> _showPhotoOptions(bool hasPhoto) async {
    final action = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: const Text('Galeriden seç'),
              onTap: () => Navigator.of(context).pop('gallery'),
            ),
            ListTile(
              leading: const Icon(Icons.photo_camera_outlined),
              title: const Text('Fotoğraf çek'),
              onTap: () => Navigator.of(context).pop('camera'),
            ),
            if (hasPhoto)
              ListTile(
                leading: Icon(
                  Icons.delete_outline,
                  color: Theme.of(context).colorScheme.error,
                ),
                title: Text(
                  'Fotoğrafı kaldır',
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
                onTap: () => Navigator.of(context).pop('remove'),
              ),
          ],
        ),
      ),
    );

    switch (action) {
      case 'gallery':
        await _pickAndUpload(ImageSource.gallery);
      case 'camera':
        await _pickAndUpload(ImageSource.camera);
      case 'remove':
        await _removePhoto();
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSubmitting = true);
    try {
      await ref
          .read(socialRepositoryProvider)
          .updateProfile(
            bio: _bioController.text.trim(),
            // Fotoğraf yalnızca yükleme ile değişir; mevcut değer aynen gider.
            profileImageUrl: ref.read(sessionProvider).user?.profileImageUrl,
          );

      await _refreshAfterChange();

      if (!mounted) return;
      _showMessage('Profil güncellendi.');
      Navigator.of(context).pop();
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(sessionProvider).user;
    final hasPhoto = user?.profileImageUrl?.isNotEmpty ?? false;

    return Scaffold(
      appBar: AppBar(title: const Text('Profili düzenle')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Center(
                child: Stack(
                  alignment: Alignment.center,
                  children: [
                    UserAvatar(
                      userName: user?.userName ?? '',
                      imageUrl: user?.profileImageUrl,
                      radius: 48,
                    ),
                    if (_isUploading) const CircularProgressIndicator(),
                  ],
                ),
              ),
              TextButton.icon(
                key: const Key('change-photo'),
                onPressed: _isUploading
                    ? null
                    : () => _showPhotoOptions(hasPhoto),
                icon: const Icon(Icons.edit_outlined),
                label: Text(hasPhoto ? 'Fotoğrafı değiştir' : 'Fotoğraf ekle'),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _bioController,
                maxLines: 4,
                maxLength: 500,
                decoration: const InputDecoration(
                  labelText: 'Hakkında',
                  alignLabelWithHint: true,
                ),
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _isSubmitting || _isUploading ? null : _submit,
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
