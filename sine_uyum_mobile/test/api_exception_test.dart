import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sine_uyum_mobile/core/network/api_exception.dart';

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

void main() {
  group('ApiException.fromDio', () {
    test('{"message": ...} gövdesindeki sunucu mesajını korur', () {
      final exception = _responseError({
        'message': 'Kendinize mesaj gönderemezsiniz.',
      });
      expect(
        ApiException.fromDio(exception).message,
        'Kendinize mesaj gönderemezsiniz.',
      );
    });

    test('ASP.NET ModelState hatalarını birleştirir', () {
      final exception = _responseError({
        'errors': {
          'Username': ['Kullanıcı adı zorunludur.'],
          'Password': ['Şifre zorunludur.'],
        },
      });

      final message = ApiException.fromDio(exception).message;
      expect(message, contains('Kullanıcı adı zorunludur.'));
      expect(message, contains('Şifre zorunludur.'));
    });

    test('Identity hata dizisindeki description alanlarını okur', () {
      final exception = _responseError([
        {
          'code': 'DuplicateUserName',
          'description': 'Bu kullanıcı adı alınmış.',
        },
      ]);
      expect(
        ApiException.fromDio(exception).message,
        'Bu kullanıcı adı alınmış.',
      );
    });

    test('düz metin gövdeyi doğrudan kullanır', () {
      final exception = _responseError('TMDb API anahtarı yapılandırılmamış.');
      expect(
        ApiException.fromDio(exception).message,
        'TMDb API anahtarı yapılandırılmamış.',
      );
    });

    test('gövde yoksa duruma uygun varsayılan mesaj döner', () {
      expect(
        ApiException.fromDio(_responseError(null, statusCode: 429)).message,
        contains('Çok fazla deneme'),
      );
      expect(
        ApiException.fromDio(_responseError(null, statusCode: 500)).message,
        contains('Sunucuda bir hata'),
      );
    });

    test('401 isUnauthorized olarak işaretlenir', () {
      final exception = ApiException.fromDio(
        _responseError(null, statusCode: 401),
      );
      expect(exception.isUnauthorized, isTrue);
    });

    test('bağlantı hatasında ağ mesajı gösterilir', () {
      final exception = DioException(
        requestOptions: RequestOptions(path: '/api/test'),
        type: DioExceptionType.connectionError,
      );
      expect(
        ApiException.fromDio(exception).message,
        contains('İnternet bağlantısı'),
      );
    });
  });
}
