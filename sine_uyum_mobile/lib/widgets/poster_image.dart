import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';

import '../core/config/app_config.dart';

/// TMDb poster image. Shows a placeholder when `posterPath` is null.
class PosterImage extends StatelessWidget {
  const PosterImage({
    super.key,
    required this.posterPath,
    this.width,
    this.height,
    this.size = 'w342',
    this.borderRadius = 10,
  });

  final String? posterPath;
  final double? width;
  final double? height;
  final String size;
  final double borderRadius;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(borderRadius);

    if (posterPath == null || posterPath!.isEmpty) {
      return ClipRRect(
        borderRadius: radius,
        child: Container(
          width: width,
          height: height,
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          alignment: Alignment.center,
          child: Icon(
            Icons.movie_outlined,
            color: Theme.of(context).colorScheme.onSurfaceVariant,
          ),
        ),
      );
    }

    return ClipRRect(
      borderRadius: radius,
      child: CachedNetworkImage(
        imageUrl: '${AppConfig.tmdbImageBaseUrl}$size$posterPath',
        width: width,
        height: height,
        fit: BoxFit.cover,
        placeholder: (context, _) => Container(
          width: width,
          height: height,
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
        ),
        errorWidget: (context, _, _) => Container(
          width: width,
          height: height,
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          alignment: Alignment.center,
          child: const Icon(Icons.broken_image_outlined),
        ),
      ),
    );
  }
}

/// User profile photo, or the username's initial when there is none.
class UserAvatar extends StatelessWidget {
  const UserAvatar({
    super.key,
    required this.userName,
    this.imageUrl,
    this.radius = 20,
  });

  final String userName;
  final String? imageUrl;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final initial = userName.isEmpty
        ? '?'
        : userName.characters.first.toUpperCase();

    final url = AppConfig.resolveMediaUrl(imageUrl);
    if (url == null) {
      return CircleAvatar(
        radius: radius,
        child: Text(initial, style: TextStyle(fontSize: radius * 0.8)),
      );
    }

    return CircleAvatar(
      radius: radius,
      backgroundImage: CachedNetworkImageProvider(url),
      onBackgroundImageError: (_, _) {},
      child: null,
    );
  }
}
