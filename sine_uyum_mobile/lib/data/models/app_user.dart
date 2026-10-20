/// `/api/v1/account/login`, `/api/v1/account/me` yanıtlarındaki kullanıcı özeti.
class AppUser {
  const AppUser({
    required this.id,
    required this.userName,
    this.email,
    this.bio,
    this.profileImageUrl,
    this.isSubscribed = false,
    this.subscriptionExpires,
    this.roles = const [],
  });

  final String id;
  final String userName;
  final String? email;
  final String? bio;
  final String? profileImageUrl;
  final bool isSubscribed;
  final DateTime? subscriptionExpires;
  final List<String> roles;

  bool get isAdmin => roles.contains('Admin');

  factory AppUser.fromJson(Map<String, dynamic> json) {
    return AppUser(
      id: json['id'] as String? ?? '',
      userName: json['userName'] as String? ?? '',
      email: json['email'] as String?,
      bio: json['bio'] as String?,
      profileImageUrl: json['profileImageUrl'] as String?,
      isSubscribed: json['isSubscribed'] as bool? ?? false,
      subscriptionExpires: json['subscriptionExpires'] == null
          ? null
          : DateTime.tryParse(json['subscriptionExpires'] as String),
      roles:
          (json['roles'] as List?)?.map((e) => e.toString()).toList() ??
          const [],
    );
  }
}

/// [AppUser.fromJson] ile geri okunabilen biçim (çevrimdışı önbellek için).
extension AppUserJson on AppUser {
  Map<String, dynamic> toJson() => {
    'id': id,
    'userName': userName,
    'email': email,
    'bio': bio,
    'profileImageUrl': profileImageUrl,
    'isSubscribed': isSubscribed,
    'subscriptionExpires': subscriptionExpires?.toIso8601String(),
    'roles': roles,
  };
}

/// `/api/v1/account/login`, `/refresh` ve `/api/v1/subscription/activate` yanıtı.
class AuthResult {
  const AuthResult({
    required this.token,
    required this.refreshToken,
    required this.expiresAt,
    required this.user,
  });

  final String token;
  final String refreshToken;
  final DateTime? expiresAt;
  final AppUser user;

  factory AuthResult.fromJson(Map<String, dynamic> json) {
    return AuthResult(
      token: json['token'] as String? ?? '',
      refreshToken: json['refreshToken'] as String? ?? '',
      expiresAt: json['expiresAt'] == null
          ? null
          : DateTime.tryParse(json['expiresAt'] as String),
      user: AppUser.fromJson(
        (json['user'] as Map?)?.cast<String, dynamic>() ?? const {},
      ),
    );
  }
}

/// `/api/v1/account/search` sonucundaki kullanıcı + uyumluluk skoru.
class UserSearchResult {
  const UserSearchResult({
    required this.id,
    required this.userName,
    this.profileImageUrl,
    this.bio,
    this.compatibilityScore = 0,
    this.commonMovieCount = 0,
  });

  final String id;
  final String userName;
  final String? profileImageUrl;
  final String? bio;
  final double compatibilityScore;
  final int commonMovieCount;

  factory UserSearchResult.fromJson(Map<String, dynamic> json) {
    return UserSearchResult(
      id: json['id'] as String? ?? '',
      userName: json['userName'] as String? ?? '',
      profileImageUrl: json['profileImageUrl'] as String?,
      bio: json['bio'] as String?,
      compatibilityScore: (json['compatibilityScore'] as num?)?.toDouble() ?? 0,
      commonMovieCount: (json['commonMovieCount'] as num?)?.toInt() ?? 0,
    );
  }
}

/// `/api/v1/subscription` yanıtı.
class SubscriptionInfo {
  const SubscriptionInfo({
    required this.isActive,
    this.expiresAt,
    this.provider,
    this.willRenew = false,
    this.availableProviders = const [],
  });

  final bool isActive;
  final DateTime? expiresAt;
  final String? provider;

  /// false: iptal edildi, [expiresAt]'te sona erecek.
  final bool willRenew;

  /// Sunucuda yapılandırılmış ödeme sağlayıcıları (boşsa satın alınamaz).
  final List<String> availableProviders;

  factory SubscriptionInfo.fromJson(Map<String, dynamic> json) =>
      SubscriptionInfo(
        isActive: json['isActive'] as bool? ?? false,
        expiresAt: json['expiresAt'] == null
            ? null
            : DateTime.tryParse(json['expiresAt'] as String),
        provider: json['provider'] as String?,
        willRenew: json['willRenew'] as bool? ?? false,
        availableProviders:
            (json['availableProviders'] as List?)
                ?.map((e) => e.toString())
                .toList() ??
            const [],
      );
}
