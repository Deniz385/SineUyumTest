import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';

import '../config/app_config.dart';
import '../theme/tokens.dart';

/// Network artwork switch: widget tests turn it off so nothing is
/// downloaded (the placeholders render instead).
abstract final class NetworkArtwork {
  static bool enabled = true;
}

/// A TMDb poster: always 2:3, `md` radius, a 1px inner hairline and a film
/// placeholder while loading or when there is no artwork.
///
/// [title] is announced by screen readers ("posters announce the title");
/// without it the image is decorative and hidden from them.
class PosterImage extends StatelessWidget {
  const PosterImage({
    super.key,
    required this.posterPath,
    this.title,
    this.width,
    this.height,
    this.size = 'w342',
    this.borderRadius = AppRadii.md,
    this.fit = BoxFit.cover,
  });

  final String? posterPath;
  final String? title;
  final double? width;

  /// Defaults to 1.5 × [width] (2:3).
  final double? height;
  final String size;
  final double borderRadius;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final w = width;
    final h = height ?? (w == null ? null : w * 1.5);
    final radius = BorderRadius.circular(borderRadius);
    final path = posterPath;

    Widget placeholder({bool icon = true}) => Container(
      width: w,
      height: h,
      color: colors.surfaceRaised,
      alignment: Alignment.center,
      child: icon
          ? Icon(
              Icons.movie_rounded,
              color: colors.textMuted,
              size: w == null ? 28 : (w * 0.28).clamp(16, 40),
            )
          : null,
    );

    Widget image = path == null || path.isEmpty || !NetworkArtwork.enabled
        ? placeholder()
        : CachedNetworkImage(
            imageUrl: '${AppConfig.tmdbImageBaseUrl}$size$path',
            width: w,
            height: h,
            fit: fit,
            fadeInDuration: AppMotion.of(context, AppMotion.standard),
            placeholder: (context, _) => placeholder(icon: false),
            errorWidget: (context, _, _) => placeholder(),
          );

    image = ClipRRect(
      borderRadius: radius,
      child: DecoratedBox(
        position: DecorationPosition.foreground,
        decoration: BoxDecoration(
          borderRadius: radius,
          border: Border.all(color: colors.posterHairline),
        ),
        child: image,
      ),
    );

    if (w == null && h == null) {
      image = AspectRatio(aspectRatio: 2 / 3, child: image);
    }

    final label = title;
    return label == null || label.isEmpty
        ? ExcludeSemantics(child: image)
        : Semantics(
            image: true,
            label: label,
            child: ExcludeSemantics(child: image),
          );
  }
}

/// A 16:9 TMDb backdrop (or any image path), decorative by default.
class BackdropImage extends StatelessWidget {
  const BackdropImage({
    super.key,
    required this.path,
    this.size = 'w780',
    this.fit = BoxFit.cover,
  });

  final String? path;
  final String size;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final p = path;
    final fallback = DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [colors.surfaceOverlay, colors.bg],
        ),
      ),
    );
    return ExcludeSemantics(
      child: p == null || p.isEmpty || !NetworkArtwork.enabled
          ? fallback
          : CachedNetworkImage(
              imageUrl: '${AppConfig.tmdbImageBaseUrl}$size$p',
              fit: fit,
              fadeInDuration: AppMotion.of(context, AppMotion.standard),
              placeholder: (_, _) => fallback,
              errorWidget: (_, _, _) => fallback,
            ),
    );
  }
}

/// User profile photo in a circle, or the username's initial when there is
/// none.
class UserAvatar extends StatelessWidget {
  const UserAvatar({
    super.key,
    required this.userName,
    this.imageUrl,
    this.radius = 20,
    this.borderColor,
  });

  final String userName;
  final String? imageUrl;
  final double radius;

  /// Ring around the avatar (e.g. the profile header over the cover).
  final Color? borderColor;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final initial = userName.isEmpty
        ? '?'
        : userName.characters.first.toUpperCase();

    final url = NetworkArtwork.enabled
        ? AppConfig.resolveMediaUrl(imageUrl)
        : null;
    final avatar = url == null
        ? CircleAvatar(
            radius: radius,
            backgroundColor: colors.surfaceOverlay,
            foregroundColor: colors.primary,
            child: Text(
              initial,
              style: TextStyle(
                fontFamily: AppFonts.display,
                fontWeight: FontWeight.w700,
                fontSize: radius * 0.85,
                color: colors.primary,
              ),
            ),
          )
        : CircleAvatar(
            radius: radius,
            backgroundColor: colors.surfaceOverlay,
            backgroundImage: CachedNetworkImageProvider(url),
            onBackgroundImageError: (_, _) {},
          );

    final border = borderColor;
    return ExcludeSemantics(
      child: border == null
          ? avatar
          : Container(
              padding: const EdgeInsets.all(3),
              decoration: BoxDecoration(color: border, shape: BoxShape.circle),
              child: avatar,
            ),
    );
  }
}
