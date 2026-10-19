# SineUyum — Proje Analizi ve Mobil Yol Haritası

> Bu doküman mevcut kod tabanının (SineUyum.Api + sine-uyum-web) incelenmesi
> sonucu tespit edilen eksikleri ve mobil uygulamaya geçiş için gereken
> adımları listeler.

**İncelenen sürüm:** `claude/loving-carson-a7k46a` (4b5bd97)
**Kapsam:** .NET 8 Web API (SQLite + Identity + JWT + SignalR), React 19 + Vite + MUI web istemcisi

---

## 1. Mevcut Durum Özeti

| Katman | Teknoloji | Durum |
|---|---|---|
| Backend | ASP.NET Core 8, EF Core, Identity, JWT, SignalR | Çalışıyor, 12 controller, 14 entity |
| Veritabanı | SQLite (dosya) | Geliştirme için uygun, production için değil |
| Web | React 19, Vite 5, MUI 7, axios, SignalR client | 14 sayfa, 10 bileşen, 4 context |
| Mobil | — | **Yok** (bu çalışmada Flutter ile eklendi) |
| Test | — | **Yok** (mobil tarafa 21 birim test eklendi; API hâlâ testsiz) |
| CI/CD | — | GitHub Actions eklendi (API derleme + migration kontrolü, web lint/build, mobil format/analiz/test) |
| Docker | — | **Yok** |
| README | Sadece Vite şablonu (`sine-uyum-web/README.md`) | Kök README ve `.env.example` bu çalışmada eklendi |

**Çalışan özellikler:** kayıt/giriş, TMDb film arama ve detay, 1-10 film puanlama,
uyumluluk yüzdesi hesaplama, kullanıcı arama/takip, özel izleme listeleri,
mesajlaşma (film/liste paylaşımı dahil), sinema etkinliği + otomatik grup
eşleştirme + grup içi film oylaması, anlık bildirim (SignalR), admin paneli,
abonelik bayrağı.

---

## 2. Güvenlik Açıkları

Bunlar üretime çıkmadan önce kapatılmalı. Numaralandırma öncelik sırasına göredir.

### G1 — JWT imzalama anahtarı git geçmişindeydi — ✅ temizlendi
`appsettings.json` içinde commit edilmiş bir `Jwt:Key` değeri vardı. Değer
üretilmiş bir gizli anahtar değil, "bunu değiştir" anlamına gelen bir **yer
tutucuydu** — ama uygulama token'ları onunla imzaladığı için onu bilen biri
istediği kullanıcı adına (admin dahil) geçerli token üretebilirdi.

`git filter-repo` ile tüm geçmişten silindi. Anahtar artık yalnızca `JWT_KEY`
ortam değişkeninden okunuyor ve açılışta HS512 için ≥64 byte olduğu
doğrulanıyor, yani eski değer teknik olarak da kullanılamaz.

**Hâlâ yapılması gereken:** Uygulamayı bu anahtarla herhangi bir ortamda
çalıştırdıysanız yeni bir anahtar üretin. Ayrıca force-push, GitHub'ın eski
objelerini hemen yok etmez: eski commit SHA'ları bir süre daha doğrudan URL
ile erişilebilir kalır. Kalıcı silme için GitHub Support'a başvurulmalı.

> TMDb API anahtarı hiç commit edilmemişti (geçmişte her zaman `""`).

