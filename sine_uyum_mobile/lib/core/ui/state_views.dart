import 'package:flutter/material.dart';

import '../../l10n/l10n.dart';
import '../theme/tokens.dart';
import 'skeleton.dart';

export 'compatibility_badge.dart';

/// First-load placeholder for content areas: skeleton rows, never a bare
/// spinner.
class LoadingView extends StatelessWidget {
  const LoadingView({super.key, this.rows = 6, this.posters = false});

  final int rows;

  /// Rows with a poster instead of an avatar.
  final bool posters;

  @override
  Widget build(BuildContext context) => Align(
    alignment: Alignment.topCenter,
    child: Shimmer(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpace.xs),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < rows; i++) SkeletonListTile(poster: posters),
          ],
        ),
      ),
    ),
  );
}

/// The spec's EmptyState / ErrorState names.
typedef EmptyState = EmptyView;
typedef ErrorState = ErrorView;

/// A failure: what went wrong and "Try again".
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return _StateLayout(
      icon: Icons.error_outline_rounded,
      iconColor: colors.error,
      title: message,
      action: onRetry == null
          ? null
          : OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh_rounded),
              label: Text(context.l10n.commonRetry),
            ),
    );
  }
}

/// Nothing here (yet): an icon, a one-line title, an optional one-line hint
/// and an optional action.
class EmptyView extends StatelessWidget {
  const EmptyView({
    super.key,
    required this.message,
    this.icon = Icons.inbox_rounded,
    this.hint,
    this.action,
  });

  final String message;
  final IconData icon;
  final String? hint;
  final Widget? action;

  @override
  Widget build(BuildContext context) => _StateLayout(
    icon: icon,
    iconColor: context.colors.textSecondary,
    title: message,
    hint: hint,
    action: action,
  );
}

class _StateLayout extends StatelessWidget {
  const _StateLayout({
    required this.icon,
    required this.iconColor,
    required this.title,
    this.hint,
    this.action,
  });

  final IconData icon;
  final Color iconColor;
  final String title;
  final String? hint;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpace.xl,
          vertical: AppSpace.xxl,
        ),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 64,
                height: 64,
                decoration: BoxDecoration(
                  color: colors.surfaceRaised,
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, size: 30, color: iconColor),
              ),
              const SizedBox(height: AppSpace.md),
              Text(
                title,
                textAlign: TextAlign.center,
                style: theme.textTheme.titleMedium,
              ),
              if (hint != null) ...[
                const SizedBox(height: AppSpace.xxs),
                Text(
                  hint!,
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodySmall,
                ),
              ],
              if (action != null) ...[
                const SizedBox(height: AppSpace.lg),
                action!,
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// A section title with an optional "See all".
class SectionHeader extends StatelessWidget {
  const SectionHeader({
    super.key,
    required this.title,
    this.onSeeAll,
    this.padding = const EdgeInsets.fromLTRB(
      AppSpace.gutter,
      AppSpace.xl,
      AppSpace.xs,
      AppSpace.xs,
    ),
  });

  final String title;
  final VoidCallback? onSeeAll;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Padding(
      padding: padding,
      child: Row(
        children: [
          Expanded(
            child: Semantics(
              header: true,
              child: Text(title, style: context.type.h2),
            ),
          ),
          if (onSeeAll != null)
            TextButton(
              onPressed: onSeeAll,
              child: Text(
                l10n.seeAll,
                semanticsLabel: l10n.seeAllSemantics(title),
              ),
            ),
        ],
      ),
    );
  }
}
