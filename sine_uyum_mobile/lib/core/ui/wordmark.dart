import 'package:flutter/material.dart';

import '../../l10n/l10n.dart';
import '../theme/tokens.dart';

/// "SineUyum" in the display face with the marquee gradient.
class Wordmark extends StatelessWidget {
  const Wordmark({super.key, this.size = 28});

  final double size;

  @override
  Widget build(BuildContext context) => Semantics(
    label: context.l10n.appName,
    excludeSemantics: true,
    child: ShaderMask(
      blendMode: BlendMode.srcIn,
      shaderCallback: (bounds) => AppGradients.marquee.createShader(bounds),
      child: Text(
        context.l10n.appName,
        style: TextStyle(
          fontFamily: AppFonts.display,
          fontWeight: FontWeight.w800,
          fontSize: size,
          height: 1.1,
          color: Colors.white,
        ),
      ),
    ),
  );
}
