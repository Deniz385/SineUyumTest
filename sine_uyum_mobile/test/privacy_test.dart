import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/data/models/app_user.dart';
import 'package:sine_uyum_mobile/data/models/profile.dart';
import 'package:sine_uyum_mobile/data/repositories/social_repository.dart';
import 'package:sine_uyum_mobile/features/profile/profile_page.dart';
import 'package:sine_uyum_mobile/features/settings/privacy_settings_page.dart';
import 'package:sine_uyum_mobile/providers/repository_providers.dart';
import 'package:sine_uyum_mobile/providers/session_provider.dart';

import 'support/localized_app.dart';

class FakeSocial implements SocialRepository {
  PrivacySettings settings = const PrivacySettings();
  final updates = <PrivacySettings>[];
  Object? updateError;
  List<BlockedUser> blocked = const [];
  final unblocked = <String>[];
  final blockedCalls = <String>[];
  int compatibilityCalls = 0;
  UserProfile profileToReturn = const UserProfile(
    id: 'u2',
    userName: 'mehmet',
    statistics: ProfileStatistics(),
  );

  @override
  Future<PrivacySettings> privacy() async => settings;

  @override
  Future<PrivacySettings> updatePrivacy(PrivacySettings next) async {
    updates.add(next);
    if (updateError != null) throw updateError!;
    return settings = next;
  }

  @override
  Future<List<BlockedUser>> blockedUsers() async => blocked;

  @override
  Future<void> unblock(String userId) async {
    unblocked.add(userId);
    blocked = blocked.where((u) => u.id != userId).toList();
    profileToReturn = UserProfile(
      id: userId,
      userName: profileToReturn.userName,
      statistics: const ProfileStatistics(),
    );
  }

  @override
  Future<void> block(String userId) async {
    blockedCalls.add(userId);
    profileToReturn = UserProfile(
      id: userId,
      userName: profileToReturn.userName,
      statistics: const ProfileStatistics(),
      isRestricted: true,
      isBlockedByMe: true,
    );
  }

  @override
  Future<UserProfile> profile(String userId) async => profileToReturn;

  @override
  Future<FollowStatus> followStatus(String userId) async =>
      const FollowStatus();

