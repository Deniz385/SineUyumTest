import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_theme.dart';
import '../../data/models/movie.dart';
import '../../data/models/profile.dart';
import '../../data/models/year_in_review.dart';
import '../../l10n/l10n.dart';
import '../../providers/year_in_review_provider.dart';
import '../../widgets/poster_image.dart';
import '../profile/film_identity_page.dart' show imageSharerProvider;
import 'year_in_review_share_card.dart';

const _gold = Color(0xFFFFD54F);
const _soft = Color(0xFFE3E8FF);

/// The slides of a recap, in order. Slides without data are skipped.
enum RecapSlide {
  intro,
  totals,
  rhythm,
  genres,
  topMovies,
  contrarian,
  bookends,
  rating,
  tasteMate,
  finale;

  /// Background gradient of each slide (dark, so white text stays legible).
  List<Color> get colors => switch (this) {
    intro => const [Color(0xFF1B1038), Color(0xFF0D2A4A)],
    totals => const [Color(0xFF2A0845), Color(0xFF6441A5)],
    rhythm => const [Color(0xFF0F2027), Color(0xFF2C5364)],
    genres => const [Color(0xFF3A1C71), Color(0xFF9E4A6C)],
    topMovies => const [Color(0xFF141E30), Color(0xFF243B55)],
    contrarian => const [Color(0xFF42275A), Color(0xFF734B6D)],
    bookends => const [Color(0xFF16222A), Color(0xFF3A6073)],
    rating => const [Color(0xFF1F1C2C), Color(0xFF5C5884)],
    tasteMate => const [Color(0xFF2C3E50), Color(0xFF9C4A5B)],
    finale => const [Color(0xFF1B1038), Color(0xFF6A2BB8)],
  };

  static List<RecapSlide> forRecap(YearInReview recap) {
    final t = recap.totals;
    final take = recap.identity.contrarianTake;
    return [
      intro,
      if (t.diaryEntries > 0 ||
          t.rewatches > 0 ||
          t.ratings > 0 ||
          t.reviews > 0 ||
          t.likesReceived > 0 ||
          t.commentsReceived > 0)
        totals,
      if (recap.busiestMonth != null || recap.monthCounts.any((c) => c > 0))
        rhythm,
      if (recap.identity.topGenres.isNotEmpty) genres,
      if (recap.topMovies.isNotEmpty) topMovies,
      if (take != null && take.tmdbAverage != null) contrarian,
      if (recap.firstMovie != null ||
          recap.lastMovie != null ||
          recap.mostRewatched != null)
        bookends,
      if (recap.averageRating != null) rating,
      if (recap.tasteMate != null) tasteMate,
      finale,
    ];
  }
}

/// "Year in review": a user's year in movies as full-screen story slides
/// (tap right / left to move, long-press to pause, auto-advance), ending
/// with a shareable story image.
class YearInReviewPage extends ConsumerStatefulWidget {
  const YearInReviewPage({super.key, required this.userId, this.year});

  final String userId;

  /// Null: the current year so far.
  final int? year;

  @override
  ConsumerState<YearInReviewPage> createState() => _YearInReviewPageState();
}

class _YearInReviewPageState extends ConsumerState<YearInReviewPage> {
  late int? _year = widget.year;

  YearInReviewKey get _key => (userId: widget.userId, year: _year);

  /// A private profile (403) or a block (404): the server explains which.
  static bool _isAccessDenied(Object error) =>
      error is ApiException &&
      (error.statusCode == 403 || error.statusCode == 404);

  void _selectYear(int year) => setState(() => _year = year);

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final recap = ref.watch(yearInReviewProvider(_key));

    return Theme(
      data: AppTheme.dark,
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.light,
        child: Scaffold(
          backgroundColor: Colors.black,
          body: recap.when(
            skipLoadingOnReload: true,
            loading: () => const _Frame(
              colors: [Color(0xFF1B1038), Color(0xFF0D2A4A)],
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => _Frame(
              colors: const [Color(0xFF1B1038), Color(0xFF0D2A4A)],
              child: _Message(
                icon: _isAccessDenied(error)
                    ? Icons.lock_outline
                    : Icons.error_outline,
                message: error.toString(),
                action: _isAccessDenied(error)
                    ? null
                    : OutlinedButton.icon(
                        onPressed: () =>
                            ref.invalidate(yearInReviewProvider(_key)),
                        icon: const Icon(Icons.refresh),
                        label: Text(l10n.commonRetry),
                      ),
              ),
            ),
            data: (data) => data.ready
                ? YearInReviewStory(
                    key: ValueKey(data.year),
                    recap: data,
                    onYearSelected: _selectYear,
                  )
                : _NotReady(
                    recap: data,
                    userId: widget.userId,
                    onYearSelected: _selectYear,
                  ),
          ),
        ),
      ),
    );
  }
}

