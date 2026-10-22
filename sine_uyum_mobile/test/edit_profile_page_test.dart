import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:sine_uyum_mobile/core/config/app_config.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/profile/edit_profile_page.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/localized_app.dart';

class FakePicker extends ImagePicker {
  XFile? next = XFile('/tmp/picked.jpg');
  final calls = <(ImageSource, double?, int?)>[];

  @override
  Future<XFile?> pickImage({
    required ImageSource source,
    double? maxWidth,
    double? maxHeight,
    int? imageQuality,
    CameraDevice preferredCameraDevice = CameraDevice.rear,
    bool requestFullMetadata = true,
  }) async {
    calls.add((source, maxWidth, imageQuality));
    return next;
  }
}

class FakeSocial implements SocialRepository {
  String? photoUrl;
  final uploads = <String>[];
  int deletes = 0;
  final updates = <(String?, String?)>[];
  Object? uploadError;

  @override
  Future<String?> uploadProfilePhoto(String filePath) async {
    if (uploadError != null) throw uploadError!;
    uploads.add(filePath);
    return photoUrl = '/media/avatars/u1/${uploads.length}.jpg';
  }

  @override
  Future<void> deleteProfilePhoto() async {
    deletes++;
    photoUrl = null;
  }

  @override
  Future<void> updateProfile({String? bio, String? profileImageUrl}) async =>
      updates.add((bio, profileImageUrl));

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

/// The session's user is refreshed with the photo of the fake repository.
class FakeSession extends SessionNotifier {
  FakeSession(this.social);
  final FakeSocial social;

  @override
  SessionState build() => _state();

  SessionState _state() => SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(
      id: 'u1',
      userName: 'ayse',
      bio: 'old bio',
      profileImageUrl: social.photoUrl,
    ),
  );

  @override
  Future<void> refreshUser() async => state = _state();
}

void main() {
  late FakePicker picker;
  late FakeSocial social;

  Future<void> pumpPage(WidgetTester tester, {Locale locale = tr}) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          imagePickerProvider.overrideWithValue(picker),
          socialRepositoryProvider.overrideWithValue(social),
          sessionProvider.overrideWith(() => FakeSession(social)),
        ],
        child: localizedApp(locale: locale, home: const EditProfilePage()),
      ),
    );
  }

  setUp(() {
    picker = FakePicker();
    social = FakeSocial();
  });

  testWidgets('a photo picked from the gallery is uploaded with the '
      'downscaling settings', (tester) async {
    await pumpPage(tester);
    expect(find.text('Fotoğraf ekle'), findsOneWidget);

    await tester.tap(find.byKey(const Key('change-photo')));
    await tester.pumpAndSettle();
    // Without a photo the remove option isn't offered.
    expect(find.text('Fotoğrafı kaldır'), findsNothing);
    await tester.tap(find.text('Galeriden seç'));
    await tester.pumpAndSettle();

    expect(picker.calls.single, (ImageSource.gallery, 1600.0, 85));
    expect(social.uploads, ['/tmp/picked.jpg']);
    expect(find.text('Profil fotoğrafı güncellendi.'), findsOneWidget);
    expect(find.text('Fotoğrafı değiştir'), findsOneWidget);
  });

  testWidgets('nothing is uploaded when the user cancels', (tester) async {
    picker.next = null;
    await pumpPage(tester);

    await tester.tap(find.byKey(const Key('change-photo')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Fotoğraf çek'));
    await tester.pumpAndSettle();

    expect(picker.calls.single.$1, ImageSource.camera);
    expect(social.uploads, isEmpty);
  });

  testWidgets('an existing photo can be removed', (tester) async {
    social.photoUrl = '/media/avatars/u1/old.jpg';
    await pumpPage(tester);

    await tester.tap(find.byKey(const Key('change-photo')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Fotoğrafı kaldır'));
    await tester.pumpAndSettle();

    expect(social.deletes, 1);
    expect(find.text('Fotoğraf ekle'), findsOneWidget);
  });

  testWidgets('a server error is shown to the user', (tester) async {
    social.uploadError = ApiException(
      'The photo can be at most 10 MB.',
      statusCode: 413,
    );
    await pumpPage(tester);

    await tester.tap(find.byKey(const Key('change-photo')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Galeriden seç'));
    await tester.pumpAndSettle();

    expect(find.text('The photo can be at most 10 MB.'), findsOneWidget);
  });

  testWidgets('save sends the current photo URL unchanged', (tester) async {
    social.photoUrl = '/media/avatars/u1/current.jpg';
    await pumpPage(tester);

    await tester.enterText(find.byType(TextFormField), 'new bio');
    await tester.tap(find.text('Kaydet'));
    await tester.pumpAndSettle();

    expect(social.updates.single, ('new bio', '/media/avatars/u1/current.jpg'));
  });

  testWidgets('renders in English', (tester) async {
    await pumpPage(tester, locale: en);
    expect(find.text('Edit profile'), findsOneWidget);
    expect(find.text('Add photo'), findsOneWidget);
    expect(find.text('About'), findsOneWidget);

    await tester.tap(find.byKey(const Key('change-photo')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Choose from gallery'));
    await tester.pumpAndSettle();
    expect(find.text('Profile photo updated.'), findsOneWidget);
    expect(find.text('Change photo'), findsOneWidget);
  });

  group('AppConfig.resolveMediaUrl', () {
    test('joins a relative path with the API URL', () {
      expect(
        AppConfig.resolveMediaUrl('/media/avatars/u1/a.jpg'),
        '${AppConfig.apiUrl}/media/avatars/u1/a.jpg',
      );
    });

    test('full URLs and empty values', () {
      expect(
        AppConfig.resolveMediaUrl('https://cdn.example/a.jpg'),
        'https://cdn.example/a.jpg',
      );
      expect(AppConfig.resolveMediaUrl(''), isNull);
      expect(AppConfig.resolveMediaUrl(null), isNull);
    });
  });
}