### G2 — Veritabanı repoya commit edilmişti — ✅ temizlendi
`SineUyum.Api/sineuyum.db` (266 KB) git'te izleniyordu ve repo **public**'ti.
İçinde 11 kullanıcı kaydı (e-posta adresleri ve parola hash'leri), 20 mesaj,
bildirimler ve puanlamalar vardı. Veriler geliştirme amaçlı test kayıtlarıydı
(`test@gmail.com` gibi tek kullanımlık adresler), yine de herkese açıktı.

SQLite'ın yan dosyaları `sineuyum.db-shm` (32 KB) ve `sineuyum.db-wal` de
commit edilmişti; bunlar da veri sayfası taşıyabildiği için birlikte silindi.

`git filter-repo --invert-paths --path-glob 'SineUyum.Api/sineuyum.db*'` ile
tüm geçmişten çıkarıldı, `*.db` `.gitignore`'a eklendi.

**Hâlâ yapılması gereken:** G1'deki GitHub Support notu bunun için de geçerli.
Bu veritabanındaki hesaplar hâlâ kullanılıyorsa parolaları değiştirilmeli.

### G3 — Sabit kodlanmış admin hesabı (KRİTİK)
`Program.cs:120-128` her açılışta `admin` / `Password123!` hesabını oluşturuyor —
ortam ayrımı yok, yani production'da da oluşur. Repoyu gören herkes admin olur.
**Yapılacak:** kullanıcı adı/parola konfigürasyondan okunmalı, parola yoksa
hesap oluşturulmamalı (bu çalışmada düzeltildi).

### G4 — Ödeme doğrulaması olmayan abonelik
`SubscriptionController.ActivateSubscription` hiçbir ödeme kanıtı istemeden
`IsSubscribed = true` yapıyor. `Iyzipay` ve `Stripe.net` paketleri `.csproj`'a
eklenmiş ama hiç kullanılmıyor. Herhangi bir kullanıcı tek POST isteğiyle
ücretsiz abone olabiliyor. **Yapılacak:** gerçek ödeme sağlayıcı entegrasyonu
+ webhook ile sunucu tarafı doğrulama.

### G5 — Rate limiting yok
`/api/account/login` brute force'a açık; TMDb proxy endpoint'leri (`/api/movies/*`)
sınırsız çağrılabildiği için TMDb kotası tüketilebilir.
**Yapılacak:** ASP.NET Core rate limiting. Bu çalışmada login/register/refresh
uçlarına IP başına dakikada 10 istek sınırı eklendi; kalan uçlar açık.

### G6 — Token iptal edilemiyor, refresh token yok
Token 7 gün geçerli, sunucu tarafı iptal mekanizması yok. `IsSubscribed`
claim'i token'ın içinde olduğu için abonelik iptal edilse bile kullanıcı 7 gün
abone kalır. Çıkış yapmak yalnızca `localStorage`'ı temizliyor.
**Yapılacak:** kısa ömürlü access token + iptal edilebilir refresh token
(bu çalışmada eklendi).

### G7 — Doğrulanmamış film kaydı (over-posting)
`RatingsController.cs:63` `AddMovie(Movie movie)` istemciden gelen `Id`, `Title`,
`PosterPath` değerlerini doğrulamadan kaydediyor. İstemci istediği film kaydını
uydurabilir, var olan TMDb id'lerini sahte başlıklarla doldurabilir.
**Yapılacak:** yalnızca `movieId` alınmalı, meta veri sunucuda TMDb'den çekilmeli.

### G8 — CORS ve JWT issuer/audience sabit kodlanmış
`Program.cs:20` iki Codespaces URL'sini sabit içeriyor; `appsettings.json`'daki
`Jwt:Issuer`/`Audience` de aynı geçici URL. Codespace değiştiğinde proje
çalışmaz; farklı ortamlara deploy edilemez.
**Yapılacak:** konfigürasyona taşındı (bu çalışmada).

### G9 — HTTPS zorlaması ve güvenlik başlıkları yok
`UseHttpsRedirection`, HSTS, güvenlik başlıkları (`X-Content-Type-Options`,
`Referrer-Policy` vb.) yok.

### G10 — Hata detayları dışa sızıyor
`AdminController.TriggerMatching` `ex.Message`'ı doğrudan istemciye döndürüyor;
global exception handler yok, bu yüzden beklenmeyen hatalarda stack trace
sızabilir. **Yapılacak:** `ProblemDetails` tabanlı merkezi hata yönetimi.

### G11 — Parola politikası ve e-posta tekilliği tanımsız
`AddIdentity` varsayılanları kullanılıyor; `RegisterDto` "en az 6 karakter"
diyor ama Identity varsayılanı rakam + büyük/küçük harf + özel karakter de
istiyor → kullanıcıya yanlış mesaj gösteriliyor. `RequireUniqueEmail`
ayarlanmadığı için aynı e-posta ile birden fazla hesap açılabilir.

### G12 — Gizlilik ayarı yok
`ProfileController.GetUserProfile` herhangi bir giriş yapmış kullanıcıya
hedef kullanıcının tüm puanlamalarını ve istatistiklerini açıyor. Gizli hesap
/ görünürlük tercihi kavramı yok.

### G13 — Etkinlik katılımında doğrulama eksik
`EventController.JoinEvent` `eventId`'nin var olup olmadığını ve etkinlik
tarihinin geçmiş olup olmadığını kontrol etmiyor; geçersiz id ile FK hatası,
geçmiş etkinliğe katılım mümkün.

---

## 3. Mimari ve Kod Kalitesi

### M1 — Hiç test yok
Unit, integration veya E2E test bulunmuyor. `Program.cs` sonundaki
`public partial class Program { }` integration test için eklenmiş ama test
projesi hiç oluşturulmamış. Uyumluluk algoritması ve eşleştirme servisi gibi
iş kritik mantık tamamen test edilmemiş durumda.

### M2 — SQLite production'a uygun değil
Tek dosya, eşzamanlı yazmada kilitlenir, yatay ölçeklenemez.
**Yapılacak:** PostgreSQL'e geçiş (`Npgsql.EntityFrameworkCore.PostgreSQL`),
connection string ortam değişkeninden.

### M3 — Anonim obje döndürülen API sözleşmesi
Neredeyse tüm endpoint'ler `new { ... }` anonim obje döndürüyor. Sonuçları:
- Swagger/OpenAPI şeması üretilemiyor → **mobil için otomatik istemci üretimi imkânsız**
- Alan adı değişiklikleri derleme zamanında yakalanmıyor
- `AccountController.cs:115` sıralama için `(dynamic)` kullanmak zorunda kalmış

**Yapılacak:** response DTO'ları tanımlanmalı (bu çalışmada auth/profil
endpoint'leri için başlatıldı, kalanı sürdürülmeli).

### M4 — Uyumluluk algoritması üç yerde kopyalanmış
Aynı ağırlıklı skor hesabı `AccountController.SearchUsers`,
`CompatibilityController` ve `MatchingService.CalculateCompatibility` içinde
birbirinden bağımsız üç kopya halinde duruyor. Biri değişirse diğerleri
sapar. **Yapılacak:** tek bir `ICompatibilityCalculator` servisine çıkarılmalı.

### M5 — N+1 sorgu ve bellek içi işleme
- `AccountController.SearchUsers` (satır 83): her eşleşen kullanıcı için ayrı
  veritabanı sorgusu atıyor. 1000 kullanıcılı aramada 1000 sorgu.
- `MessageController.GetConversations`: kullanıcının **tüm** mesajlarını
  belleğe çekip sonra grupluyor.
- `ProfileController.GetUserProfile`: tüm puanlamaları limitsiz çekiyor.

### M6 — Sayfalama yok
Bildirimler dışında (`Take(50)`) hiçbir listeleme endpoint'inde sayfalama yok.
Mobilde bu doğrudan veri kullanımı ve açılış süresi problemi demek.

### M7 — Eşleştirme servisi kırılgan
`MatchingService.CreateGroupsForEventAsync`:
- `while` döngüsü içinde `SaveChangesAsync` çağırıyor, transaction yok →
  hata halinde yarım oluşmuş gruplar kalıcı oluyor
- Döngü içinde senkron TMDb HTTP çağrısı yapıyor → istek zaman aşımı riski
- O(n²) karşılaştırma, tüm katılımcı puanları belleğe yükleniyor
- `Exception` sınıfını doğrudan fırlatıyor (tipli domain hatası yok)

**Yapılacak:** arka plan işine taşınmalı (Hangfire/Quartz), tek transaction
içinde çalışmalı.

### M8 — `SuggestedMovieIds` virgüllü string
`EventGroup.SuggestedMovieIds` ilişkisel tablo yerine `"1,2,3"` biçiminde
string. `EventController.cs` içinde `Split(',').Select(int.Parse)` bozuk veride
`FormatException` fırlatır; sorgulanamaz, indekslenemez.

### M9 — TMDb yanıtları cache'lenmiyor
`MoviesController` TMDb yanıtlarını olduğu gibi proxy'liyor. Her popüler film
listesi isteği TMDb'ye gidiyor → kota tüketimi, yavaş mobil deneyim.
**Yapılacak:** `IMemoryCache`/Redis ile TTL'li cache.

### M10 — "Okundu" bilgisi hiç yazılmıyor
`Message.DateRead` alanı tanımlı ama hiçbir yerde set edilmiyor.
`MessageController.cs:158` bu alana göre `IsRead` döndürdüğü için gelen her
mesaj sonsuza kadar "okunmamış" görünüyor. Mesajı okundu işaretleyen endpoint
yok.

### M11 — Gerçek zamanlı sohbet yok
SignalR yalnızca bildirim gönderiyor; mesajın kendisi hub üzerinden
iletilmiyor. Sohbet ekranı yeni mesajı görmek için sayfa yenilemeye muhtaç.
`NotificationHub` tamamen boş, grup/oda yönetimi yok.

### M12 — Altyapı eksikleri
- Migration'lar otomatik uygulanmıyor (`Database.Migrate()` yok) → her deploy'da elle
- Health check endpoint'i yok (`/health`)
- Yapılandırılmış loglama yok (Serilog + correlation id)
- Dockerfile / docker-compose yok
- CI yok (`.github/workflows` dizini yok) → derleme ve lint kontrolü yapılmıyor
- Kök dizinde README yok; kurulum adımları hiçbir yerde yazılı değil

### M13 — Frontend eksikleri
- `.env.example` yok; `axiosConfig.js` `localhost:5074`'e sessizce düşüyor
- Error boundary yok → tek render hatası beyaz ekrana düşürüyor
- `NotificationContext` içinde SignalR bağlantısı `showSnackbar` bağımlılığıyla
  yeniden kurulabiliyor; cleanup yalnızca `Connected` durumunu kapatıyor,
  `Connecting` durumunda bağlantı sızıyor
- Yükleniyor / boş durum / hata gösterimleri sayfalar arasında tutarsız
- Kod bölme (lazy route) yok; tek bundle 711 kB
- `react-refresh/only-export-components` kuralı uyarıya çekildi: context
  dosyaları hem provider'ı hem kendi hook'unu export ediyor (yaygın kalıp,
  yalnızca hot reload'u etkiliyor)

---

## 4. Mobil Uygulama İçin Gereken Backend Değişiklikleri

Mobil istemcinin çalışabilmesi için backend'de kapatılması **zorunlu** olan
boşluklar:

| # | Eksik | Neden mobil için zorunlu | Durum |
|---|---|---|---|
| B1 | Refresh token | Mobilde kullanıcı her hafta yeniden giriş yapmak istemez; access token kısa ömürlü olmalı | ✅ Bu çalışmada eklendi |
| B2 | Tipli auth yanıtı (`token`, `refreshToken`, `expiresAt`, `user`) | Dart tarafında güvenli deserializasyon | ✅ Eklendi |
| B3 | Konfigürasyondan JWT issuer/audience | Codespaces URL'sine bağlı token mobil build'de geçersiz olur | ✅ Eklendi |
| B4 | `SubscriptionController` anahtar okuma hatası | `JWT_KEY` env değişkeni kullanıldığında abonelik aktivasyonu `NullReferenceException` ile çöküyordu | ✅ Düzeltildi |
| B5 | `/health` ve `/api/app/config` | Mobil uygulamanın bağlantı kontrolü ve zorunlu güncelleme kontrolü | ✅ Eklendi |
| B6 | Mesaj sayfalama | Mobil veri kullanımı | ✅ `?page`/`pageSize` ile eklendi (varsayılan 100) |
| B7 | Push notification (FCM/APNs) | SignalR mobilde arka planda çalışmaz; uygulama kapalıyken bildirim gitmez | ⬜ Yapılacak |
| B8 | Dosya yükleme endpoint'i | Profil fotoğrafı şu an sadece URL; mobilde galeriden seçim için upload gerekir | ⬜ Yapılacak |
| B9 | API versiyonlama (`/api/v1/...`) | Store'daki eski sürümler API değişince kırılır | ⬜ Yapılacak |
| B10 | Tüm listelerde sayfalama | Performans | ⬜ Yapılacak (B6 kısmi) |

### Bu çalışmada ek olarak kapatılanlar

Mobil için zorunlu olmayan ama yol üstünde olduğu için düzeltilen maddeler:

- **G3** Admin hesabı artık `ADMIN_USERNAME`/`ADMIN_PASSWORD` ile oluşturuluyor;
  parola verilmezse hesap hiç açılmıyor.
- **G2** `*.db` `.gitignore`'a eklendi ve `sineuyum.db` takipten çıkarıldı.
  *(Dosya git geçmişinde hâlâ duruyor — Faz 0'da temizlenmeli.)*
- **G8** CORS origin listesi ve JWT issuer/audience konfigürasyona taşındı.
- **G9** Production'da HSTS + HTTPS yönlendirmesi devrede.
- **G11** Parola politikası `RegisterDto`'daki mesajla hizalandı,
  `RequireUniqueEmail` açıldı.
- **M4** Uyumluluk hesabı tek `CompatibilityCalculator` servisine taşındı;
  üç kopya kaldırıldı.
- **M5** `AccountController.SearchUsers` N+1 sorgusu tek sorguya indirildi,
  `(dynamic)` sıralaması tipli DTO ile değiştirildi, sonuç sayısı sınırlandı.
- **M10** `POST /api/messages/thread/{userId}/mark-as-read` eklendi;
  `DateRead` artık gerçekten yazılıyor.
- **M12** Migration'lar açılışta otomatik uygulanıyor, `/health` eklendi,
  kök README ve `sine-uyum-web/.env.example` yazıldı, GitHub Actions CI
  kuruldu (`.github/workflows/ci.yml`).
- **M13 kısmi** Web lint'i sıfır hataya indirildi: 17 kullanılmayan `catch`
  bağlantısı temizlendi ve **gerçek bir hata** düzeltildi —
  `WatchlistDetailPage` düzenleme modalı var olmayan `setNewListName` /
  `setNewListDesc` fonksiyonlarını çağırıyordu, yani liste adı/açıklaması
  düzenlenemiyordu.
- **G1 kısmi** HS512 için anahtar uzunluğu (≥64 byte) açılışta doğrulanıyor.
  *(Anahtar rotasyonu ve geçmiş temizliği hâlâ yapılmalı.)*

Web istemcisi de access token kısaldığı için refresh token akışına bağlandı
(`axiosConfig.js` + `AuthContext.jsx`); aksi halde web kullanıcıları 1 saat
sonra oturumdan düşerdi.

---

## 5. Mobil Uygulama (Flutter) — Bu Çalışmada Yapılanlar

`sine_uyum_mobile/` dizininde Flutter 3 uygulaması:

**Altyapı**
- Riverpod ile durum yönetimi, `go_router` ile navigasyon ve auth yönlendirme
- `dio` tabanlı API katmanı; interceptor ile otomatik token ekleme ve
  **401'de refresh token ile sessiz yenileme**
- `flutter_secure_storage` ile token saklama (iOS Keychain / Android Keystore)
- Material 3 tema, açık/koyu mod, uygulamanın web tarafındaki renk paletiyle uyumlu
- `signalr_netcore` ile canlı bildirim dinleme
- `--dart-define=API_URL=...` ile derleme zamanı API adresi

**Doğrulama**
- `flutter analyze`: 0 bulgu
- `flutter test`: 21 test geçiyor (API hata ayrıştırma, model deserializasyonu,
  bildirim rota çevirisi)
- Android: release derlemesi için `INTERNET` izni; şifrelenmemiş HTTP yalnızca
  debug derlemesinde ve yalnızca yerel adresler için açık
- iOS: `NSAllowsLocalNetworking` yalnızca yerel ağ için

**Ekranlar**
- Giriş / Kayıt
- Ana sayfa (vizyondakiler + popüler film şeritleri)
- Film arama ve film detay (puanlama, listeye ekleme)
- Profil (istatistikler, puanlanan filmler, takip et/bırak)
- Kullanıcı arama (uyumluluk yüzdesiyle)
- İzleme listeleri ve liste detayı
- Mesajlar ve sohbet
- Etkinlik (durum, katılma, grup üyeleri, film oylaması)
- Bildirimler
- Ayarlar (tema, çıkış)

---

## 6. Önerilen Yol Haritası

### Faz 0 — Güvenlik acil müdahale
1. ✅ JWT anahtarı git geçmişinden silindi (G1)
2. ✅ `sineuyum.db` ve yan dosyaları git geçmişinden silindi (G2)
3. ⬜ **Kalan iş:** daha önce oluşmuş `admin` / `Password123!` hesabının
   parolasını değiştir (G3) — yeni kurulumlarda artık oluşmuyor
4. ⬜ **Kalan iş:** eski commit objelerinin GitHub'dan kalıcı silinmesi için
   GitHub Support'a başvur (force-push objeleri hemen yok etmez)

### Faz 1 — Üretime hazırlık (1-2 hafta)
4. PostgreSQL'e geçiş (M2)
5. Global hata yönetimi + Serilog (G10)
6. Dockerfile (M12) — *CI kuruldu, Docker kalan iş*
7. API için test projesi — önce `CompatibilityCalculator` ve auth akışı (M1)

### Faz 2 — API olgunlaştırma (2-3 hafta)
9. Tüm endpoint'ler için response DTO'ları (M3)
10. Uyumluluk hesabını tek servise çıkar + unit testler (M4, M1)
11. Sayfalama (M6) ve N+1 sorguların düzeltilmesi (M5)
12. TMDb cache (M9)
13. `AddMovie` güvenliği: meta veriyi sunucuda çek (G7)
14. API versiyonlama (B9)

### Faz 3 — Mobil tamamlama (2-3 hafta)
15. FCM/APNs push notification (B7)
16. Dosya yükleme + profil fotoğrafı (B8)
17. Çevrimdışı cache (film listeleri, sohbetler)
18. Gerçek zamanlı sohbet (SignalR üzerinden mesaj iletimi) (M11)
19. Mesaj "okundu" bilgisi (M10)
20. Store yayın hazırlığı: ikonlar, splash, gizlilik politikası, zorunlu güncelleme

### Faz 4 — İş modeli ve ölçekleme
21. Gerçek ödeme entegrasyonu (G4)
22. Eşleştirmeyi arka plan işine taşı (M7)
23. `SuggestedMovieIds` tablosuna normalizasyon (M8)
24. Rate limiting'i tüm API'ye yay (G5)
25. Gizlilik ayarları (G12)
