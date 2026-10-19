import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';

import 'app.dart';

void main() {
  // Tarih/saat biçimleri Türkçe gösterilecek; locale verisinin yüklenmesi gerekir.
  initializeDateFormatting('tr');
  Intl.defaultLocale = 'tr';

  runApp(const ProviderScope(child: SineUyumApp()));
}
