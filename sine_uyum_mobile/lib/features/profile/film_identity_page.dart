import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

import '../../data/models/profile.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/state_views.dart';

final filmIdentityProvider = FutureProvider.autoDispose
    .family<FilmIdentity, String>(
      (ref, userId) => ref.watch(socialRepositoryProvider).identity(userId),
    );

/// Kartın PNG'sini sistem paylaşım menüsüne verir (testte değiştirilir).
typedef ImageSharer = Future<void> Function(List<int> png, String text);

final imageSharerProvider = Provider<ImageSharer>(
  (ref) => (png, text) async {
    final dir = await getTemporaryDirectory();
    final file = File('${dir.path}/sineuyum-film-kimligi.png');
    await file.writeAsBytes(png, flush: true);
    await SharePlus.instance.share(
      ShareParams(
        files: [XFile(file.path, mimeType: 'image/png')],
        text: text,
      ),
    );
  },
);

String _fmt(double n) => n.toStringAsFixed(1).replaceAll('.', ',');

/// Film kimliği kartı: puanlardan çıkan zevk profili ve unvan; görsel olarak
/// paylaşılır (Instagram, WhatsApp...).
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
    setState(() => _sharing = true);
    try {
      final boundary =
          _cardKey.currentContext!.findRenderObject()! as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 3);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      await ref.read(imageSharerProvider)(
        bytes!.buffer.asUint8List(),
        '${identity.title} — SineUyum film kimliğim',
      );
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(const SnackBar(content: Text('Görsel paylaşılamadı.')));
      }
    } finally {
      if (mounted) setState(() => _sharing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final identity = ref.watch(filmIdentityProvider(widget.userId));

    return Scaffold(
      appBar: AppBar(title: const Text('Film kimliği')),
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
              label: const Text('Görsel olarak paylaş'),
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
          Text('SİNEUYUM · FİLM KİMLİĞİ', style: label),
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
            '${identity.totalRatings} film · ortalama ${_fmt(identity.averageRating)}',
            style: body.copyWith(fontWeight: FontWeight.w600),
          ),
          if (identity.topGenres.isNotEmpty) ...[
            const SizedBox(height: 16),
            Text(
              isMine ? 'EN ÇOK İZLEDİĞİN TÜRLER' : 'EN ÇOK İZLEDİĞİ TÜRLER',
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
                    Text('%${g.share}', style: body),
                  ],
                ),
              ),
          ],
          if (take != null && take.tmdbAverage != null) ...[
            const SizedBox(height: 16),
            Text(isMine ? 'EN AYKIRI PUANIN' : 'EN AYKIRI PUANI', style: label),
            const SizedBox(height: 4),
            Text(
              '${take.title}: herkes ${_fmt(take.tmdbAverage!)}, '
              '${isMine ? 'sen' : 'o'} ${take.rating}',
              style: body,
            ),
          ],
          if (favorite != null) ...[
            const SizedBox(height: 12),
            Text(isMine ? 'FAVORİN' : 'FAVORİSİ', style: label),
            const SizedBox(height: 4),
            Text('${favorite.title} · ${favorite.rating}/10', style: body),
          ],
        ],
      ),
    );
  }
}