/// Years to offer in the picker: those with activity plus the shown one.
List<int> _yearsOf(YearInReview recap) =>
    {...recap.availableYears, recap.year}.toList()
      ..sort((a, b) => b.compareTo(a));

/// Gradient background, the safe area and a close button (plus the year
/// picker when [recap] is given).
class _Frame extends StatelessWidget {
  const _Frame({
    required this.colors,
    required this.child,
    this.recap,
    this.onYearSelected,
  });

  final List<Color> colors;
  final Widget child;
  final YearInReview? recap;
  final ValueChanged<int>? onYearSelected;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: colors,
        ),
      ),
      child: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 4, 0),
              child: _HeaderRow(recap: recap, onYearSelected: onYearSelected),
            ),
            Expanded(child: child),
          ],
        ),
      ),
    );
  }
}

class _HeaderRow extends StatelessWidget {
  const _HeaderRow({this.recap, this.onYearSelected});

  final YearInReview? recap;
  final ValueChanged<int>? onYearSelected;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final recap = this.recap;
    final years = recap == null ? const <int>[] : _yearsOf(recap);
    const yearStyle = TextStyle(
      color: Colors.white,
      fontSize: 18,
      fontWeight: FontWeight.w700,
    );

    return Row(
      children: [
        if (recap != null)
          if (years.length > 1 && onYearSelected != null)
            PopupMenuButton<int>(
              tooltip: l10n.recapPickYear,
              initialValue: recap.year,
              onSelected: (year) {
                if (year != recap.year) onYearSelected!(year);
              },
              itemBuilder: (_) => [
                for (final year in years)
                  // A year is not a quantity: no digit grouping ("2.026").
                  PopupMenuItem(value: year, child: Text('$year')),
              ],
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text('${recap.year}', style: yearStyle),
                    const Icon(Icons.arrow_drop_down, color: Colors.white),
                  ],
                ),
              ),
            )
          else
            Text('${recap.year}', style: yearStyle),
        const Spacer(),
        IconButton(
          tooltip: l10n.recapClose,
          icon: const Icon(Icons.close, color: Colors.white),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
      ],
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({
    required this.icon,
    required this.message,
    this.title,
    this.action,
  });

  final IconData icon;
  final String? title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 56, color: _gold),
            if (title != null) ...[
              const SizedBox(height: 16),
              Text(
                title!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 28,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
            const SizedBox(height: 12),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: _soft, fontSize: 17, height: 1.4),
            ),
            if (action != null) ...[const SizedBox(height: 24), action!],
          ],
        ),
      ),
    );
  }
}

/// Too few movies in the year for a recap.
class _NotReady extends StatelessWidget {
  const _NotReady({
    required this.recap,
    required this.userId,
    required this.onYearSelected,
  });

  final YearInReview recap;
  final String userId;
  final ValueChanged<int> onYearSelected;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return _Frame(
      colors: RecapSlide.intro.colors,
      recap: recap,
      onYearSelected: onYearSelected,
      child: _Message(
        icon: Icons.hourglass_top_rounded,
        title: l10n.recapNotReadyTitle,
        message: recap.isMine
            ? l10n.recapNotReadyMine(recap.minMovies, recap.year)
            : l10n.recapNotReadyTheirs(
                recap.userName,
                recap.minMovies,
                recap.year,
              ),
        action: recap.isMine
            ? FilledButton.icon(
                onPressed: () => context.push('/profile/$userId/diary'),
                icon: const Icon(Icons.menu_book_outlined),
                label: Text(l10n.recapOpenDiary),
              )
            : null,
      ),
    );
  }
}

/// The slides of a ready recap. Auto-advance follows
/// [storySlideDurationProvider] (off when null).
class YearInReviewStory extends ConsumerStatefulWidget {
  const YearInReviewStory({
    super.key,
    required this.recap,
    required this.onYearSelected,
  });

