import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';

/// Follow / Following toggle for one person. The change shows at once and
/// is rolled back (with the server's message) when it fails.
class FollowButton extends ConsumerStatefulWidget {
  const FollowButton({
    super.key,
    required this.userId,
    required this.userName,
    this.initiallyFollowing = false,
    this.onChanged,
  });

  final String userId;
  final String userName;
  final bool initiallyFollowing;
  final ValueChanged<bool>? onChanged;

  @override
  ConsumerState<FollowButton> createState() => _FollowButtonState();
}

class _FollowButtonState extends ConsumerState<FollowButton> {
  late bool _following = widget.initiallyFollowing;
  bool _busy = false;

  Future<void> _toggle() async {
    final messenger = ScaffoldMessenger.of(context);
    final next = !_following;
    setState(() {
      _following = next;
      _busy = true;
    });
    try {
      final repository = ref.read(socialRepositoryProvider);
      if (next) {
        await repository.follow(widget.userId);
      } else {
        await repository.unfollow(widget.userId);
      }
      ref.invalidate(followStatusProvider(widget.userId));
      widget.onChanged?.call(next);
    } catch (error) {
      if (mounted) setState(() => _following = !next);
      messenger.showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final onPressed = _busy ? null : _toggle;
    return _following
        ? OutlinedButton(
            onPressed: onPressed,
            child: Text(
              l10n.followingState,
              semanticsLabel: l10n.unfollowSemantics(widget.userName),
            ),
          )
        : FilledButton(
            onPressed: onPressed,
            child: Text(
              l10n.commonFollow,
              semanticsLabel: l10n.followSemantics(widget.userName),
            ),
          );
  }
}
