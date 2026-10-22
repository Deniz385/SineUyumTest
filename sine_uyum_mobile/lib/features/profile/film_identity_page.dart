import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

import '../../data/models/profile.dart';
import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/state_views.dart';

final filmIdentityProvider = FutureProvider.autoDispose
    .family<FilmIdentity, String>(
      (ref, userId) => ref.watch(socialRepositoryProvider).identity(userId),
    );

/// Hands the card's PNG to the system share sheet (replaced in tests).
typedef ImageSharer = Future<void> Function(List<int> png, String text);

final imageSharerProvider = Provider<ImageSharer>(
  (ref) => (png, text) async {
    final dir = await getTemporaryDirectory();
    final file = File('${dir.path}/sineuyum-film-identity.png');
    await file.writeAsBytes(png, flush: true);
    await SharePlus.instance.share(
      ShareParams(
        files: [XFile(file.path, mimeType: 'image/png')],
        text: text,
      ),
    );
  },
);

/// Film identity card: the taste profile and title derived from the user's
/// ratings, shared as an image (Instagram, WhatsApp...). The title,
/// description and genre names come localized from the server.
class FilmIdentityPage extends ConsumerStatefulWidget {
  const FilmIdentityPage({
    super.key,
    required this.userId,
    this.isMine = false,
  });

  final String userId;
  final bool isMine;

  @override
  ConsumerState<FilmIdentityPage> createState() => _FilmIdentityPageState();
}

class _FilmIdentityPageState extends ConsumerState<FilmIdentityPage> {
  final _cardKey = GlobalKey();
  bool _sharing = false;

  Future<void> _share(FilmIdentity identity) async {
    final l10n = context.l10n;
    setState(() => _sharing = true);
    try {
      final boundary =
          _cardKey.currentContext!.findRenderObject()! as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 3);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      await ref.read(imageSharerProvider)(
        bytes!.buffer.asUint8List(),
        l10n.filmIdentityShareText(identity.title),
      );
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(l10n.shareImageFailed)));
      }
    } finally {
      if (mounted) setState(() => _sharing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final identity = ref.watch(filmIdentityProvider(widget.userId));

    return Scaffold(
      appBar: AppBar(title: Text(l10n.filmIdentity)),
      body: identity.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(filmIdentityProvider(widget.userId)),
        ),
        data: (data) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            RepaintBoundary(
              key: _cardKey,
              child: _IdentityCard(identity: data, isMine: widget.isMine),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: _sharing ? null : () => _share(data),
              icon: const Icon(Icons.ios_share),
              label: Text(l10n.shareAsImage),
            ),
          ],
        ),
      ),
    );
  }
}

class _IdentityCard extends StatelessWidget {
  const _IdentityCard({required this.identity, required this.isMine});

  final FilmIdentity identity;
  final bool isMine;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    const label = TextStyle(
      color: Color(0xFF9FB3FF),
      fontSize: 12,
      fontWeight: FontWeight.w600,
      letterSpacing: 1.2,
    );
    const body = TextStyle(color: Colors.white, fontSize: 15);
    final take = identity.contrarianTake;
    final favorite = identity.favorite;

    return Container(
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(18),
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF1B1038), Color(0xFF0D2A4A)],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(l10n.identityCardHeader, style: label),
          const SizedBox(height: 6),
          Text('@${identity.userName}', style: body),
          const SizedBox(height: 14),
          Text(
            identity.title,
            style: const TextStyle(
              color: Color(0xFFFFD54F),
              fontSize: 30,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            identity.titleDescription,
            style: const TextStyle(color: Color(0xFFE3E8FF), fontSize: 14),
          ),
          const SizedBox(height: 14),
          Text(
            l10n.identitySummary(
              identity.totalRatings,
              l10n.decimal(identity.averageRating),
            ),
            style: body.copyWith(fontWeight: FontWeight.w600),
          ),
          if (identity.topGenres.isNotEmpty) ...[
            const SizedBox(height: 16),
            Text(
              isMine
                  ? l10n.identityTopGenresMine
                  : l10n.identityTopGenresTheirs,
              style: label,
            ),
            for (final g in identity.topGenres)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Row(
                  children: [
                    SizedBox(width: 96, child: Text(g.name, style: body)),
                    Expanded(
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(4),
                        child: LinearProgressIndicator(
                          value: g.share / 100,
                          minHeight: 10,
                          backgroundColor: Colors.white24,
                          color: const Color(0xFF7C9CFF),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(l10n.percent(g.share), style: body),
                  ],
                ),
              ),
          ],
          if (take != null && take.tmdbAverage != null) ...[
            const SizedBox(height: 16),
            Text(
              isMine
                  ? l10n.identityContrarianMine
                  : l10n.identityContrarianTheirs,
              style: label,
            ),
            const SizedBox(height: 4),
            Text(
              (isMine
                  ? l10n.identityContrarianLineMine
                  : l10n.identityContrarianLineTheirs)(
                take.title,
                l10n.decimal(take.tmdbAverage!),
                take.rating,
              ),
              style: body,
            ),
          ],
          if (favorite != null) ...[
            const SizedBox(height: 12),
            Text(
              isMine ? l10n.identityFavoriteMine : l10n.identityFavoriteTheirs,
              style: label,
            ),
            const SizedBox(height: 4),
            Text(
              l10n.identityFavoriteLine(favorite.title, favorite.rating),
              style: body,
            ),
          ],
        ],
      ),
    );
  }
}