  final YearInReview recap;
  final ValueChanged<int> onYearSelected;

  @override
  ConsumerState<YearInReviewStory> createState() => _YearInReviewStoryState();
}

class _YearInReviewStoryState extends ConsumerState<YearInReviewStory>
    with SingleTickerProviderStateMixin {
  late final List<RecapSlide> _slides = RecapSlide.forRecap(widget.recap);
  late final Duration? _duration;
  late final AnimationController _progress;
  final _cardKey = GlobalKey();
  int _index = 0;
  bool _sharing = false;

  RecapSlide get _slide => _slides[_index];

  @override
  void initState() {
    super.initState();
    _duration = ref.read(storySlideDurationProvider);
    _progress = AnimationController(
      vsync: this,
      duration: _duration ?? const Duration(seconds: 1),
    )..addStatusListener(_onProgress);
    _restart();
  }

  @override
  void dispose() {
    _progress.dispose();
    super.dispose();
  }

  void _onProgress(AnimationStatus status) {
    if (status == AnimationStatus.completed) _next();
  }

  void _restart() {
    if (_duration == null) return;
    _progress.forward(from: 0);
  }

  void _next() {
    if (_index >= _slides.length - 1) return; // the finale stays
    setState(() => _index++);
    _restart();
  }

  void _previous() {
    if (_index > 0) setState(() => _index--);
    _restart();
  }

  void _pause() => _progress.stop();

  void _resume() {
    if (_duration != null && !_sharing && _progress.value < 1) {
      _progress.forward();
    }
  }

  Future<void> _share() async {
    final l10n = context.l10n;
    final messenger = ScaffoldMessenger.of(context);
    final recap = widget.recap;
    final title = recap.identity.title.isEmpty
        ? l10n.movieCount(recap.totals.movies)
        : recap.identity.title;
    setState(() => _sharing = true);
    _pause();
    try {
      final boundary =
          _cardKey.currentContext!.findRenderObject()! as RenderRepaintBoundary;
      final image = await boundary.toImage(
        pixelRatio: YearInReviewShareCard.pixelRatio,
      );
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      await ref.read(imageSharerProvider)(
        bytes!.buffer.asUint8List(),
        recap.isMine
            ? l10n.recapShareTextMine(recap.year, title)
            : l10n.recapShareTextTheirs(recap.userName, recap.year, title),
      );
    } catch (_) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.shareImageFailed)));
    } finally {
      if (mounted) setState(() => _sharing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final recap = widget.recap;
    return LayoutBuilder(
      builder: (context, constraints) => Stack(
        children: [
          // The story image, laid out at its own size behind the slides so
          // it can be captured; only while the finale (with the Share
          // button) is shown.
          if (_slide == RecapSlide.finale)
            Positioned(
              left: 0,
              top: 0,
              width: YearInReviewShareCard.size.width,
              height: YearInReviewShareCard.size.height,
              child: IgnorePointer(
                child: ExcludeSemantics(
                  child: RepaintBoundary(
                    key: _cardKey,
                    child: YearInReviewShareCard(recap: recap),
                  ),
                ),
              ),
            ),
          Positioned.fill(
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 400),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: _slide.colors,
                ),
              ),
            ),
          ),
          Positioned.fill(
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTapUp: (details) =>
                  details.localPosition.dx < constraints.maxWidth / 3
                  ? _previous()
                  : _next(),
              onLongPressStart: (_) => _pause(),
              onLongPressEnd: (_) => _resume(),
              onLongPressCancel: _resume,
              child: SafeArea(
                child: Padding(
                  padding: const EdgeInsets.only(top: 64),
                  child: AnimatedSwitcher(
                    duration: const Duration(milliseconds: 250),
                    child: KeyedSubtree(
                      key: ValueKey(_slide),
                      child: _buildSlide(context, _slide),
                    ),
                  ),
                ),
              ),
            ),
          ),
          // Every child is positioned, so the stack fills the screen.
          Positioned(
            left: 0,
            top: 0,
            right: 0,
            child: SafeArea(
              bottom: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 8, 4, 0),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    _ProgressBars(
                      count: _slides.length,
                      index: _index,
                      progress: _duration == null ? null : _progress,
                    ),
                    _HeaderRow(
                      recap: recap,
                      onYearSelected: widget.onYearSelected,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSlide(BuildContext context, RecapSlide slide) {
    final recap = widget.recap;
    return switch (slide) {
      RecapSlide.intro => _IntroSlide(recap: recap),
      RecapSlide.totals => _TotalsSlide(recap: recap),
      RecapSlide.rhythm => _RhythmSlide(recap: recap),
      RecapSlide.genres => _GenresSlide(recap: recap),
      RecapSlide.topMovies => _TopMoviesSlide(recap: recap),
      RecapSlide.contrarian => _ContrarianSlide(recap: recap),
      RecapSlide.bookends => _BookendsSlide(recap: recap),
      RecapSlide.rating => _RatingSlide(recap: recap),
      RecapSlide.tasteMate => _TasteMateSlide(recap: recap),
      RecapSlide.finale => _FinaleSlide(
        recap: recap,
        onShare: _sharing ? null : _share,
      ),
    };
  }
}

/// One segment per slide: past ones full, the current one filling up.
class _ProgressBars extends StatelessWidget {
  const _ProgressBars({
    required this.count,
    required this.index,
    required this.progress,
  });

  final int count;
  final int index;

  /// Null without auto-advance: the current segment is shown full.
  final Animation<double>? progress;

  @override
  Widget build(BuildContext context) {
    Widget segment(double value) => Expanded(
      child: Container(
        height: 3,
        margin: const EdgeInsets.symmetric(horizontal: 2),
        decoration: BoxDecoration(
          color: Colors.white24,
          borderRadius: BorderRadius.circular(2),
        ),
        alignment: AlignmentDirectional.centerStart,
        child: FractionallySizedBox(
          widthFactor: value.clamp(0.0, 1.0),
          heightFactor: 1,
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(2),
            ),
          ),
        ),
      ),
    );

    final progress = this.progress;
    return Semantics(
      label: '${index + 1}/$count',
      child: progress == null
          ? Row(
              children: [
                for (var i = 0; i < count; i++) segment(i <= index ? 1 : 0),
              ],
            )
          : AnimatedBuilder(
              animation: progress,
              builder: (context, _) => Row(
                children: [
                  for (var i = 0; i < count; i++)
                    segment(
                      i < index
                          ? 1
                          : i == index
                          ? progress.value
                          : 0,
                    ),
                ],
              ),
            ),
    );
  }
}

