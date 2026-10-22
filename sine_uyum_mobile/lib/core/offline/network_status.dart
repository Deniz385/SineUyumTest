import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// The device's network connection (is there Wi-Fi/mobile data). It does not
/// say whether the internet is actually reachable; that is what
/// [NetworkStatus.serverReachable] tracks.
abstract class ConnectivitySource {
  Future<bool> isOnline();
  Stream<bool> get changes;
}

class PlusConnectivitySource implements ConnectivitySource {
  final _connectivity = Connectivity();

  static bool _online(List<ConnectivityResult> results) =>
      results.any((r) => r != ConnectivityResult.none);

  @override
  Future<bool> isOnline() async {
    try {
      return _online(await _connectivity.checkConnectivity());
    } catch (_) {
      return true; // if unknown, trying the network is harmless
    }
  }

  // A plugin error (unsupported platform etc.) must not crash the app; with
  // no events the network is assumed "up" and request errors decide.
  @override
  Stream<bool> get changes => _connectivity.onConnectivityChanged
      .map(_online)
      .handleError((Object _) {})
      .distinct();
}

final connectivitySourceProvider = Provider<ConnectivitySource>(
  (ref) => PlusConnectivitySource(),
);

class NetworkStatus {
  const NetworkStatus({this.deviceOnline = true, this.serverReachable = true});

  /// The OS reports a network connection.
  final bool deviceOnline;

  /// The server answered the last request (error statuses included). With a
  /// network but no internet (e.g. a hotel Wi-Fi captive portal) this is
  /// false.
  final bool serverReachable;

  /// The user should be told "showing saved data".
  bool get isOffline => !deviceOnline || !serverReachable;

  NetworkStatus copyWith({bool? deviceOnline, bool? serverReachable}) =>
      NetworkStatus(
        deviceOnline: deviceOnline ?? this.deviceOnline,
        serverReachable: serverReachable ?? this.serverReachable,
      );
}

final networkStatusProvider =
    NotifierProvider<NetworkStatusNotifier, NetworkStatus>(
      NetworkStatusNotifier.new,
    );

class NetworkStatusNotifier extends Notifier<NetworkStatus> {
  @override
  NetworkStatus build() {
    final source = ref.watch(connectivitySourceProvider);
    final subscription = source.changes.listen(_onDevice);
    ref.onDispose(subscription.cancel);
    source.isOnline().then(_onDevice);
    return const NetworkStatus();
  }

  void _onDevice(bool online) {
    // When the network comes back the server is assumed reachable; if not,
    // the first request corrects it.
    final next = online
        ? state.copyWith(deviceOnline: true, serverReachable: true)
        : state.copyWith(deviceOnline: false);
    if (next.deviceOnline != state.deviceOnline ||
        next.serverReachable != state.serverReachable) {
      state = next;
    }
  }

  /// Reported by `ApiClient` on every response (true) or network error (false).
  void reportServer(bool reachable) {
    if (state.serverReachable != reachable) {
      state = state.copyWith(serverReachable: reachable);
    }
  }
}
