import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/skeleton.dart';
import '../../core/ui/state_views.dart';
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
        padding: EdgeInsets.only(top: AppSpace.xl),
        child: Shimmer(child: SkeletonFeedCard()),
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
            SectionHeader(
              title: data.totalCount > 0
                  ? l10n.reviewsTitleWithCount(data.totalCount)
                  : l10n.reviewsTitle,
              padding: const EdgeInsets.only(
                top: AppSpace.xl,
                bottom: AppSpace.xs,
              ),
            ),
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
                  alignLabelWithHint: true,
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
                child: Text(
                  l10n.reviewsNoneYet,
                  style: theme.textTheme.bodySmall,
                ),
              )
            else
              for (final review in data.reviews)
                Padding(
                  padding: const EdgeInsets.only(bottom: AppSpace.sm),
                  child: ReviewCard(review: review),
                ),
          ],
        );
      },
    );
  }
}

/// The "for you" score (from people who match your taste) as a score tile;
/// nothing without enough data. It comes with the reviews request (no
/// extra call).
class ForYouBadge extends ConsumerWidget {
  const ForYouBadge({super.key, required this.movieId});

  final int movieId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final forYou = ref.watch(movieReviewsProvider(movieId)).value?.forYou;
    if (forYou == null) return const SizedBox.shrink();
    final l10n = context.l10n;
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
      child: ScoreTile(
        label: l10n.forYouLabel,
        score: forYou.score,
        caption: l10n.forYouBasedOn(forYou.basedOn),
        highlight: true,
      ),
    );
  }
}

/// A score out of 10 with a label above and a caption below.
class ScoreTile extends StatelessWidget {
  const ScoreTile({
    super.key,
    required this.label,
    required this.score,
    this.caption,
    this.highlight = false,
    this.capitals = true,
  });

  final String label;
  final double score;
  final String? caption;
  final bool highlight;

  /// Overline capitals; off for names spelled their own way ("TMDb").
  final bool capitals;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final number = context.type.number;
    return Semantics(
      label: [
        label,
        l10n.scoreSpoken(l10n.decimal(score)),
        ?caption,
      ].join(', '),
      excludeSemantics: true,
      child: Container(
        padding: const EdgeInsets.all(AppSpace.sm),
        decoration: BoxDecoration(
          color: highlight
              ? colors.primary.withValues(alpha: colors.isDark ? 0.12 : 0.08)
              : colors.surface,
          borderRadius: BorderRadius.circular(AppRadii.md),
          border: Border.all(
            color: highlight
                ? colors.primary.withValues(alpha: 0.5)
                : colors.border,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              capitals ? l10n.upper(label) : label,
              style: context.type.overline.copyWith(
                color: highlight ? colors.primary : colors.textSecondary,
              ),
            ),
            const SizedBox(height: 2),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: AlignmentDirectional.centerStart,
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.baseline,
                textBaseline: TextBaseline.alphabetic,
                children: [
                  Text(
                    l10n.decimal(score),
                    style: number.copyWith(fontSize: 28, height: 1.2),
                  ),
                  Text(
                    '/10',
                    style: number.copyWith(
                      fontSize: 14,
                      color: colors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            if (caption != null)
              Text(
                caption!,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.labelMedium,
              ),
          ],
        ),
      ),
    );
  }
}
