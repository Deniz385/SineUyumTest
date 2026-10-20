import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Cihazın ağ bağlantısı (Wi-Fi/mobil veri var mı). İnternete gerçekten
/// ulaşılıp ulaşılamadığını söylemez; onu [NetworkStatus.serverReachable] tutar.
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
      return true; // bilinmiyorsa ağı denemek zararsız
    }
  }

  // Eklenti hatası (desteklenmeyen platform vb.) uygulamayı düşürmemeli;
  // olay gelmezse ağ "var" kabul edilir ve istek hataları durumu belirler.
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

  /// İşletim sistemine göre bir ağ bağlantısı var.
  final bool deviceOnline;

  /// Son istekte sunucu yanıt verdi (hata kodu dahil). Ağ var ama internet
  /// yoksa (ör. girişi yapılmamış otel Wi-Fi'ı) burası false olur.
  final bool serverReachable;

  /// Kullanıcıya "kayıtlı veriler gösteriliyor" denmesi gereken durum.
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
    // Ağ geri gelince sunucuya ulaşılabildiği varsayılır; değilse ilk istek
    // bunu düzeltir.
    final next = online
        ? state.copyWith(deviceOnline: true, serverReachable: true)
        : state.copyWith(deviceOnline: false);
    if (next.deviceOnline != state.deviceOnline ||
        next.serverReachable != state.serverReachable) {
      state = next;
    }
  }

  /// [ApiClient] her yanıtta (true) ya da ağ hatasında (false) bildirir.
  void reportServer(bool reachable) {
    if (state.serverReachable != reachable) {
      state = state.copyWith(serverReachable: reachable);
    }
  }
}