// --- Slides ---

const _titleStyle = TextStyle(
  color: Colors.white,
  fontSize: 30,
  fontWeight: FontWeight.w800,
  height: 1.15,
);
const _bodyStyle = TextStyle(color: _soft, fontSize: 17, height: 1.35);
const _labelStyle = TextStyle(
  color: Color(0xFF9FB3FF),
  fontSize: 13,
  fontWeight: FontWeight.w700,
  letterSpacing: 1.2,
);

/// A slide's content, vertically centred and scrollable on short screens.
class _SlideBody extends StatelessWidget {
  const _SlideBody({required this.children, this.title});

  final String? title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(28, 8, 28, 32),
        child: ConstrainedBox(
          constraints: BoxConstraints(
            minHeight: math.max(0, constraints.maxHeight - 40),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (title != null) ...[
                Text(title!, style: _titleStyle),
                const SizedBox(height: 24),
              ],
              ...children,
            ],
          ),
        ),
      ),
    );
  }
}

class _IntroSlide extends StatelessWidget {
  const _IntroSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final movies = recap.totals.movies;
    return _SlideBody(
      children: [
        Text(
          recap.isMine
              ? l10n.recapIntroMine(recap.year)
              : l10n.recapIntroTheirs(recap.userName, recap.year),
          style: _titleStyle.copyWith(fontSize: 36),
        ),
        const SizedBox(height: 32),
        Text(
          l10n.decimal(movies, digits: 0),
          style: const TextStyle(
            color: _gold,
            fontSize: 112,
            fontWeight: FontWeight.w900,
            height: 1,
          ),
        ),
        Text(
          recap.isMine
              ? l10n.recapIntroMoviesMine(movies)
              : l10n.recapIntroMoviesTheirs(movies),
          style: _bodyStyle.copyWith(fontSize: 22, color: Colors.white),
        ),
      ],
    );
  }
}

