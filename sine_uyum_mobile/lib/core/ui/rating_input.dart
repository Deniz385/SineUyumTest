import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../l10n/l10n.dart';
import '../theme/tokens.dart';

/// The 10-point rating shown as five stars with halves (2 points a star).
/// Tap or drag across the stars, or use the arrow keys; the number is always
/// shown next to them. Screen readers hear "8 out of 10" and can adjust it.
class RatingInput extends StatefulWidget {
  const RatingInput({
    super.key,
    required this.value,
    required this.onChanged,
    this.starSize = 40,
    this.showNumber = true,
  });

  /// 1–10, or null when not rated yet.
  final int? value;
  final ValueChanged<int>? onChanged;
  final double starSize;
  final bool showNumber;

  /// The rating for a touch at [dx] across stars [width] wide: each half
  /// star is one point, rounded up so touching a star's left half gives the
  /// half.
  static int valueAt(double dx, double width) {
    if (width <= 0) return 1;
    return ((dx / width) * 10).ceil().clamp(1, 10);
  }

  @override
  State<RatingInput> createState() => _RatingInputState();
}

class _RatingInputState extends State<RatingInput> {
  final _focusNode = FocusNode(debugLabel: 'RatingInput');
  bool _focused = false;

  @override
  void dispose() {
    _focusNode.dispose();
    super.dispose();
  }

  bool get _enabled => widget.onChanged != null;

  void _set(int value) {
    final next = value.clamp(1, 10);
    if (next != widget.value) {
      HapticFeedback.selectionClick();
      widget.onChanged?.call(next);
    }
  }

  void _step(int delta) => _set((widget.value ?? 0) + delta);

  double get _starsWidth => widget.starSize * 5;

  void _handle(Offset local) {
    if (!_enabled) return;
    final dx = Directionality.of(context) == TextDirection.rtl
        ? _starsWidth - local.dx
        : local.dx;
    _set(RatingInput.valueAt(dx, _starsWidth));
  }

  KeyEventResult _onKey(FocusNode node, KeyEvent event) {
    if (event is! KeyDownEvent && event is! KeyRepeatEvent) {
      return KeyEventResult.ignored;
    }
    final key = event.logicalKey;
    if (key == LogicalKeyboardKey.arrowRight ||
        key == LogicalKeyboardKey.arrowUp) {
      _step(1);
      return KeyEventResult.handled;
    }
    if (key == LogicalKeyboardKey.arrowLeft ||
        key == LogicalKeyboardKey.arrowDown) {
      _step(-1);
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final value = widget.value;

    final stars = GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTapDown: (d) => _handle(d.localPosition),
      onHorizontalDragStart: (d) => _handle(d.localPosition),
      onHorizontalDragUpdate: (d) => _handle(d.localPosition),
      child: SizedBox(
        width: _starsWidth,
        height: widget.starSize < 48 ? 48 : widget.starSize,
        child: Center(
          child: RatingStars(
            value: value ?? 0,
            size: widget.starSize,
            color: colors.primary,
            emptyColor: colors.border,
          ),
        ),
      ),
    );

    return Semantics(
      slider: true,
      focusable: _enabled,
      enabled: _enabled,
      label: l10n.ratingInputLabel,
      value: value == null ? l10n.ratingNotRated : l10n.ratingSpoken(value),
      increasedValue: l10n.ratingSpoken(((value ?? 0) + 1).clamp(1, 10)),
      decreasedValue: l10n.ratingSpoken(((value ?? 2) - 1).clamp(1, 10)),
      onIncrease: _enabled ? () => _step(1) : null,
      onDecrease: _enabled ? () => _step(-1) : null,
      child: ExcludeSemantics(
        child: Focus(
          focusNode: _focusNode,
          canRequestFocus: _enabled,
          onKeyEvent: _onKey,
          onFocusChange: (focused) => setState(() => _focused = focused),
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(AppRadii.sm),
              border: Border.all(
                color: _focused ? colors.primary : Colors.transparent,
                width: 2,
              ),
            ),
            child: Padding(
              padding: const EdgeInsets.all(2),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  stars,
                  if (widget.showNumber) ...[
                    const SizedBox(width: AppSpace.sm),
                    _RatingNumber(value: value, size: widget.starSize),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _RatingNumber extends StatelessWidget {
  const _RatingNumber({required this.value, required this.size});

  final int? value;
  final double size;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final number = context.type.number;
    return Row(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.baseline,
      textBaseline: TextBaseline.alphabetic,
      children: [
        Text(
          value == null ? '–' : '$value',
          style: number.copyWith(fontSize: size * 0.8, color: colors.text),
        ),
        Text(
          '/10',
          style: number.copyWith(
            fontSize: size * 0.4,
            color: colors.textSecondary,
          ),
        ),
      ],
    );
  }
}

/// Read-only stars for a 0–10 [value] (halves for odd values).
class RatingStars extends StatelessWidget {
  const RatingStars({
    super.key,
    required this.value,
    this.size = 16,
    this.color,
    this.emptyColor,
  });

  final num value;
  final double size;
  final Color? color;
  final Color? emptyColor;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final full = color ?? colors.primary;
    final empty = emptyColor ?? colors.border;
    final halves = value.clamp(0, 10).round();
    return ExcludeSemantics(
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (var star = 0; star < 5; star++)
            Icon(
              halves >= (star + 1) * 2
                  ? Icons.star_rounded
                  : halves == star * 2 + 1
                  ? Icons.star_half_rounded
                  : Icons.star_outline_rounded,
              size: size,
              color: halves > star * 2 ? full : empty,
            ),
        ],
      ),
    );
  }
}
