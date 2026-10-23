import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/movie_widgets.dart';
import '../../core/ui/poster_image.dart';
import '../../core/ui/skeleton.dart';
import '../../core/ui/state_views.dart';
import '../../core/ui/wordmark.dart';
import '../../data/models/movie.dart';
import '../../l10n/l10n.dart';
import '../../providers/locale_provider.dart';
import '../../providers/repository_providers.dart';
import '../../providers/session_provider.dart';
import '../discover/discover_page.dart';
import 'welcome_provider.dart';

/// The first-run welcome (docs/DESIGN-SYSTEM.md §8), shown while the
/// signed-in user's `onboardingCompleted` is false: Welcome → Rate a few
/// films → People like you → Done. Every step can be skipped; skipping or
/// finishing calls `POST /account/onboarding/complete` and the router moves
/// on to Home.
class WelcomePage extends ConsumerStatefulWidget {
  const WelcomePage({super.key});

  /// Ratings that unlock "Continue" on the rating step.
  static const ratingsGoal = 5;

  @override
  ConsumerState<WelcomePage> createState() => _WelcomePageState();
}

enum _Step { intro, rate, people, done }

class _WelcomePageState extends ConsumerState<WelcomePage> {
  _Step _step = _Step.intro;
  bool _finishing = false;

  /// movieId → rating given in this welcome.
  final _ratings = <int, int>{};

  void _go(_Step step) => setState(() => _step = step);

  Future<void> _finish() async {
    if (_finishing) return;
    setState(() => _finishing = true);
    await ref.read(sessionProvider.notifier).completeOnboarding();
    // The router leaves for Home once the session says "completed".
    if (mounted) setState(() => _finishing = false);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final step = _step;
    final body = switch (step) {
      _Step.intro => _IntroStep(onStart: () => _go(_Step.rate)),
      _Step.rate => _RateStep(
        ratings: _ratings,
        onRated: (id, rating) => setState(() => _ratings[id] = rating),
        onContinue: () => _go(_Step.people),
      ),
      _Step.people => _PeopleStep(onContinue: () => _go(_Step.done)),
      _Step.done => _DoneStep(busy: _finishing, onFinish: _finish),
    };

    return PopScope(
      canPop: step == _Step.intro,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _go(_Step.values[step.index - 1]);
      },
      child: Scaffold(
        body: DecoratedBox(
          decoration: BoxDecoration(
            gradient: RadialGradient(
              center: const Alignment(0.9, -1.1),
              radius: 1.3,
              colors: [
                colors.primary.withValues(alpha: colors.isDark ? 0.16 : 0.07),
                colors.bg,
              ],
            ),
          ),
          child: SafeArea(
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    AppSpace.gutter,
                    AppSpace.xs,
                    AppSpace.xs,
                    0,
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: _StepDots(
                          index: step.index,
                          count: _Step.values.length,
                          label: l10n.welcomeStep(
                            step.index + 1,
                            _Step.values.length,
                          ),
                        ),
                      ),
                      if (step != _Step.done)
                        TextButton(
                          onPressed: _finishing ? null : _finish,
                          child: Text(l10n.welcomeSkip),
                        )
                      else
                        const SizedBox(height: 48),
                    ],
                  ),
                ),
                Expanded(
                  child: AnimatedSwitcher(
                    duration: AppMotion.of(context, AppMotion.emphasized),
                    switchInCurve: AppMotion.enterCurve,
                    child: KeyedSubtree(key: ValueKey(step), child: body),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _StepDots extends StatelessWidget {
  const _StepDots({
    required this.index,
    required this.count,
    required this.label,
  });

  final int index;
  final int count;
  final String label;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Semantics(
      label: label,
      excludeSemantics: true,
      child: Row(
        children: [
          for (var i = 0; i < count; i++)
            AnimatedContainer(
              duration: AppMotion.of(context, AppMotion.standard),
              margin: const EdgeInsetsDirectional.only(end: 6),
              width: i == index ? 24 : 8,
              height: 8,
              decoration: BoxDecoration(
                color: i <= index ? colors.primary : colors.border,
                borderRadius: BorderRadius.circular(AppRadii.pill),
              ),
            ),
        ],
      ),
    );
  }
}

/// Step 1: wordmark, the promise, three benefits and the language.
class _IntroStep extends ConsumerWidget {
  const _IntroStep({required this.onStart});

  final VoidCallback onStart;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final colors = context.colors;
    final current = ref.watch(appLocaleProvider);

    Widget benefit(IconData icon, String text) => Padding(
      padding: const EdgeInsets.only(bottom: AppSpace.md),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: colors.surfaceRaised,
              borderRadius: BorderRadius.circular(AppRadii.md),
            ),
            child: Icon(icon, color: colors.primary),
          ),
          const SizedBox(width: AppSpace.md),
          Expanded(child: Text(text, style: context.type.body)),
        ],
      ),
    );

    return ListView(
      padding: const EdgeInsets.fromLTRB(
        AppSpace.xl,
        AppSpace.xxl,
        AppSpace.xl,
        AppSpace.xl,
      ),
      children: [
        const Wordmark(size: 22),
        const SizedBox(height: AppSpace.xl),
        Semantics(
          header: true,
          child: Text(l10n.welcomeTitle, style: context.type.display),
        ),
        const SizedBox(height: AppSpace.xxl),
        benefit(Icons.favorite_rounded, l10n.welcomeBenefitMatch),
        benefit(Icons.dynamic_feed_rounded, l10n.welcomeBenefitFeed),
        benefit(Icons.local_activity_rounded, l10n.welcomeBenefitEvents),
        const SizedBox(height: AppSpace.lg),
        Text(
          context.l10n.welcomeLanguage.toUpperCase(),
          style: context.type.overline,
        ),
        const SizedBox(height: AppSpace.xs),
        SegmentedButton<String>(
          showSelectedIcon: false,
          segments: [
            for (final locale in AppLocalizations.supportedLocales)
              ButtonSegment(
                value: locale.languageCode,
                label: Text(lookupAppLocalizations(locale).languageName),
              ),
          ],
          selected: {current.languageCode},
          onSelectionChanged: (selection) => ref
              .read(localeProvider.notifier)
              .set(L10n.supported(selection.first)),
        ),
        const SizedBox(height: AppSpace.xxl),
        FilledButton(onPressed: onStart, child: Text(l10n.welcomeStart)),
      ],
    );
  }
}

