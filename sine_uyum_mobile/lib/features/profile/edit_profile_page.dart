import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../../widgets/poster_image.dart';

/// Gallery/camera access; replaced with a fake in tests.
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
    // With these settings the platform pickers convert to JPEG (HEIC on iOS
    // included) and downscale; the server still validates and resizes to
    // 512x512.
    final file = await ref
        .read(imagePickerProvider)
        .pickImage(
          source: source,
          maxWidth: 1600,
          maxHeight: 1600,
          imageQuality: 85,
          preferredCameraDevice: CameraDevice.front,
        );
    if (file == null) return; // the user cancelled

    setState(() => _isUploading = true);
    try {
      await ref.read(socialRepositoryProvider).uploadProfilePhoto(file.path);
      await _refreshAfterChange();
      if (mounted) _showMessage(context.l10n.photoUpdated);
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
      if (mounted) _showMessage(context.l10n.photoRemoved);
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isUploading = false);
    }
  }

  Future<void> _showPhotoOptions(bool hasPhoto) async {
    final l10n = context.l10n;
    final action = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: Text(l10n.pickFromGallery),
              onTap: () => Navigator.of(context).pop('gallery'),
            ),
            ListTile(
              leading: const Icon(Icons.photo_camera_outlined),
              title: Text(l10n.takePhoto),
              onTap: () => Navigator.of(context).pop('camera'),
            ),
            if (hasPhoto)
              ListTile(
                leading: Icon(
                  Icons.delete_outline,
                  color: Theme.of(context).colorScheme.error,
                ),
                title: Text(
                  l10n.removePhoto,
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
            // The photo only changes via upload; send the current value as-is.
            profileImageUrl: ref.read(sessionProvider).user?.profileImageUrl,
          );

      await _refreshAfterChange();

      if (!mounted) return;
      _showMessage(context.l10n.profileUpdated);
      Navigator.of(context).pop();
    } catch (error) {
      _showMessage(error.toString());
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final user = ref.watch(sessionProvider).user;
    final hasPhoto = user?.profileImageUrl?.isNotEmpty ?? false;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.editProfile)),
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
                label: Text(hasPhoto ? l10n.changePhoto : l10n.addPhoto),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _bioController,
                maxLines: 4,
                maxLength: 500,
                decoration: InputDecoration(
                  labelText: l10n.bioLabel,
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
                    : Text(l10n.commonSave),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
