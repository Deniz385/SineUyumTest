import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../l10n/l10n.dart';
import '../../providers/repository_providers.dart';
import '../../widgets/poster_image.dart';
import '../../widgets/state_views.dart';

/// User search — results are sorted by compatibility percentage.
class UserSearchPage extends ConsumerStatefulWidget {
  const UserSearchPage({super.key});

  @override
  ConsumerState<UserSearchPage> createState() => _UserSearchPageState();
}

class _UserSearchPageState extends ConsumerState<UserSearchPage> {
  final _controller = TextEditingController();
  Timer? _debounce;
  String _query = '';

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      if (mounted) setState(() => _query = value.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(16),
          child: TextField(
            controller: _controller,
            onChanged: _onChanged,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: l10n.userSearchHint,
              prefixIcon: const Icon(Icons.search),
            ),
          ),
        ),
        Expanded(
          child: _query.isEmpty
              ? EmptyView(
                  icon: Icons.people_outline,
                  message: l10n.userSearchPrompt,
                )
              : ref
                    .watch(userSearchProvider(_query))
                    .when(
                      loading: () => const LoadingView(),
                      error: (error, _) => ErrorView(
                        message: error.toString(),
                        onRetry: () =>
                            ref.invalidate(userSearchProvider(_query)),
                      ),
                      data: (users) => users.isEmpty
                          ? EmptyView(message: l10n.userSearchEmpty)
                          : ListView.builder(
                              itemCount: users.length,
                              itemBuilder: (context, index) {
                                final user = users[index];
                                return ListTile(
                                  leading: UserAvatar(
                                    userName: user.userName,
                                    imageUrl: user.profileImageUrl,
                                  ),
                                  title: Text(user.userName),
                                  subtitle:
                                      user.bio == null || user.bio!.isEmpty
                                      ? null
                                      : Text(
                                          user.bio!,
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                  trailing: CompatibilityBadge(
                                    score: user.compatibilityScore,
                                    commonMovieCount: user.commonMovieCount,
                                  ),
                                  onTap: () =>
                                      context.push('/profile/${user.id}'),
                                );
                              },
                            ),
                    ),
        ),
      ],
    );
  }
}