/// Step 2: a poster grid to rate; "Continue" unlocks at five ratings.
class _RateStep extends ConsumerStatefulWidget {
  const _RateStep({
    required this.ratings,
    required this.onRated,
    required this.onContinue,
  });

  final Map<int, int> ratings;
  final void Function(int movieId, int rating) onRated;
  final VoidCallback onContinue;

  @override
  ConsumerState<_RateStep> createState() => _RateStepState();
}

class _RateStepState extends ConsumerState<_RateStep> {
  Future<void> _rate(Movie movie) async {
    final messenger = ScaffoldMessenger.of(context);
    final rating = await RatingSheet.show(
      context,
      movieTitle: movie.title,
      posterPath: movie.posterPath,
      initialRating: widget.ratings[movie.id],
    );
    if (rating == null) return;
    try {
      await ref.read(movieRepositoryProvider).rateById(movie.id, rating);
      widget.onRated(movie.id, rating);
    } catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(error.toString())));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final movies = ref.watch(welcomeMoviesProvider);
    final count = widget.ratings.length;
    const goal = WelcomePage.ratingsGoal;

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.md,
            AppSpace.gutter,
            AppSpace.xs,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Semantics(
                header: true,
                child: Text(l10n.welcomeRateTitle, style: context.type.h1),
              ),
              const SizedBox(height: AppSpace.xxs),
              Text(
                l10n.welcomeRateHint(goal),
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
        ),
        Expanded(
          child: movies.when(
            loading: () => const SkeletonPosterGrid(count: 12),
            error: (error, _) => ErrorView(
              message: error.toString(),
              onRetry: () => ref.invalidate(welcomeMoviesProvider),
            ),
            data: (state) => LayoutBuilder(
              builder: (context, constraints) {
                const gap = AppSpace.sm;
                final columns = constraints.maxWidth >= 600 ? 5 : 3;
                return GridView.builder(
                  padding: const EdgeInsets.all(AppSpace.gutter),
                  gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: columns,
                    crossAxisSpacing: gap,
                    mainAxisSpacing: gap,
                    childAspectRatio: 2 / 3,
                  ),
                  itemCount: state.movies.length + (state.hasMore ? 1 : 0),
                  itemBuilder: (context, index) {
                    if (index == state.movies.length) {
                      return _MoreTile(
                        busy: state.isLoadingMore,
                        onTap: () =>
                            ref.read(welcomeMoviesProvider.notifier).loadMore(),
                      );
                    }
                    final movie = state.movies[index];
                    return _RateTile(
                      movie: movie,
                      rating: widget.ratings[movie.id],
                      onTap: () => _rate(movie),
                    );
                  },
                );
              },
            ),
          ),
        ),
        DecoratedBox(
          decoration: BoxDecoration(
            color: colors.surface,
            border: Border(top: BorderSide(color: colors.border)),
          ),
          child: SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpace.gutter,
                AppSpace.sm,
                AppSpace.gutter,
                AppSpace.sm,
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Semantics(
                      liveRegion: true,
                      label: l10n.welcomeRateProgressSemantics(
                        count.clamp(0, goal),
                        goal,
                      ),
                      excludeSemantics: true,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            l10n.welcomeRateProgress(
                              count.clamp(0, goal),
                              goal,
                            ),
                            style: context.type.number.copyWith(fontSize: 18),
                          ),
                          const SizedBox(height: 6),
                          ClipRRect(
                            borderRadius: BorderRadius.circular(AppRadii.pill),
                            child: LinearProgressIndicator(
                              value: (count / goal).clamp(0.0, 1.0),
                              minHeight: 6,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpace.md),
                  FilledButton(
                    onPressed: count >= goal ? widget.onContinue : null,
                    child: Text(l10n.welcomeContinue),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _RateTile extends StatelessWidget {
  const _RateTile({required this.movie, required this.rating, this.onTap});

  final Movie movie;
  final int? rating;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    final value = rating;
    return Semantics(
      button: true,
      label: [
        movie.title,
        if (value != null) l10n.ratingSpoken(value),
      ].join(', '),
      excludeSemantics: true,
      child: GestureDetector(
        onTap: onTap,
        child: Stack(
          fit: StackFit.expand,
          children: [
            PosterImage(
              posterPath: movie.posterPath,
              size: 'w185',
              width: double.infinity,
              height: double.infinity,
            ),
            // The title stays readable on a missing poster.
            if (movie.posterPath == null)
              Padding(
                padding: const EdgeInsets.all(8),
                child: Align(
                  alignment: Alignment.bottomLeft,
                  child: Text(
                    movie.title,
                    maxLines: 3,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.labelLarge,
                  ),
                ),
              ),
            if (value != null)
              DecoratedBox(
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(AppRadii.md),
                  border: Border.all(color: colors.primary, width: 3),
                  color: Colors.black.withValues(alpha: 0.45),
                ),
                child: Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.star_rounded, color: colors.primary, size: 28),
                      Text(
                        '$value',
                        style: context.type.number.copyWith(
                          fontSize: 26,
                          color: Colors.white,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _MoreTile extends StatelessWidget {
  const _MoreTile({required this.busy, required this.onTap});

  final bool busy;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Material(
      color: colors.surfaceRaised,
      borderRadius: BorderRadius.circular(AppRadii.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.md),
        onTap: busy ? null : onTap,
        child: Center(
          child: busy
              ? const Shimmer(child: SkeletonBox(width: 48, height: 48))
              : Padding(
                  padding: const EdgeInsets.all(8),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.add_rounded, color: colors.primary),
                      const SizedBox(height: 4),
                      Text(
                        context.l10n.welcomeMoreFilms,
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.labelLarge,
                      ),
                    ],
                  ),
                ),
        ),
      ),
    );
  }
}

/// Step 3: people to follow (taste-based when possible).
class _PeopleStep extends StatelessWidget {
  const _PeopleStep({required this.onContinue});

  final VoidCallback onContinue;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpace.gutter,
            AppSpace.md,
            AppSpace.gutter,
            0,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Semantics(
                header: true,
                child: Text(l10n.welcomePeopleTitle, style: context.type.h1),
              ),
              const SizedBox(height: AppSpace.xxs),
              Text(
                l10n.welcomePeopleHint,
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
        ),
        const Expanded(child: PeopleSuggestionsList()),
        SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.all(AppSpace.gutter),
            child: SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: onContinue,
                child: Text(l10n.welcomeContinue),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// Step 4: a calm confirmation, then Home.
class _DoneStep extends StatelessWidget {
  const _DoneStep({required this.busy, required this.onFinish});

  final bool busy;
  final VoidCallback onFinish;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final colors = context.colors;
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        AppSpace.xl,
        AppSpace.x5,
        AppSpace.xl,
        AppSpace.xl,
      ),
      children: [
        Center(
          child: Container(
            width: 88,
            height: 88,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colors.success.withValues(alpha: 0.14),
            ),
            child: Icon(Icons.check_rounded, size: 44, color: colors.success),
          ),
        ),
        const SizedBox(height: AppSpace.xl),
        Semantics(
          header: true,
          child: Text(
            l10n.welcomeDoneTitle,
            textAlign: TextAlign.center,
            style: context.type.h1,
          ),
        ),
        const SizedBox(height: AppSpace.sm),
        Text(
          l10n.welcomeDoneBody,
          textAlign: TextAlign.center,
          style: context.type.body.copyWith(color: colors.textSecondary),
        ),
        const SizedBox(height: AppSpace.xxl),
        FilledButton(
          onPressed: busy ? null : onFinish,
          child: Text(l10n.welcomeGoHome),
        ),
      ],
    );
  }
}