  @override
  Future<CompatibilityResult> compatibility(String userId) async {
    compatibilityCalls++;
    return const CompatibilityResult(compatibilityScore: 80);
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeSession extends SessionNotifier {
  @override
  SessionState build() => const SessionState(
    status: SessionStatus.authenticated,
    user: AppUser(id: 'u1', userName: 'ayse'),
  );
}

void main() {
  group('models', () {
    test('privacy settings round-trip in the server format', () {
      final parsed = PrivacySettings.fromJson({
        'profileVisibility': 'Following',
        'messagePermission': 'Nobody',
        'hiddenFromSearch': true,
      });
      expect(parsed.profileVisibility, ProfileVisibility.following);
      expect(parsed.messagePermission, MessagePermission.nobody);
      expect(parsed.hiddenFromSearch, isTrue);
      expect(parsed.toJson(), {
        'profileVisibility': 'Following',
        'messagePermission': 'Nobody',
        'hiddenFromSearch': true,
        'showReadReceipts': true,
      });
    });

    test("an unknown value falls back to the server's default", () {
      final parsed = PrivacySettings.fromJson({'profileVisibility': 'New'});
      expect(parsed.profileVisibility, ProfileVisibility.public);
      expect(parsed.messagePermission, MessagePermission.everyone);
    });

    test('profile restriction fields are read; false in an older response', () {
      final restricted = UserProfile.fromJson({
        'id': 'u2',
        'userName': 'x',
        'isRestricted': true,
        'isBlockedByMe': true,
      });
      expect(restricted.isRestricted, isTrue);
      expect(restricted.isBlockedByMe, isTrue);

      final old = UserProfile.fromJson({'id': 'u2', 'userName': 'x'});
      expect(old.isRestricted, isFalse);
      expect(old.isBlockedByMe, isFalse);
    });
  });

  Future<FakeSocial> pump(
    WidgetTester tester,
    Widget page, {
    FakeSocial? social,
    Locale locale = tr,
  }) async {
    final fake = social ?? FakeSocial();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          socialRepositoryProvider.overrideWithValue(fake),
          sessionProvider.overrideWith(FakeSession.new),
        ],
        child: localizedApp(locale: locale, home: page),
      ),
    );
    await tester.pumpAndSettle();
    return fake;
  }

  bool selected<T>(WidgetTester tester, String label) {
    final tile = tester.widget<RadioListTile<T>>(
      find.widgetWithText(RadioListTile<T>, label),
    );
    final group = tester.widget<RadioGroup<T>>(find.byType(RadioGroup<T>));
    return group.groupValue == tile.value;
  }

  testWidgets('a chosen setting is saved and the others are kept', (
    tester,
  ) async {
    final social = await pump(tester, const PrivacySettingsPage());

    await tester.tap(find.text('Hiç kimse'));
    await tester.pumpAndSettle();

    expect(social.updates, hasLength(1));
    expect(social.updates.single.messagePermission, MessagePermission.nobody);
    expect(social.updates.single.profileVisibility, ProfileVisibility.public);
    expect(social.updates.single.hiddenFromSearch, isFalse);
    expect(selected<MessagePermission>(tester, 'Hiç kimse'), isTrue);

    await tester.tap(find.byType(Switch));
    await tester.pumpAndSettle();
    expect(social.updates.last.hiddenFromSearch, isTrue);
    // The previous change isn't lost.
    expect(social.updates.last.messagePermission, MessagePermission.nobody);
  });

  testWidgets('if the server rejects it, the old value returns and the '
      'error is shown', (tester) async {
    final social = FakeSocial()..updateError = ApiException('Could not save.');
    await pump(tester, const PrivacySettingsPage(), social: social);

    await tester.tap(find.text('Yalnızca ben'));
    await tester.pumpAndSettle();

    expect(find.text('Could not save.'), findsOneWidget);
    expect(selected<ProfileVisibility>(tester, 'Herkes'), isTrue);
    expect(selected<ProfileVisibility>(tester, 'Yalnızca ben'), isFalse);
  });

  testWidgets('blocked users are listed and can be unblocked', (tester) async {
    final social = FakeSocial()
      ..blocked = const [BlockedUser(id: 'u9', userName: 'troll')];
    await pump(tester, const PrivacySettingsPage(), social: social);

    await tester.scrollUntilVisible(find.text('troll'), 100);
    await tester.tap(find.text('Engeli kaldır'));
    await tester.pumpAndSettle();

    expect(social.unblocked, ['u9']);
    expect(find.text('troll'), findsNothing);
  });

  testWidgets('a private profile requests no ratings/compatibility and '
      'shows a notice', (tester) async {
    final social = FakeSocial()
      ..profileToReturn = const UserProfile(
        id: 'u2',
        userName: 'mehmet',
        statistics: ProfileStatistics(),
        isRestricted: true,
      );
    await pump(tester, const ProfilePage(userId: 'u2'), social: social);

    expect(find.textContaining('gizli tutuyor'), findsOneWidget);
    expect(find.textContaining('Puanladığı filmler'), findsNothing);
    expect(find.text('Puanlama'), findsNothing);
    expect(social.compatibilityCalls, 0);
    // Following and messaging are still possible.
    expect(find.text('Takip et'), findsOneWidget);
    expect(find.text('Mesaj'), findsOneWidget);
  });

  testWidgets('blocking from the menu; while blocked only unblock is '
      'offered', (tester) async {
    final social = await pump(tester, const ProfilePage(userId: 'u2'));
    expect(social.compatibilityCalls, 1);

    await tester.tap(find.byTooltip('Diğer'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Engelle'));
    await tester.pumpAndSettle();
    // Confirmation dialog.
    await tester.tap(find.widgetWithText(FilledButton, 'Engelle'));
    await tester.pumpAndSettle();

    expect(social.blockedCalls, ['u2']);
    expect(find.textContaining('Bu kullanıcıyı engelledin'), findsOneWidget);
    expect(find.text('Mesaj'), findsNothing);
    expect(find.text('Takip et'), findsNothing);
    expect(find.byTooltip('Diğer'), findsNothing);

    await tester.tap(find.text('Engeli kaldır'));
    await tester.pumpAndSettle();
    expect(social.unblocked, ['u2']);
    expect(find.text('Mesaj'), findsOneWidget);
  });

  testWidgets('renders the privacy settings in English', (tester) async {
    final social = FakeSocial()
      ..blocked = const [BlockedUser(id: 'u9', userName: 'troll')];
    await pump(tester, const PrivacySettingsPage(), social: social, locale: en);

    expect(find.text('Privacy'), findsOneWidget);
    expect(find.text('Who can see my ratings and lists'), findsOneWidget);
    expect(find.text('Everyone'), findsNWidgets(2));
    expect(find.text('Only me'), findsOneWidget);

    await tester.tap(find.text('No one'));
    await tester.pumpAndSettle();
    expect(social.updates.single.messagePermission, MessagePermission.nobody);
    expect(selected<MessagePermission>(tester, 'No one'), isTrue);

    await tester.scrollUntilVisible(find.text('troll'), 100);
    expect(find.text('Unblock'), findsOneWidget);
  });

  testWidgets('renders a restricted profile in English', (tester) async {
    final social = FakeSocial()
      ..profileToReturn = const UserProfile(
        id: 'u2',
        userName: 'mehmet',
        statistics: ProfileStatistics(),
        isRestricted: true,
      );
    await pump(
      tester,
      const ProfilePage(userId: 'u2'),
      social: social,
      locale: en,
    );

    expect(
      find.text('This user keeps their ratings and lists private.'),
      findsOneWidget,
    );
    expect(find.text('Follow'), findsOneWidget);
    expect(find.text('Message'), findsOneWidget);
    expect(find.text('0 followers · 0 following'), findsOneWidget);
  });
}
