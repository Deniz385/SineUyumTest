import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/social.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../providers/social_provider.dart';
import 'review_card.dart';

/// Reviews on the movie page: write/edit/delete your own review, and read
/// other people's.
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
    // The draft is filled when the review changes, not on every load.
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
      if (mounted) _show(context.l10n.reviewSaved);
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
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final reviews = ref.watch(movieReviewsProvider(widget.movieId));

    return reviews.when(
      loading: () => const Padding(
        padding: EdgeInsets.all(24),
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: Text(l10n.reviewsLoadFailed(error.toString())),
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
                  ? l10n.reviewsTitleWithCount(data.totalCount)
                  : l10n.reviewsTitle,
              style: theme.textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (data.myRating == null)
              Card(
                child: ListTile(
                  leading: const Icon(Icons.info_outline_rounded),
                  title: Text(l10n.reviewRateFirst),
                ),
              )
            else if (_editing) ...[
              TextField(
                controller: _controller,
                minLines: 3,
                maxLines: 8,
                maxLength: 2000,
                decoration: InputDecoration(
                  labelText: l10n.reviewFieldLabel(data.myRating!),
                  border: const OutlineInputBorder(),
                ),
              ),
              Row(
                children: [
                  Expanded(
                    child: CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      controlAffinity: ListTileControlAffinity.leading,
                      title: Text(l10n.containsSpoilers),
                      value: _spoilers,
                      onChanged: (v) => setState(() => _spoilers = v ?? false),
                    ),
                  ),
                  if (mine != null)
                    TextButton(
                      onPressed: () => setState(() => _editing = false),
                      child: Text(l10n.commonCancel),
                    ),
                  FilledButton(
                    onPressed: _saving ? null : _save,
                    child: Text(
                      mine == null ? l10n.reviewPost : l10n.reviewUpdate,
                    ),
                  ),
                ],
              ),
            ] else if (mine != null) ...[
              ReviewCard(review: mine),
              Row(
                children: [
                  TextButton(
                    onPressed: () => setState(() => _editing = true),
                    child: Text(l10n.commonEdit),
                  ),
                  TextButton(
                    onPressed: _delete,
                    child: Text(
                      l10n.commonDelete,
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 8),
            if (data.reviews.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Text(l10n.reviewsNoneYet),
              )
            else
              for (final review in data.reviews) ReviewCard(review: review),
          ],
        );
      },
    );
  }
}

/// The "for you" score next to the movie title; nothing without enough data.
/// It comes with the reviews request (no extra call).
class ForYouBadge extends ConsumerWidget {
  const ForYouBadge({super.key, required this.movieId});

  final int movieId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final forYou = ref.watch(movieReviewsProvider(movieId)).value?.forYou;
    if (forYou == null) return const SizedBox.shrink();
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final who = forYou.topMatches
        .map(
          (m) => l10n.forYouMatchEntry(
            m.author.userName,
            l10n.percent(m.compatibility),
            m.rating,
          ),
        )
        .join(', ');

    return Tooltip(
      message: l10n.forYouTooltip(forYou.basedOn, who),
      triggerMode: TooltipTriggerMode.tap,
      child: Padding(
        padding: const EdgeInsets.only(top: 6),
        child: Row(
          children: [
            Icon(
              Icons.favorite_rounded,
              size: 16,
              color: theme.colorScheme.secondary,
            ),
            const SizedBox(width: 4),
            Text(
              l10n.decimal(forYou.score),
              style: theme.textTheme.titleSmall?.copyWith(
                color: theme.colorScheme.secondary,
              ),
            ),
            Text(
              l10n.forYouSuffix(forYou.basedOn),
              style: theme.textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}
