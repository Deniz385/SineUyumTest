import '../../core/network/api_client.dart';
import '../models/year_in_review.dart';

class YearInReviewRepository {
  YearInReviewRepository(this._client);

  final ApiClient _client;

  /// [userId]'s recap of [year] (the current year so far by default).
  /// 403 for a private profile and 404 after a block, with the server's
  /// localized message.
  Future<YearInReview> forUser(String userId, {int? year}) async {
    final data = await _client.get<Map<String, dynamic>>(
      '/api/v1/year-in-review/user/${Uri.encodeComponent(userId)}',
      query: {'year': ?year},
    );
    return YearInReview.fromJson(data);
  }
}
