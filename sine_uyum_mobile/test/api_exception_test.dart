import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';
import 'package:sine_uyum_mobile/l10n/l10n.dart';

import 'support/localized_app.dart';

DioException _responseError(Object? data, {int statusCode = 400}) {
  final options = RequestOptions(path: '/api/test');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response(
      requestOptions: options,
      statusCode: statusCode,
      data: data,
    ),
  );
}

DioException _connectionError() => DioException(
  requestOptions: RequestOptions(path: '/api/test'),
  type: DioExceptionType.connectionError,
);

void main() {
  tearDown(() => L10n.locale = en);

  group('ApiException.fromDio', () {
    test('keeps the server message from a {"message": ...} body', () {
      final exception = _responseError({
        'message': 'You cannot send a message to yourself.',
      });
      expect(
        ApiException.fromDio(exception).message,
        'You cannot send a message to yourself.',
      );
    });

    test('keeps the ProblemDetails title (localized by the server)', () {
      L10n.locale = tr;
      final exception = ApiException.fromDio(
        _responseError({'title': 'Movie not found.'}, statusCode: 404),
      );
      // The server's text is shown as-is, not replaced by a client fallback.
      expect(exception.message, 'Movie not found.');
      expect(exception.isNotFound, isTrue);
    });

    test('joins ASP.NET ModelState errors', () {
      final exception = _responseError({
        'errors': {
          'Username': ['Username is required.'],
          'Password': ['Password is required.'],
        },
      });

      final message = ApiException.fromDio(exception).message;
      expect(message, contains('Username is required.'));
      expect(message, contains('Password is required.'));
    });

    test('reads description fields of an Identity error array', () {
      final exception = _responseError([
        {
          'code': 'DuplicateUserName',
          'description': 'This username is already taken.',
        },
      ]);
      expect(
        ApiException.fromDio(exception).message,
        'This username is already taken.',
      );
    });

    test('uses a plain-text body directly', () {
      final exception = _responseError('TMDb API key is not configured.');
      expect(
        ApiException.fromDio(exception).message,
        'TMDb API key is not configured.',
      );
    });

    test('without a body, falls back to a status message (Turkish)', () {
      L10n.locale = tr;
      expect(
        ApiException.fromDio(_responseError(null, statusCode: 429)).message,
        contains('Çok fazla deneme'),
      );
      expect(
        ApiException.fromDio(_responseError(null, statusCode: 500)).message,
        contains('Sunucuda bir hata'),
      );
    });

    test('without a body, falls back to a status message (English)', () {
      L10n.locale = en;
      final tooMany = ApiException.fromDio(
        _responseError(null, statusCode: 429),
      );
      expect(tooMany.kind, ApiErrorKind.tooManyRequests);
      expect(tooMany.message, contains('Too many attempts'));
      expect(
        ApiException.fromDio(_responseError(null, statusCode: 500)).message,
        contains('Something went wrong on the server'),
      );
    });

    test('a 401 is flagged as isUnauthorized', () {
      final exception = ApiException.fromDio(
        _responseError(null, statusCode: 401),
      );
      expect(exception.isUnauthorized, isTrue);
    });

    test('a connection error shows the network message', () {
      L10n.locale = tr;
      final exception = ApiException.fromDio(_connectionError());
      expect(exception.isNetworkError, isTrue);
      expect(exception.message, contains('İnternet bağlantısı'));

      L10n.locale = en;
      expect(
        exception.message,
        "Couldn't connect to the internet.",
        reason: 'client-side messages follow the current language',
      );
    });

    test('describe() uses the given localizations', () {
      final exception = ApiException.fromDio(_connectionError());
      expect(
        exception.describe(lookupAppLocalizations(tr)),
        'İnternet bağlantısı kurulamadı.',
      );
      expect(
        exception.describe(lookupAppLocalizations(en)),
        "Couldn't connect to the internet.",
      );
    });
  });
}
