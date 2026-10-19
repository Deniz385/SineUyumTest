import 'package:dio/dio.dart';

/// API hatalarını kullanıcıya gösterilebilir tek bir tipe indirger.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  bool get isUnauthorized => statusCode == 401;
  bool get isNotFound => statusCode == 404;

  /// Dio hatasını, sunucunun döndüğü Türkçe mesajı koruyarak dönüştürür.
  factory ApiException.fromDio(DioException error) {
    final statusCode = error.response?.statusCode;

    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return ApiException(
          'Sunucuya ulaşılamadı, bağlantınızı kontrol edin.',
          statusCode: statusCode,
        );
      case DioExceptionType.connectionError:
        return ApiException(
          'İnternet bağlantısı kurulamadı.',
          statusCode: statusCode,
        );
      case DioExceptionType.cancel:
        return ApiException('İstek iptal edildi.', statusCode: statusCode);
      default:
        return ApiException(
          _extractMessage(error.response?.data) ?? _defaultMessage(statusCode),
          statusCode: statusCode,
        );
    }
  }

  static String _defaultMessage(int? statusCode) {
    if (statusCode == 401) return 'Oturumunuz sona erdi, tekrar giriş yapın.';
    if (statusCode == 403) return 'Bu işlem için yetkiniz yok.';
    if (statusCode == 404) return 'Kayıt bulunamadı.';
    if (statusCode == 429) return 'Çok fazla deneme yaptınız, biraz bekleyin.';
    if (statusCode != null && statusCode >= 500) {
      return 'Sunucuda bir hata oluştu, lütfen sonra tekrar deneyin.';
    }
    return 'Beklenmeyen bir hata oluştu.';
  }

  /// Sunucu hata gövdesi üç farklı biçimde gelebiliyor:
  /// `{"message": "..."}`, ASP.NET ModelState (`{"errors": {...}}`),
  /// Identity hata dizisi (`[{"description": "..."}]`) veya düz metin.
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
          .map((item) =>
              item is Map ? (item['description'] ?? item['message']) : item)
          .whereType<String>()
          .where((value) => value.isNotEmpty);
      if (messages.isNotEmpty) return messages.join('\n');
    }

    return null;
  }

  @override
  String toString() => message;
}
