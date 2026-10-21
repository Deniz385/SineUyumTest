import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/social.dart';
import '../../providers/repository_providers.dart';
import '../../providers/social_provider.dart';
import 'review_card.dart';

/// Film sayfasındaki incelemeler: kendi incelemesini yaz/düzenle/sil ve
/// başkalarının incelemeleri.
class MovieReviewsSection extends ConsumerStatefulWidget {
  const MovieReviewsSection({super.key, required this.movieId});

  final int movieId;

  @override
  ConsumerState<MovieReviewsSection> createState() =>
      _MovieReviewsSectionState();
}

class _MovieReviewsSectionState extends ConsumerState<MovieReviewsSection> {
  final _controller = TextEditingController();
  bool _spoilers = false;
  bool _editing = false;
  bool _saving = false;
  int? _seededFor;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _seed(MovieReviews data) {
    // Taslak her yüklemede değil, inceleme değişince doldurulur.
    final key = data.mine?.id ?? -1;
    if (_seededFor == key) return;
    _seededFor = key;
    _controller.text = data.mine?.text ?? '';
    _spoilers = data.mine?.containsSpoilers ?? false;
    _editing = data.mine == null;
  }

  Future<void> _save() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _saving) return;
    setState(() => _saving = true);
    try {
      await ref
          .read(socialRepositoryProvider)
          .upsertReview(
            widget.movieId,
            text: text,
            containsSpoilers: _spoilers,
          );
      _seededFor = null;
      ref.invalidate(movieReviewsProvider(widget.movieId));
      ref.invalidate(feedProvider);
      _show('İncelemen kaydedildi.');
    } catch (error) {
      _show(error.toString());
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _delete() async {
    try {
      await ref.read(socialRepositoryProvider).deleteReview(widget.movieId);
      _seededFor = null;
      ref.invalidate(movieReviewsProvider(widget.movieId));
      ref.invalidate(feedProvider);
    } catch (error) {
      _show(error.toString());
    }
  }

  void _show(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final reviews = ref.watch(movieReviewsProvider(widget.movieId));

    return reviews.when(
      loading: () => const Padding(
        padding: EdgeInsets.all(24),
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: Text('İncelemeler yüklenemedi: $error'),
      ),
      data: (data) {
        _seed(data);
        final mine = data.mine;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 24),
            Text(
              data.totalCount > 0
                  ? 'İncelemeler (${data.totalCount})'
                  : 'İncelemeler',
              style: theme.textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (data.myRating == null)
              const Card(
                child: ListTile(
                  leading: Icon(Icons.info_outline),
                  title: Text('İnceleme yazmak için önce filmi puanla.'),
                ),
              )
            else if (_editing) ...[
              TextField(
                controller: _controller,
                minLines: 3,
                maxLines: 8,
                maxLength: 2000,
                decoration: InputDecoration(
                  labelText: 'İncelemen (puanın: ${data.myRating}/10)',
                  border: const OutlineInputBorder(),
                ),
              ),
              Row(
                children: [
                  Expanded(
                    child: CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      controlAffinity: ListTileControlAffinity.leading,
                      title: const Text('Spoiler içeriyor'),
                      value: _spoilers,
                      onChanged: (v) => setState(() => _spoilers = v ?? false),
                    ),
                  ),
                  if (mine != null)
                    TextButton(
                      onPressed: () => setState(() => _editing = false),
                      child: const Text('Vazgeç'),
                    ),
                  FilledButton(
                    onPressed: _saving ? null : _save,
                    child: Text(mine == null ? 'Paylaş' : 'Güncelle'),
                  ),
                ],
              ),
            ] else if (mine != null) ...[
              ReviewCard(review: mine),
              Row(
                children: [
                  TextButton(
                    onPressed: () => setState(() => _editing = true),
                    child: const Text('Düzenle'),
                  ),
                  TextButton(
                    onPressed: _delete,
                    child: Text(
                      'Sil',
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 8),
            if (data.reviews.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Text('Henüz başka inceleme yok. İlk yazan sen ol.'),
              )
            else
              for (final review in data.reviews) ReviewCard(review: review),
          ],
        );
      },
    );
  }
}

/// Film başlığının yanında "Sana göre" puanı; yeterli veri yoksa hiçbir şey.
/// İncelemelerle aynı istekten gelir (ek çağrı yok).
class ForYouBadge extends ConsumerWidget {
  const ForYouBadge({super.key, required this.movieId});

  final int movieId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final forYou = ref.watch(movieReviewsProvider(movieId)).value?.forYou;
    if (forYou == null) return const SizedBox.shrink();
    final theme = Theme.of(context);
    final who = forYou.topMatches
        .map(
          (m) =>
              '${m.author.userName} (%${m.compatibility.round()}) ${m.rating}',
        )
        .join(', ');

    return Tooltip(
      message: 'Sana uyumlu ${forYou.basedOn} kişiye göre: $who',
      triggerMode: TooltipTriggerMode.tap,
      child: Padding(
        padding: const EdgeInsets.only(top: 6),
        child: Row(
          children: [
            Icon(Icons.favorite, size: 16, color: theme.colorScheme.secondary),
            const SizedBox(width: 4),
            Text(
              forYou.score.toStringAsFixed(1),
              style: theme.textTheme.titleSmall?.copyWith(
                color: theme.colorScheme.secondary,
              ),
            ),
            Text(
              ' sana göre (${forYou.basedOn} uyumlu kişi)',
              style: theme.textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}
