import 'package:flutter/material.dart';

import '../../l10n/l10n.dart';
import '../theme/tokens.dart';

/// Shimmering placeholder content shown on first load instead of a bare
/// spinner. Under reduced motion the shimmer stops (a still placeholder).
///
/// One [Shimmer] animates every [SkeletonBox] below it.
class Shimmer extends StatefulWidget {
  const Shimmer({super.key, required this.child});

  final Widget child;

  @override
  State<Shimmer> createState() => _ShimmerState();
}

class _ShimmerState extends State<Shimmer> with SingleTickerProviderStateMixin {
  late final _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1400),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (AppMotion.reduced(context)) {
      _controller.stop();
      _controller.value = 0.5;
    } else if (!_controller.isAnimating) {
      _controller.repeat();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final base = colors.surfaceRaised;
    final highlight = colors.isDark
        ? colors.surfaceOverlay
        : Color.alphaBlend(const Color(0x66FFFFFF), base);
    return Semantics(
      label: context.l10n.loadingLabel,
      excludeSemantics: true,
      child: AnimatedBuilder(
        animation: _controller,
        child: widget.child,
        builder: (context, child) {
          final t = _controller.value;
          return ShaderMask(
            blendMode: BlendMode.srcATop,
            shaderCallback: (bounds) => LinearGradient(
              begin: Alignment(-1.5 + 3 * t, -0.3),
              end: Alignment(-0.5 + 3 * t, 0.3),
              colors: [base, highlight, base],
            ).createShader(bounds),
            child: child,
          );
        },
      ),
    );
  }
}

/// A rounded block in the skeleton colour.
class SkeletonBox extends StatelessWidget {
  const SkeletonBox({
    super.key,
    this.width,
    this.height = 14,
    this.radius = AppRadii.sm,
    this.circle = false,
  });

  final double? width;
  final double height;
  final double radius;
  final bool circle;

  @override
  Widget build(BuildContext context) => Container(
    width: circle ? height : width,
    height: height,
    decoration: BoxDecoration(
      color: context.colors.surfaceRaised,
      shape: circle ? BoxShape.circle : BoxShape.rectangle,
      borderRadius: circle ? null : BorderRadius.circular(radius),
    ),
  );
}

/// A list row placeholder: avatar or poster, two lines of text.
class SkeletonListTile extends StatelessWidget {
  const SkeletonListTile({super.key, this.poster = false});

  final bool poster;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(
      horizontal: AppSpace.gutter,
      vertical: 10,
    ),
    child: Row(
      children: [
        poster
            ? const SkeletonBox(width: 44, height: 66)
            : const SkeletonBox(height: 44, circle: true),
        const SizedBox(width: AppSpace.md),
        const Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              FractionallySizedBox(widthFactor: 0.6, child: SkeletonBox()),
              SizedBox(height: 8),
              FractionallySizedBox(
                widthFactor: 0.9,
                child: SkeletonBox(height: 12),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}

/// A horizontal row of poster placeholders (for [MovieRow]).
class SkeletonPosterRow extends StatelessWidget {
  const SkeletonPosterRow({super.key, this.posterWidth = 140, this.count = 4});

  final double posterWidth;
  final int count;

  @override
  Widget build(BuildContext context) => Shimmer(
    child: SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.symmetric(horizontal: AppSpace.gutter),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (var i = 0; i < count; i++) ...[
            if (i > 0) const SizedBox(width: AppSpace.sm),
            SizedBox(
              width: posterWidth,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SkeletonBox(
                    width: posterWidth,
                    height: posterWidth * 1.5,
                    radius: AppRadii.md,
                  ),
                  const SizedBox(height: 8),
                  SkeletonBox(width: posterWidth * 0.8, height: 12),
                ],
              ),
            ),
          ],
        ],
      ),
    ),
  );
}

/// A feed card placeholder: author row, then a poster beside text lines.
class SkeletonFeedCard extends StatelessWidget {
  const SkeletonFeedCard({super.key});

  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.only(bottom: AppSpace.sm),
    padding: const EdgeInsets.all(AppSpace.md),
    decoration: BoxDecoration(
      color: context.colors.surface,
      borderRadius: BorderRadius.circular(AppRadii.md),
    ),
    child: const Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SkeletonBox(width: 72, height: 108, radius: AppRadii.md),
        SizedBox(width: AppSpace.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  SkeletonBox(height: 28, circle: true),
                  SizedBox(width: 8),
                  Expanded(child: SkeletonBox(height: 12)),
                ],
              ),
              SizedBox(height: 14),
              FractionallySizedBox(widthFactor: 0.7, child: SkeletonBox()),
              SizedBox(height: 8),
              SkeletonBox(height: 12),
              SizedBox(height: 6),
              FractionallySizedBox(
                widthFactor: 0.5,
                child: SkeletonBox(height: 12),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}

/// A grid of poster placeholders.
class SkeletonPosterGrid extends StatelessWidget {
  const SkeletonPosterGrid({super.key, this.count = 9, this.columns = 3});

  final int count;
  final int columns;

  @override
  Widget build(BuildContext context) => Shimmer(
    child: GridView.count(
      crossAxisCount: columns,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.all(AppSpace.gutter),
      mainAxisSpacing: AppSpace.sm,
      crossAxisSpacing: AppSpace.sm,
      childAspectRatio: 2 / 3,
      children: [
        for (var i = 0; i < count; i++)
          const SkeletonBox(height: double.infinity, radius: AppRadii.md),
      ],
    ),
  );
}
