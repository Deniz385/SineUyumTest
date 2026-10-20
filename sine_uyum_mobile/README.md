# SineUyum Mobil (Flutter)

SineUyum API'si (`SineUyum.Api`) üzerine çalışan iOS + Android uygulaması.

## Gereksinimler

- Flutter 3.35 veya üzeri (geliştirme Flutter 3.47 / Dart 3.13 ile yapıldı)
- Çalışan bir SineUyum.Api örneği

## Çalıştırma

API adresi koda gömülü değildir, derleme zamanında verilir:

```bash
# Android emülatörü (ana makine 10.0.2.2 olarak görünür)
flutter run --dart-define=API_URL=http://10.0.2.2:5074

# iOS simülatörü
flutter run --dart-define=API_URL=http://localhost:5074

# Gerçek cihaz (bilgisayarın yerel IP'si)
flutter run --dart-define=API_URL=http://192.168.1.25:5074
```

`API_URL` verilmezse varsayılan `http://10.0.2.2:5074` kullanılır.

Diğer derleme değişkenleri:

| Değişken | Varsayılan | Açıklama |
|---|---|---|
| `API_URL` | `http://10.0.2.2:5074` | API temel adresi |
| `TMDB_IMAGE_BASE_URL` | `https://image.tmdb.org/t/p/` | Poster adresleri |
| `APP_VERSION` | `1.0.0` | Sunucudaki minimum sürümle karşılaştırılır |

> Şifrelenmemiş HTTP yalnızca **debug** derlemesinde ve yalnızca yerel
> adresler (`10.0.2.2`, `localhost`, `127.0.0.1`) için açıktır. Release
> derlemesi HTTPS zorunlu tutar — yayına çıkarken `API_URL` `https://` ile
> başlamalıdır.

## Derleme

```bash
flutter build apk --release --dart-define=API_URL=https://api.ornek.com
flutter build ipa --release --dart-define=API_URL=https://api.ornek.com
```

## Testler ve statik analiz

```bash
flutter analyze
flutter test
```

## Mimari

```
lib/
  core/
    config/      Derleme zamanı yapılandırması (API_URL vb.)
    network/     Dio istemcisi, token ekleme, 401'de sessiz yenileme, hata dönüşümü
    router/      go_router rotaları ve oturum tabanlı yönlendirme
    storage/     Token'ların güvenli depoda (Keychain / Keystore) tutulması
    theme/       Material 3 açık/koyu temalar (web paletiyle hizalı)
  data/
    models/      API yanıtlarının Dart karşılıkları
    repositories/ Endpoint başına veri erişimi
  providers/     Riverpod sağlayıcıları (oturum, bildirimler, tema, veri)
  features/      Ekranlar (auth, home, movies, profile, users, watchlists,
                 messages, events, notifications, settings, subscription, shell)
  widgets/       Paylaşılan bileşenler (poster, avatar, durum görünümleri,
                 uyumluluk etiketi, puanlama alt sayfası)
```

### Oturum yönetimi

1. Giriş/kayıt sonrası `token` + `refreshToken` güvenli depoya yazılır.
2. Her istekte `Authorization: Bearer <token>` eklenir.
3. 401 alınırsa `/api/v1/account/refresh` ile yeni token alınır ve **istek
   otomatik tekrarlanır**; kullanıcı bunu görmez.
4. Eşzamanlı 401'lerde tek bir yenileme isteği atılır (sunucu refresh token
   rotation uyguladığı için ikinci istek geçersiz olurdu).
5. Yenileme de başarısızsa token'lar silinir ve giriş ekranına dönülür.

### Bildirimler

İki kanal birlikte çalışır:

- **Uygulama açıkken:** SignalR (`/notificationHub`) — bildirim listeye anında düşer.
- **Uygulama arka planda/kapalıyken:** FCM push (iOS da FCM → APNs üzerinden).
  Dokununca ilgili ekran açılır (soğuk açılış dahil) ve bildirim okundu işaretlenir.

FCM ön planda sistem bildirimi göstermediği için çift bildirim olmaz.
Oturum açınca cihaz `/api/v1/devices`'a kaydedilir; çıkışta **ve oturum
süresi dolduğunda** kaydı silinir (cihaz önceki kullanıcının bildirimlerini
almasın). Durum *Ayarlar → Anlık bildirimler* satırında görünür.

#### Push'u açmak (Firebase kurulumu)

Firebase yapılandırılmadan da uygulama derlenir ve çalışır; push yalnızca kapalı olur.

1. [Firebase konsolunda](https://console.firebase.google.com) proje oluşturun.
   - Android uygulaması ekleyin: paket adı `com.sineuyum.sine_uyum_mobile`
   - iOS uygulaması ekleyin: bundle id `com.sineuyum.sineUyumMobile`
2. *Proje ayarları → Genel → Uygulamalarınız*'dan değerleri alıp build'e verin
   (`google-services.json` / `GoogleService-Info.plist` **gerekmez**):

   ```bash
   flutter run \
     --dart-define=API_URL=https://api.ornek.com \
     --dart-define=FIREBASE_API_KEY=AIza... \
     --dart-define=FIREBASE_PROJECT_ID=sineuyum-xxxx \
     --dart-define=FIREBASE_MESSAGING_SENDER_ID=1234567890 \
     --dart-define=FIREBASE_ANDROID_APP_ID=1:1234567890:android:abc... \
     --dart-define=FIREBASE_IOS_APP_ID=1:1234567890:ios:def...
   ```

3. **Sunucu:** *Proje ayarları → Hizmet hesapları → Yeni özel anahtar oluştur*
   ile inen JSON'u API'ye `FCM_SERVICE_ACCOUNT_JSON` (içerik) ya da
   `FCM_SERVICE_ACCOUNT_FILE` (yol) olarak verin. Bu anahtar **gizlidir**,
   repoya girmez (`.gitignore`'da). Ayrıntı: `.env.docker.example`.
4. **Yalnızca iOS:** Apple Developer'da bir APNs kimlik doğrulama anahtarı
   (.p8) oluşturup *Proje ayarları → Cloud Messaging → Apple uygulaması*na
   yükleyin. Xcode'da *Runner → Signing & Capabilities → + Capability →
   Push Notifications* ekleyin (ücretli Apple geliştirici hesabı gerekir; bu
   yüzden projede varsayılan açık değil — ücretsiz hesapla cihaz derlemesi
   bozulmasın). Push iOS simülatöründe değil gerçek cihazda test edilir.
5. Doğrulama: uygulamada giriş yapın, *Ayarlar → Anlık bildirimler* "Açık"
   olmalı. Uygulamayı kapatıp başka bir hesaptan mesaj gönderin.

## Bilinen eksikler

Mobil tarafta tamamlanması gereken işler `docs/ANALIZ-VE-YOL-HARITASI.md`
dosyasının "Faz 3 — Mobil tamamlama" bölümünde listelenmiştir. Öne çıkanlar:

- Çevrimdışı cache
- Gerçek zamanlı sohbet (mesajlar şu an ekran yenilendiğinde gelir)
- Uygulama ikonu ve açılış ekranı görselleri (şu an Flutter varsayılanları)
