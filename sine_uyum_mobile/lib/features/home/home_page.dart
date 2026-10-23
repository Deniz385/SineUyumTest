import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/tokens.dart';
import '../../core/ui/wordmark.dart';
import '../../l10n/l10n.dart';
import '../../providers/session_provider.dart';
import '../../providers/year_in_review_provider.dart';
import '../shell/tab_scaffold.dart';
import '../social/feed_view.dart';

/// Home: a greeting, the taste-twin / year-in-review cards and the feed of
/// the people you follow; movie rows when the feed is thin.
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Scaffold(
      appBar: AppBar(
        title: const Wordmark(size: 24),
        actions: [
          IconButton(
            tooltip: l10n.searchMoviesTooltip,
            icon: const Icon(Icons.search_rounded),
            onPressed: () => context.push('/movies/search'),
          ),
          const NotificationsButton(),
          const SizedBox(width: 4),
        ],
      ),
      body: const FeedView(header: HomeGreeting()),
    );
  }
}

/// "Good evening, ayse" and a one-line hint.
class HomeGreeting extends ConsumerWidget {
  const HomeGreeting({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final name =
        ref.watch(sessionProvider.select((s) => s.user?.userName)) ?? '';
    final hour = ref.watch(clockProvider)().hour;
    final greeting = switch (hour) {
      >= 5 && < 12 => l10n.homeGreetingMorning(name),
      >= 12 && < 18 => l10n.homeGreetingAfternoon(name),
      _ => l10n.homeGreetingEvening(name),
    };
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AppSpace.gutter,
        AppSpace.xs,
        AppSpace.gutter,
        AppSpace.md,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Semantics(
            header: true,
            child: Text(greeting, style: context.type.h1),
          ),
          const SizedBox(height: AppSpace.xxs),
          Text(
            l10n.homeGreetingSubtitle,
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
  }
}
