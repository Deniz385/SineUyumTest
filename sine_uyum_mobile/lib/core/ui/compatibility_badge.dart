import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../l10n/l10n.dart';
import '../theme/tokens.dart';

/// The compatibility percentage in the scale colour (< 50 accent, 50–74
/// primary, ≥ 75 success), always with the number — never colour alone.
class CompatibilityBadge extends StatelessWidget {
  const CompatibilityBadge({
    super.key,
    required this.score,
    this.commonMovieCount,
    this.ring = false,
    this.ringSize = 56,
  });

  final double score;
  final int? commonMovieCount;

  /// A ring with the number inside (profile headers) instead of a pill.
  final bool ring;
  final double ringSize;

  /// The scale colour for [score] in the current theme.
  static Color colorFor(BuildContext context, double score) =>
      context.colors.compatibility(score);

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final color = colors.compatibility(score);
    final percent = l10n.percent(score, digits: score % 1 == 0 ? 0 : 1);
    final common = commonMovieCount;
    final spoken = [
      l10n.compatibilityMatch(percent),
      if (common != null && common > 0) l10n.commonMoviesShort(common),
    ].join(' ');

    if (ring) {
      return Semantics(
        label: spoken,
        excludeSemantics: true,
        child: SizedBox.square(
          dimension: ringSize,
          child: CustomPaint(
            painter: _RingPainter(
              value: (score / 100).clamp(0.0, 1.0),
              color: color,
              track: colors.border,
            ),
            child: Center(
              child: FittedBox(
                child: Padding(
                  padding: const EdgeInsets.all(8),
                  child: Text(
                    percent,
                    style: context.type.number.copyWith(
                      fontSize: ringSize * 0.26,
                      color: colors.text,
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      );
    }

    return Semantics(
      label: spoken,
      excludeSemantics: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.14),
          borderRadius: BorderRadius.circular(AppRadii.pill),
          border: Border.all(color: color.withValues(alpha: 0.55)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.favorite_rounded, size: 14, color: color),
            const SizedBox(width: 4),
            Text(
              percent,
              style: context.type.number.copyWith(fontSize: 14, color: color),
            ),
            if (common != null && common > 0) ...[
              const SizedBox(width: 6),
              Flexible(
                child: Text(
                  l10n.commonMoviesShort(common),
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelMedium,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter({required this.value, required this.color, required this.track});

  final double value;
  final Color color;
  final Color track;

  @override
  void paint(Canvas canvas, Size size) {
    const stroke = 4.0;
    final rect = Offset.zero & size;
    final arc = rect.deflate(stroke / 2);
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round;
    canvas.drawArc(arc, 0, math.pi * 2, false, paint..color = track);
    canvas.drawArc(
      arc,
      -math.pi / 2,
      math.pi * 2 * value,
      false,
      paint..color = color,
    );
  }

  @override
  bool shouldRepaint(_RingPainter old) =>
      old.value != value || old.color != color || old.track != track;
}
