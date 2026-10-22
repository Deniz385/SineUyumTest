import 'package:dio/dio.dart';

import '../../l10n/l10n.dart';

/// Why a request failed when the server gave no message of its own; picks
/// the client-side text shown to the user.
enum ApiErrorKind {
  timeout,
  noConnection,
  cancelled,

  /// Offline and the response was never cached.
  offlineNotCached,
  unauthorized,
  forbidden,
  notFound,
  tooManyRequests,
  server,
  unexpected;

  static ApiErrorKind forStatus(int? statusCode) => switch (statusCode) {
    401 => unauthorized,
    403 => forbidden,
    404 => notFound,
    429 => tooManyRequests,
    final int code when code >= 500 => server,
    _ => unexpected,
  };
}

/// Reduces API failures to a single type the UI can show.
///
/// Messages from the server (ProblemDetails `title` etc.) are already in the
/// language sent in `Accept-Language` and are shown as-is. Failures without
/// one (no network, empty error body) are described by [kind] and translated
/// when read, so they follow the current app language.
class ApiException implements Exception {
  ApiException(
    this.serverMessage, {
    this.statusCode,
    this.isNetworkError = false,
    ApiErrorKind? kind,
  }) : kind = kind ?? ApiErrorKind.forStatus(statusCode);

  /// The server's message, if it sent one.
  final String? serverMessage;
  final ApiErrorKind kind;
  final int? statusCode;

  /// The server was never reached (no connection / timeout). This is not a
  /// rejection by the server (4xx/5xx), so the session and data must be kept.
  final bool isNetworkError;

  bool get isUnauthorized => statusCode == 401;
  bool get isNotFound => statusCode == 404;

  /// Text for the user, in the current app language.
  String get message => describe(L10n.current);

  /// Text for the user, using [l10n] for client-side messages.
  String describe(AppLocalizations l10n) =>
      serverMessage ??
      switch (kind) {
        ApiErrorKind.timeout => l10n.errorServerUnreachable,
        ApiErrorKind.noConnection => l10n.errorNoInternet,
        ApiErrorKind.cancelled => l10n.errorRequestCancelled,
        ApiErrorKind.offlineNotCached => l10n.errorOfflineNotCached,
        ApiErrorKind.unauthorized => l10n.errorSessionExpired,
        ApiErrorKind.forbidden => l10n.errorForbidden,
        ApiErrorKind.notFound => l10n.errorNotFound,
        ApiErrorKind.tooManyRequests => l10n.errorTooManyRequests,
        ApiErrorKind.server => l10n.errorServer,
        ApiErrorKind.unexpected => l10n.errorUnexpected,
      };

  /// Converts a Dio error, keeping the server's own message when present.
  factory ApiException.fromDio(DioException error) {
    final statusCode = error.response?.statusCode;

    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return ApiException(
          null,
          statusCode: statusCode,
          isNetworkError: true,
          kind: ApiErrorKind.timeout,
        );
      case DioExceptionType.connectionError:
        return ApiException(
          null,
          statusCode: statusCode,
          isNetworkError: true,
          kind: ApiErrorKind.noConnection,
        );
      case DioExceptionType.cancel:
        return ApiException(
          null,
          statusCode: statusCode,
          kind: ApiErrorKind.cancelled,
        );
      default:
        return ApiException(
          _extractMessage(error.response?.data),
          statusCode: statusCode,
        );
    }
  }

  /// The server's error body comes in several shapes:
  /// `{"message": "..."}` / ProblemDetails (`title`, `detail`), ASP.NET
  /// ModelState (`{"errors": {...}}`), an Identity error array
  /// (`[{"description": "..."}]`) or plain text.
  static String? _extractMessage(Object? data) {
    if (data == null) return null;

    if (data is String) {
      return data.trim().isEmpty ? null : data;
    }

    if (data is Map) {
      final message = data['message'] ?? data['title'] ?? data['detail'];
      if (message is String && message.isNotEmpty) return message;

      final errors = data['errors'];
      if (errors is Map) {
        final messages = errors.values
            .expand((value) => value is List ? value : [value])
            .map((value) => value.toString())
            .where((value) => value.isNotEmpty);
        if (messages.isNotEmpty) return messages.join('\n');
      }
    }

    if (data is List) {
      final messages = data
          .map(
            (item) =>
                item is Map ? (item['description'] ?? item['message']) : item,
          )
          .whereType<String>()
          .where((value) => value.isNotEmpty);
      if (messages.isNotEmpty) return messages.join('\n');
    }

    return null;
  }

  @override
  String toString() => message;
}