class _TotalsSlide extends StatelessWidget {
  const _TotalsSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final t = recap.totals;
    final mine = recap.isMine;
    final rows = <(int, String Function(int))>[
      (t.diaryEntries, l10n.recapTotalDiaryEntries),
      (t.rewatches, l10n.recapTotalRewatches),
      (t.ratings, l10n.recapTotalRatings),
      (t.reviews, l10n.recapTotalReviews),
      (
        t.likesReceived,
        mine ? l10n.recapTotalLikesMine : l10n.recapTotalLikesTheirs,
      ),
      (
        t.commentsReceived,
        mine ? l10n.recapTotalCommentsMine : l10n.recapTotalCommentsTheirs,
      ),
    ];
    return _SlideBody(
      title: l10n.recapTotalsTitle,
      children: [
        for (final (count, label) in rows)
          if (count > 0)
            Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.baseline,
                textBaseline: TextBaseline.alphabetic,
                children: [
                  Text(
                    l10n.decimal(count, digits: 0),
                    style: const TextStyle(
                      color: _gold,
                      fontSize: 40,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      label(count),
                      style: _bodyStyle.copyWith(fontSize: 19),
                    ),
                  ),
                ],
              ),
            ),
      ],
    );
  }
}

class _RhythmSlide extends StatelessWidget {
  const _RhythmSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final busiest = recap.busiestMonth;
    final weekday = recap.favoriteWeekday;
    return _SlideBody(
      title: l10n.recapRhythmTitle,
      children: [
        _MonthBars(recap: recap),
        if (busiest != null) ...[
          const SizedBox(height: 28),
          Text(
            (recap.isMine
                ? l10n.recapBusiestMonthMine
                : l10n.recapBusiestMonthTheirs)(
              DateFormat.MMMM(l10n.localeName)
                  .format(DateTime(recap.year, busiest.month)),
              busiest.count,
            ),
            style: _bodyStyle.copyWith(fontSize: 20, color: Colors.white),
          ),
        ],
        if (weekday != null) ...[
          const SizedBox(height: 14),
          Text(
            (recap.isMine
                ? l10n.recapFavoriteWeekdayMine
                : l10n.recapFavoriteWeekdayTheirs)(
              // January 1, 2024 was a Monday: day N is ISO weekday N.
              DateFormat.EEEE(l10n.localeName)
                  .format(DateTime(2024, 1, weekday.weekday)),
            ),
            style: _bodyStyle.copyWith(fontSize: 20, color: Colors.white),
          ),
        ],
      ],
    );
  }
}

/// Twelve bars scaled to the busiest month, which is highlighted.
class _MonthBars extends StatelessWidget {
  const _MonthBars({required this.recap});

  final YearInReview recap;

  static const _height = 150.0;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    // Stand-alone narrow month names ("J", "F"... / "O", "Ş"...).
    final narrow = DateFormat('LLLLL', l10n.localeName);
    final full = DateFormat.MMMM(l10n.localeName);
    final max = math.max(1, recap.monthCounts.reduce(math.max));
    final busiest = recap.busiestMonth?.month;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        for (var month = 1; month <= 12; month++)
          Expanded(
            child: Builder(
              builder: (context) {
                final count = recap.countFor(month);
                final date = DateTime(recap.year, month);
                return Semantics(
                  label: '${full.format(date)}: ${l10n.movieCount(count)}',
                  excludeSemantics: true,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Container(
                        height: count == 0
                            ? 3
                            : math.max(6, _height * count / max),
                        margin: const EdgeInsets.symmetric(horizontal: 3),
                        decoration: BoxDecoration(
                          color: count == 0
                              ? Colors.white24
                              : month == busiest
                              ? _gold
                              : Colors.white70,
                          borderRadius: const BorderRadius.vertical(
                            top: Radius.circular(4),
                          ),
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        narrow.format(date),
                        style: TextStyle(
                          color: month == busiest ? _gold : Colors.white70,
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),
                );
              },
            ),
          ),
      ],
    );
  }
}

