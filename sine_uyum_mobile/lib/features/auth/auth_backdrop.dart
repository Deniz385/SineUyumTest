import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';

import '../../core/config/app_config.dart';
import '../../core/theme/tokens.dart';
import '../../core/ui/poster_image.dart';

/// The sign-in and sign-up background: a tilted wall of film posters under
/// a scrim (the lights going down). Decorative: hidden from screen readers.
class AuthBackdrop extends StatelessWidget {
  const AuthBackdrop({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Stack(
      fit: StackFit.expand,
      children: [
        const ExcludeSemantics(child: PosterWall()),
        DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              stops: const [0, 0.38, 0.62],
              colors: [
                colors.bg.withValues(alpha: colors.isDark ? 0.55 : 0.6),
                colors.bg.withValues(alpha: 0.92),
                colors.bg,
              ],
            ),
          ),
        ),
        child,
      ],
    );
  }
}

/// Posters of well-known films (public TMDb artwork; any that fail to
/// load leave their tinted tile).
const _wallPosters = [
  '/pB8BM7pdSp6B6Ih7QZ4DrQ3PmJK.jpg',
  '/3bhkrj58Vtu7enYsRolD1fZdja1.jpg',
  '/q6y0Go1tsGEsmtFryDOJo3dEmqu.jpg',
  '/qJ2tW6WMUDux911r6m7haRef0WH.jpg',
  '/gEU2QniE6E77NI6lCU6MxlNBvIx.jpg',
  '/7IiTTgloJzvGI1TAYymCfbfl3vT.jpg',
  '/39wmItIWsg5sZMyRUHLkWBcuVCM.jpg',
  '/d5iIlFn5s0ImszYzBPb8JPIfbXD.jpg',
  '/arw2vcBveWOVZr6pxd9XTd1TdQa.jpg',
  '/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg',
];

/// Tile tints: marquee gold, cinema red and deep night blues.
const _tints = [
  [Color(0xFF3A2A0C), Color(0xFF12100A)],
  [Color(0xFF3B1214), Color(0xFF120809)],
  [Color(0xFF0F2436), Color(0xFF080D14)],
  [Color(0xFF2B1A2E), Color(0xFF0E0A10)],
  [Color(0xFF14302A), Color(0xFF070F0D)],
];

class PosterWall extends StatelessWidget {
  const PosterWall({super.key});

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.sizeOf(context);
    final tile = (size.width / 3.4).clamp(90.0, 180.0);
    const gap = 10.0;
    final columns = (size.width * 1.6 / (tile + gap)).ceil();
    final rows = (size.height * 1.4 / (tile * 1.5 + gap)).ceil();

    Widget poster(int i) {
      final tint = _tints[i % _tints.length];
      final path = _wallPosters[(i * 7) % _wallPosters.length];
      return ClipRRect(
        borderRadius: BorderRadius.circular(AppRadii.md),
        child: SizedBox(
          width: tile,
          height: tile * 1.5,
          child: Stack(
            fit: StackFit.expand,
            children: [
              DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: tint,
                  ),
                ),
              ),
              if (NetworkArtwork.enabled)
                CachedNetworkImage(
                  imageUrl: '${AppConfig.tmdbImageBaseUrl}w185$path',
                  fit: BoxFit.cover,
                  fadeInDuration: AppMotion.of(context, AppMotion.emphasized),
                  errorWidget: (_, _, _) => const SizedBox.shrink(),
                ),
            ],
          ),
        ),
      );
    }

    return ClipRect(
      child: OverflowBox(
        maxWidth: double.infinity,
        maxHeight: double.infinity,
        child: Transform.rotate(
          angle: -0.18,
          child: Row(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (var c = 0; c < columns; c++)
                Padding(
                  // Staggered columns, like a theatre lobby wall.
                  padding: EdgeInsets.only(
                    top: c.isEven ? 0 : tile * 0.6,
                    right: gap,
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      for (var r = 0; r < rows; r++)
                        Padding(
                          padding: const EdgeInsets.only(bottom: gap),
                          child: poster(c * rows + r),
                        ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