class _GenresSlide extends StatelessWidget {
  const _GenresSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return _SlideBody(
      title: recap.isMine
          ? l10n.recapGenresTitleMine
          : l10n.recapGenresTitleTheirs,
      children: [
        for (final genre in recap.identity.topGenres)
          Padding(
            padding: const EdgeInsets.only(bottom: 18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        genre.name,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 20,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    Text(
                      l10n.percent(genre.share),
                      style: const TextStyle(
                        color: _gold,
                        fontSize: 18,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                ClipRRect(
                  borderRadius: BorderRadius.circular(6),
                  child: LinearProgressIndicator(
                    value: genre.share / 100,
                    minHeight: 12,
                    backgroundColor: Colors.white24,
                    color: _gold,
                  ),
                ),
                if (genre.count > 0) ...[
                  const SizedBox(height: 4),
                  Text(
                    l10n.movieCount(genre.count),
                    style: _bodyStyle.copyWith(fontSize: 14),
                  ),
                ],
              ],
            ),
          ),
      ],
    );
  }
}

class _TopMoviesSlide extends StatelessWidget {
  const _TopMoviesSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final favoriteId = recap.identity.favorite?.movieId;
    return _SlideBody(
      title: recap.isMine
          ? l10n.recapTopMoviesTitleMine
          : l10n.recapTopMoviesTitleTheirs,
      children: [
        Wrap(
          spacing: 14,
          runSpacing: 18,
          children: [
            for (final movie in recap.topMovies)
              _RankedPoster(
                movie: movie,
                isFavorite: movie.movieId == favoriteId,
              ),
          ],
        ),
      ],
    );
  }
}

class _RankedPoster extends StatelessWidget {
  const _RankedPoster({required this.movie, required this.isFavorite});

  final IdentityMovie movie;
  final bool isFavorite;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final width = isFavorite ? 120.0 : 92.0;
    return SizedBox(
      width: width,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(10),
              border: isFavorite ? Border.all(color: _gold, width: 3) : null,
            ),
            child: PosterImage(
              posterPath: movie.posterPath,
              width: width,
              height: width * 1.5,
              size: 'w185',
              borderRadius: 8,
            ),
          ),
          const SizedBox(height: 6),
          if (isFavorite)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
              margin: const EdgeInsets.only(bottom: 4),
              decoration: BoxDecoration(
                color: _gold,
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                l10n.recapFavoriteBadge,
                style: const TextStyle(
                  color: Colors.black,
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          Text(
            movie.title,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 13,
              fontWeight: FontWeight.w600,
            ),
          ),
          Row(
            children: [
              const Icon(Icons.star_rounded, size: 15, color: _gold),
              const SizedBox(width: 2),
              Text(
                l10n.ratingOutOfTen(movie.rating),
                style: const TextStyle(color: _soft, fontSize: 13),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ContrarianSlide extends StatelessWidget {
  const _ContrarianSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final take = recap.identity.contrarianTake!;
    final average = l10n.decimal(take.tmdbAverage!);
    return _SlideBody(
      title: l10n.recapContrarianTitle,
      children: [
        PosterImage(
          posterPath: take.posterPath,
          width: 120,
          height: 180,
          size: 'w185',
        ),
        const SizedBox(height: 20),
        Text(
          recap.isMine
              ? l10n.recapContrarianLineMine(take.title, average, take.rating)
              : l10n.recapContrarianLineTheirs(
                  take.title,
                  average,
                  recap.userName,
                  take.rating,
                ),
          style: _bodyStyle.copyWith(fontSize: 22, color: Colors.white),
        ),
      ],
    );
  }
}

class _BookendsSlide extends StatelessWidget {
  const _BookendsSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final date = DateFormat.yMMMMd(l10n.localeName);
    final first = recap.firstMovie;
    final last = recap.lastMovie;
    final rewatched = recap.mostRewatched;
    return _SlideBody(
      title: l10n.recapBookendsTitle,
      children: [
        if (first != null)
          _MovieLine(
            label: l10n.recapFirstMovie,
            movie: first.movie,
            detail: date.format(first.watchedOn),
          ),
        if (last != null)
          _MovieLine(
            label: l10n.recapLastMovie,
            movie: last.movie,
            detail: date.format(last.watchedOn),
          ),
        if (rewatched != null)
          _MovieLine(
            label: l10n.recapMostRewatched,
            movie: rewatched.movie,
            detail: l10n.recapWatchedTimes(rewatched.count),
          ),
      ],
    );
  }
}

class _MovieLine extends StatelessWidget {
  const _MovieLine({
    required this.label,
    required this.movie,
    required this.detail,
  });

  final String label;
  final Movie movie;
  final String detail;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 20),
      child: Row(
        children: [
          PosterImage(
            posterPath: movie.posterPath,
            width: 56,
            height: 84,
            size: 'w185',
            borderRadius: 6,
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: _labelStyle),
                const SizedBox(height: 4),
                Text(
                  movie.title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 19,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 2),
                Text(detail, style: _bodyStyle.copyWith(fontSize: 15)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _RatingSlide extends StatelessWidget {
  const _RatingSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final average = recap.averageRating!;
    final previous = recap.previousYearAverage;
    String? comparison;
    if (previous != null) {
      // Compared as shown (one decimal), so "7.4 vs 7.4" is never "higher".
      final difference = (average * 10).round() - (previous * 10).round();
      final previousYear = recap.year - 1;
      comparison = difference > 0
          ? l10n.recapRatingHigher(l10n.decimal(previous), previousYear)
          : difference < 0
          ? l10n.recapRatingLower(l10n.decimal(previous), previousYear)
          : l10n.recapRatingSame(previousYear);
    }
    return _SlideBody(
      title: recap.isMine
          ? l10n.recapRatingTitleMine
          : l10n.recapRatingTitleTheirs,
      children: [
        Text.rich(
          TextSpan(
            children: [
              TextSpan(
                text: l10n.decimal(average),
                style: const TextStyle(
                  color: _gold,
                  fontSize: 112,
                  fontWeight: FontWeight.w900,
                  height: 1,
                ),
              ),
              const TextSpan(
                text: ' /10',
                style: TextStyle(color: _soft, fontSize: 28),
              ),
            ],
          ),
        ),
        if (comparison != null) ...[
          const SizedBox(height: 20),
          Text(
            comparison,
            style: _bodyStyle.copyWith(fontSize: 20, color: Colors.white),
          ),
        ],
      ],
    );
  }
}

class _TasteMateSlide extends StatelessWidget {
  const _TasteMateSlide({required this.recap});

  final YearInReview recap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final mate = recap.tasteMate!;
    final score = mate.compatibility;
    return _SlideBody(
      title: recap.isMine
          ? l10n.recapTasteMateTitleMine
          : l10n.recapTasteMateTitleTheirs,
      children: [
        Center(
          child: Column(
            children: [
              UserAvatar(
                userName: mate.user.userName,
                imageUrl: mate.user.imageUrl,
                radius: 52,
              ),
              const SizedBox(height: 14),
              Text(
                mate.user.userName,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 24,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                l10n.recapCompatible(
                  l10n.percent(score, digits: score % 1 == 0 ? 0 : 1),
                ),
                style: const TextStyle(
                  color: _gold,
                  fontSize: 30,
                  fontWeight: FontWeight.w900,
                ),
              ),
              if (mate.commonCount > 0)
                Text(
                  l10n.recapCommonMovies(mate.commonCount),
                  style: _bodyStyle,
                ),
              const SizedBox(height: 20),
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  foregroundColor: Colors.white,
                  side: const BorderSide(color: Colors.white70),
                ),
                onPressed: () => context.push('/profile/${mate.user.id}'),
                icon: const Icon(Icons.person_outline),
                label: Text(l10n.viewProfile),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _FinaleSlide extends StatelessWidget {
  const _FinaleSlide({required this.recap, required this.onShare});

  final YearInReview recap;

  /// Null while a share is in progress.
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final identity = recap.identity;
    return _SlideBody(
      children: [
        // Not upper-cased in code: Dart's toUpperCase is not Turkish-aware
        // ("i" would become "I", not "İ").
        Text(
          recap.isMine
              ? l10n.recapFinaleLabelMine(recap.year)
              : l10n.recapFinaleLabelTheirs(recap.year),
          style: _labelStyle.copyWith(fontSize: 15),
        ),
        const SizedBox(height: 12),
        if (identity.title.isNotEmpty)
          Text(
            identity.title,
            style: _titleStyle.copyWith(color: _gold, fontSize: 40),
          ),
        if (identity.titleDescription.isNotEmpty) ...[
          const SizedBox(height: 12),
          Text(
            identity.titleDescription,
            style: _bodyStyle.copyWith(fontSize: 19),
          ),
        ],
        const SizedBox(height: 36),
        FilledButton.icon(
          style: FilledButton.styleFrom(
            backgroundColor: _gold,
            foregroundColor: Colors.black,
          ),
          onPressed: onShare,
          icon: const Icon(Icons.ios_share),
          label: Text(l10n.recapShare),
        ),
      ],
    );
  }
}
