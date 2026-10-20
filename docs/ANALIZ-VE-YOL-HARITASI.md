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
çalıştırdıysanız yeni bir anahtar üretin (zaten sadece bir yer tutucuydu, ama
temiz olması için).

> Not: force-push sonrası eski commit objeleri bir süre doğrudan SHA ile
> erişilebilir kalabilir. GitHub Support ile kalıcı silme de düşünüldü, ancak
> GitHub bu hizmeti yalnızca riskin kimlik bilgisi rotasyonuyla
> giderilemediği durumlarda veriyor; buradaki anahtar rotasyonla çözüldüğü
> için başvuru gerekmedi.

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

**Değerlendirme:** Veritabanı tamamen geliştirme sırasında elle girilmiş test
kayıtlarından oluşuyordu — rastgele e-posta adresleri (`aasdfghjk@gmail.com`
gibi), içerik taşımayan mesajlar (`"sa"`, `"as"`), gerçek kişi verisi yok.
Parola hash'leri de ASP.NET Core Identity V3 (HMAC-SHA512, 100.000 iterasyon,
kullanıcı başına salt) ile korunuyordu. Bu nedenle GitHub Support'a başvuru
yapılmadı; dosyanın geçmişten silinmesi ve `*.db`'nin `.gitignore`'a eklenmesi
yeterli görüldü. Bu veritabanındaki `admin` gibi hesaplar başka bir ortamda
hâlâ kullanılıyorsa parolaları yine de değiştirilmeli.

### G3 — Sabit kodlanmış admin hesabı (KRİTİK)
`Program.cs:120-128` her açılışta `admin` / `Password123!` hesabını oluşturuyor —
ortam ayrımı yok, yani production'da da oluşur. Repoyu gören herkes admin olur.
**Yapılacak:** kullanıcı adı/parola konfigürasyondan okunmalı, parola yoksa
hesap oluşturulmamalı (bu çalışmada düzeltildi).

Daha önce `Password123!` ile oluşmuş hesapların parolası hâlâ değiştirilmeli.
Bunun için uygulamada hiçbir yol yoktu; `POST /api/account/change-password`
eklendi (kullanım: kök README → "Parola değiştirme").

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

### G7 — Doğrulanmamış film kaydı (over-posting) — ✅ kapatıldı
`AddMovie(Movie movie)` istemciden gelen `Title`/`PosterPath`'i paylaşılan
Movies tablosuna doğrulamadan yazıyordu; bir istemci gerçek bir TMDb id'sini
sahte başlıkla kaydedip diğer kullanıcıların gördüğü veriyi bozabilirdi.
Çözüm: `MovieCatalogService.EnsureMovieAsync(movieId)` filmi yalnızca TMDb'den
çekilen meta veriyle oluşturuyor; istemci yalnızca `movieId` veriyor.
- `AddRating` artık filmi gerektiğinde kendisi çekiyor (ayrı "önce ekle"
  çağrısına gerek yok).
- `addmovie` ucu geriye dönük uyumlu: istemciden gelen başlık/poster **yok
  sayılıyor**, yalnızca `id` okunuyor.
- TMDb yoksa/ulaşılamazsa DomainException (503/404) döner; detay sızmaz.
- Testler (`MovieOverPostingTests`): sahte başlık yok sayılıyor; `addmovie`
  olmadan doğrudan puanlama da filmi TMDb'den çekiyor.

### G8 — CORS ve JWT issuer/audience sabit kodlanmış
`Program.cs:20` iki Codespaces URL'sini sabit içeriyor; `appsettings.json`'daki
`Jwt:Issuer`/`Audience` de aynı geçici URL. Codespace değiştiğinde proje
çalışmaz; farklı ortamlara deploy edilemez.
**Yapılacak:** konfigürasyona taşındı (bu çalışmada).

### G9 — HTTPS zorlaması ve güvenlik başlıkları yok
`UseHttpsRedirection`, HSTS, güvenlik başlıkları (`X-Content-Type-Options`,
`Referrer-Policy` vb.) yok.

### G10 — Hata detayları dışa sızıyordu — ✅ merkezi hata yönetimi eklendi
`AdminController.TriggerMatching` `ex.Message`'ı doğrudan istemciye döndürüyor,
global exception handler yoktu. Çözüldü:
- `GlobalExceptionHandler` (IExceptionHandler) tüm yakalanmayan istisnaları
  `ProblemDetails`'e çevirir. Beklenen iş kuralı hataları (`DomainException`)
  kendi durum koduyla (400/404) ve güvenli mesajla döner; diğer her şey 500 +
  genel mesaj, ayrıntı yalnızca log'a yazılır. Stack trace artık sızmıyor.
- `DomainException` tipi eklendi; `MatchingService`'teki ham `Exception`
  fırlatmaları buna geçirildi, `AdminController`'daki try/catch kaldırıldı.
- Her yanıtta `traceId` var; aynı id log'da da görünüyor (korelasyon).
- Testler: `ErrorHandlingTests` — 404 ProblemDetails biçimini ve stack
  trace'in sızmadığını doğruluyor.

### G10b — Yapılandırılmış loglama (Serilog) — ✅ eklendi
Önceden yapılandırılmış log ve istek logu yoktu. Serilog eklendi:
konsola yapılandırılmış çıktı, `UseSerilogRequestLogging` ile istek başına özet
(yöntem, yol, durum kodu, süre), seviye `appsettings`'teki `Serilog` bölümünden.
Request logging exception handler'ın dışında, böylece 404/400'e çevrilen
hatalar log'da gerçek durum koduyla görünüyor.

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

### M1 — Test yoktu — ✅ başlangıç paketi eklendi
Önceden hiç test yoktu. `SineUyum.Api.Tests` projesi eklendi (21 test, CI'da
koşuyor):
- **Birim testleri:** `CompatibilityCalculator` — uyumluluk algoritmasının
  davranışı (uç/orta bant ağırlıkları, 0-100 sınırı, ortak film filtresi).
- **Integration testleri:** `WebApplicationFactory` + in-memory SQLite ile
  uçtan uca auth akışı — kayıt, giriş, yanlış parola, `/me`, token yenileme
  (rotation + replay reddi), parola değiştirme (eski/yeni + diğer oturumların
  iptali), admin tohumlama.

**Kalan:** eşleştirme servisi (`MatchingService.CreateGroupsForEventAsync`),
watchlist ve mesaj controller'ları henüz test edilmedi. Kapsam genişletilmeli.

### M2 — SQLite production'a uygun değildi — ✅ çift sağlayıcı eklendi
SQLite tek dosya; eşzamanlı yazmada kilitlenir, yatay ölçeklenemez. Çözüm:
sağlayıcı `Database:Provider` ayarından seçiliyor.
- **Postgres** (production): `Npgsql.EntityFrameworkCore.PostgreSQL`, versiyonlu
  EF migration'ları (`InitialPostgres`), açılışta `MigrateAsync`.
- **SQLite** (varsayılan; geliştirme/test): şema modelden üretilir
  (`EnsureCreatedAsync`), migration tutulmaz.

Neden karışık: EF migration'ları sağlayıcıya özeldir ve tek projede iki ayrı
migration seti tek model snapshot'ı yüzünden çakışır. Veri katmanını ayrı
kütüphaneye bölmek yerine bu yaygın desen seçildi (production migration'lı,
dev EnsureCreated'lı). Her ikisi de tek `ApplicationDbContext` kullanır.

**Doğrulama:** `docker compose up` ile Postgres + API yığını ayağa kaldırıldı;
migration uygulandı (23 tablo, `__EFMigrationsHistory` kaydı), kayıt/giriş
200. SQLite yolu 23 testle doğrulandı. CI'ın migration kontrolü Postgres
sağlayıcısına ayarlandı.

> İleride: SQLite'ı da migration'a almak istenirse veri katmanı ayrı projeye
> çıkarılıp sağlayıcı başına migration projesi kurulmalı (daha ağır, ertelendi).

### M3 — Anonim obje döndürülen API sözleşmesi — ✅ response DTO'larına geçildi
Neredeyse tüm endpoint'ler `new { ... }` anonim obje ya da doğrudan EF varlığı
döndürüyordu: Swagger şeması üretilemiyor, alan adı değişiklikleri derleme
zamanında yakalanmıyordu.

**Yapılan:**
- Tüm JSON uçları açık response DTO'ları ve `ActionResult<T>` döndürüyor
  (`Dtos/*ResponseDtos.cs`, `NotificationDto`). Swagger'da 47 işlemin 42'si
  tipli yanıt şemasına sahip; kalan 5'i TMDb yanıtını olduğu gibi aktaran
  uçlar (şema TMDb'nin, bizim değil).
- **Sözleşme testi:** `ResponseContractTests` gerçekçi bir senaryoyla 54 çağrı
  yapıp her yanıtın şeklini (durum kodu + alan adları + değer türleri)
  `SineUyum.Api.Tests/Contracts/response-shapes.txt` altın dosyasıyla
  karşılaştırır. İstemcinin okuduğu bir alan kazara silinir/yeniden
  adlandırılırsa CI kırılır. Bilinçli değişiklikte
  `UPDATE_CONTRACTS=1 dotnet test --filter ResponseContractTests`.
- Refaktör öncesi ve sonrası şekiller karşılaştırıldı; tek fark aşağıdaki
  bilinçli olarak kaldırılan sızıntı alanları. İstemciler etkilenmedi.

**Yol üstünde bulunan ve düzeltilen hatalar:**
- **Güvenlik — SignalR ile parola hash'i sızıyordu:** mesaj bildirimi
  `Notification` varlığı olarak itiliyordu; alıcı izlenen (tracked) olarak
  yüklendiğinden EF `notification.User`'ı dolduruyor, alıcının `passwordHash`,
  `securityStamp`, `email`, `normalizedEmail` alanları istemciye gidiyordu
  (testle doğrulandı). Artık REST ile aynı `NotificationDto` itiliyor
  (`NotificationPushTests`).
- **Güvenlik — izleme listesine film eklerken over-posting (G7'nin ikizi):**
  film yerelde yoksa istemcinin gönderdiği başlık/poster paylaşılan `Movies`
  tablosuna yazılıyordu. Artık `MovieCatalogService` ile TMDb'den çekiliyor;
  `title`/`posterPath` geriye dönük uyumluluk için kabul edilip yok sayılıyor.
- Eşleştirme bildirimleri kayıttan **önce** itildiği için istemciler `id: 0`
  alıyor, bildirimi okundu işaretleyemiyordu; artık önce kaydediliyor.
- Kaldırılan sızıntı alanları: bildirimlerde `userId`/`user`, liste
  oluşturma yanıtında `userId`/`user`, etkinliklerde hiç yüklenmeyen
  `selectedMovie` navigasyonu (`selectedMovieId` korunuyor).

### M4 — Uyumluluk algoritması üç yerde kopyalanmış — ✅ tek servise alındı
Aynı ağırlıklı skor hesabı `AccountController.SearchUsers`,
`CompatibilityController` ve `MatchingService.CalculateCompatibility` içinde
birbirinden bağımsız üç kopya halindeydi. `CompatibilityCalculator` servisine
çıkarıldı; `SineUyum.Api.Tests` içindeki birim testleri davranışını sabitliyor.

> **İyileştirme adayı (algoritma davranışı):** Skor, maksimum ağırlık 1.5'e göre
> normalize ediliyor; ağırlık yalnızca iki uç bantta (ikisi de ≥8 ya da ≤3)
> 1.5 oluyor. Sonuç: orta bantta (4-7) birebir aynı puan veren iki kullanıcı
> %66.7'den yüksek uyum alamıyor — yalnızca uç puanlarda tam anlaşma %100
> veriyor. Bu muhtemelen istenmeyen bir yan etki; düzeltilirse mevcut testler
> (`AyniPuan_BandaGoreSkorVerir`) güncellenmeli.

### M5 — N+1 sorgu ve bellek içi işleme — ✅ kapatıldı
- ✅ `AccountController.SearchUsers`: N+1 sorgusu daha önce (ilk mobil
  çalışmasında) tek sorguya indirilmişti.
- ✅ `MessageController.GetConversations`: artık kullanıcının tüm mesajlarını
  belleğe çekmiyor. Sohbet başına son mesaj zamanı veritabanında (GroupBy +
  Max) hesaplanıyor, sayfalanıyor, yalnızca o sayfadaki son mesajlar
  ilişkileriyle çekiliyor. Koşullu GroupBy anahtarının hem SQLite hem Postgres'te
  çevrildiği doğrulandı.
- ✅ `ProfileController.GetUserProfile`: sayı ve ortalama veritabanında
  hesaplanıyor; döndürülen puan listesi en yüksek 100 ile sınırlı.

### M6 — Sayfalama yok — ✅ kritik listeler sayfalandı
Başlangıçta yalnızca bildirimler `Take(50)` ile sınırlıydı. Eklenenler:
- Mesaj thread'i: `?page/?pageSize` (ilk mobil çalışmasında).
- Sohbet listesi (`GET /api/messages`): `?page/?pageSize` (varsayılan 30).
- Kullanıcı arama: `limit` (ilk mobil çalışmasında).
- Profil puan listesi: en yüksek 100 ile sınırlı.

Yanıt biçimleri düz dizi olarak korundu (zarf değil), istemciler kırılmadı.
Kalan büyük/büyüyebilir listeler (izleme listesi öğeleri vb.) gerektiğinde
aynı desenle sayfalanabilir.

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

### M9 — TMDb yanıtları cache'lenmiyordu — ✅ IMemoryCache eklendi
`MoviesController` her isteği TMDb'ye proxy'liyordu. Artık dört uç da TTL'li
önbellekte: popüler & vizyondakiler 1 saat, film detayı 24 saat, arama (sorgu
başına) 30 dakika. `SizeLimit=2048` ile farklı sorguların belleği şişirmesi
engellendi; hatalar önbelleğe alınmaz. Test (`MovieCacheTests`) sahte sayaç
handler ile ikinci isteğin TMDb'ye gitmediğini doğruluyor.

> Not: `CompatibilityController` ve `MatchingService` de TMDb çağırıyor; onlar
> bu cache'i henüz kullanmıyor (ayrı iş). Ölçek gerekince Redis'e taşınabilir.

### M10 — "Okundu" bilgisi hiç yazılmıyor
`Message.DateRead` alanı tanımlı ama hiçbir yerde set edilmiyor.
`MessageController.cs:158` bu alana göre `IsRead` döndürdüğü için gelen her
mesaj sonsuza kadar "okunmamış" görünüyor. Mesajı okundu işaretleyen endpoint
yok.

### M11 — Gerçek zamanlı sohbet — ✅ eklendi
SignalR yalnızca bildirim gönderiyordu; sohbet ekranı yeni mesajı görmek için
yeniden açılmaya muhtaçtı.

**Yapılan:**
- **Sunucu:** `POST /api/v1/messages` oluşturulan mesajı döner (önceki
  `{message}` metnini hiçbir istemci okumuyordu) ve aynı mesajı hub üzerinden
  `ReceiveMessage` olayıyla hem alıcıya hem gönderene iter (gönderenin diğer
  sekme/cihazları da güncellensin). Thread listesi, gönderme yanıtı ve canlı
  olay tek bir projeksiyonu paylaşır; şekil eşitliği testle sabit. Gönderme
  REST'te kaldı (doğrulama, güvenilir sonuç); SignalR yalnızca dağıtım yapar.
  Grup/oda gerekmedi: olaylar kullanıcı kimliğine gider.
- **Hub artık kimlik istiyor** (`[Authorize]`): önceden kimliksiz bağlantı da
  kabul ediliyordu (olay almasalar da kaynak tutuyorlardı).
- **İstemciler (web + mobil):** mesaj anında eklenir ve id ile tekilleştirilir
  (gönderen yanıtı + canlı yankı); sohbet açıkken gelen mesaj okundu sayılır,
  o sohbetin bildirimi gösterilmez/okundu işaretlenir; **bağlantı yeniden
  kurulunca thread tazelenir** (kopukken gelenler kaybolmaz); sohbet listesi
  canlı güncellenir. Mobilde ilk yükleme sürerken gelen mesaj da kaybolmaz.

**Yol üstünde bulunan web hataları (eski kodla tarayıcıda yeniden üretildi):**
- **Çift SignalR bağlantısı:** temizlik yalnızca "Connected" durumunda
  durduruyordu; bağlanırken bileşen kaldırılınca (React Strict Mode, kullanıcı
  değişimi) eski bağlantı yaşıyor, her olay iki kez geliyordu (tek mesajda
  rozet 2). Dinleyiciler de start'tan sonra kaydedildiği için ilk olaylar
  kaçabiliyordu.
- **Bir saat sonra zorunlu yeniden giriş:** açılışta süresi dolmuş access
  token "giriş yok" sayılıp login'e yönlendiriliyordu; hiçbir istek
  yapılmadığı için refresh token (60 gün) hiç denenmiyordu. Artık açılışta
  önce yenileme denenir (eşzamanlı çağrılar tek isteğe indirgenir).
- **İlk istek kimliksiz:** axios interceptor'ları AuthContext'in useEffect'inde
  kuruluyordu; React çocuk effect'lerini önce çalıştırdığı için sayfanın ilk
  isteği token'sız gidip 401 alıyor, gereksiz yenileme yapılıyordu. Effect her
  çalıştığında interceptor'lar üst üste de ekleniyordu. Artık modül yüklenirken
  bir kez kurulur.
- Web sohbet sayfası açılınca mesajları okundu işaretlemiyordu; sohbet
  listesindeki "..." kısa mesajlara da ekleniyordu.

**Doğrulama:** gerçek SignalR istemcisiyle API testleri (iki taraf + ikinci
sekme alır, üçüncü kişi almaz; canlı/yanıt/thread şekli aynı; hub kimliksiz
reddeder, sorgu parametresiyle kabul eder — mutasyonla sınandı). İki kullanıcı
ve ayrı tarayıcı bağlamlarıyla web uçtan uca (13 kontrol; çevrimdışı/yeniden
bağlanma dahil); aynı paket eski kodda çift bağlantıyı, zorunlu çıkışı ve
401'i yakaladı. Mobil yığın (gerçek ApiClient + Dart SignalR + sağlayıcılar)
Docker'daki API'ye karşı uçtan uca çalıştırıldı; 9 yeni mobil birim testi.

**İleride:** okundu bilgisi (✓✓) ve "yazıyor..." göstergesi; mesaj
sayfalaması için yukarı kaydırınca eski mesajları yükleme.

### M12 — Altyapı eksikleri — ✅ büyük ölçüde kapatıldı
- ✅ Migration'lar açılışta otomatik uygulanıyor (`Database.MigrateAsync()`)
- ✅ Health check ucu eklendi (`/health`)
- ✅ Yapılandırılmış loglama (Serilog + istek logu + traceId) — G10
- ✅ Dockerfile + docker-compose eklendi (çok aşamalı, root olmayan kullanıcı,
  SQLite kalıcı volume'da). İmaj yerelde build edilip çalıştırılarak doğrulandı:
  `/health` → Healthy, admin login → 200. `.dockerignore` ve
  `.env.docker.example` da eklendi.
- ✅ CI (GitHub Actions): derleme + test + lint
- ✅ Kök README (kurulum/çalıştırma, Docker dahil)

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
| B7 | Push notification (FCM/APNs) | SignalR mobilde arka planda çalışmaz; uygulama kapalıyken bildirim gitmez | ✅ Eklendi (Firebase projesi kurulunca açılır) |
| B8 | Dosya yükleme endpoint'i | Profil fotoğrafı şu an sadece URL; mobilde galeriden seçim için upload gerekir | ✅ Eklendi |
| B9 | API versiyonlama (`/api/v1/...`) | Store'daki eski sürümler API değişince kırılır | ✅ Eklendi (geriye dönük uyumlu) |
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
- **Parola değiştirme** uygulamada hiç yoktu (ne kullanıcı ne admin için).
  `POST /api/account/change-password` eklendi; parola değişince kullanıcının
  diğer tüm oturumları iptal ediliyor.
  **Not:** parola *sıfırlama* (e-posta ile "şifremi unuttum") hâlâ yok;
  e-posta gönderimi altyapısı gerektiriyor.
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

### Faz 0 — Güvenlik acil müdahale ✅
1. ✅ JWT anahtarı git geçmişinden silindi (G1)
2. ✅ `sineuyum.db` ve yan dosyaları git geçmişinden silindi (G2)
3. ✅ Sabit admin hesabı kaldırıldı; `admin`'e bilinen bir parola set edildi,
   değiştirmek için `POST /api/account/change-password` eklendi (G3)
4. ➖ GitHub Support'a başvuru: gerek görülmedi (yukarıda G2 değerlendirmesi).

### Faz 1 — Üretime hazırlık (1-2 hafta)
4. ✅ PostgreSQL'e geçiş — çift sağlayıcı, env ile seçim (M2)
5. ✅ Global hata yönetimi + Serilog (G10)
6. ✅ Dockerfile + docker-compose (M12)
7. ✅ API için test projesi — `CompatibilityCalculator` + auth + hata yönetimi (M1)

### Faz 2 — API olgunlaştırma (2-3 hafta)
9. Tüm endpoint'ler için response DTO'ları (M3) — ✅ (Faz 2 tamamlandı)
10. Uyumluluk hesabını tek servise çıkar + unit testler (M4, M1) — ✅
11. Sayfalama (M6) ve N+1 sorguların düzeltilmesi (M5) — ✅
12. TMDb cache (M9) — ✅
13. `AddMovie` güvenliği: meta veriyi sunucuda çek (G7) — ✅
14. API versiyonlama (B9) — ✅

**B9 — API versiyonlama (`/api/v1/...`):** Tüm controller rotaları tek bir
`ApiV1RouteConvention` ile `api/v1/...` altına alındı (controller başına
`[Route]` düzenlemesi gerekmedi; sürüm tek yerde). Dağıtılmış eski istemcileri
kırmamak için sürümsüz `/api/*` istekleri routing'den önce `/api/v1/*`'e
yönlendiriliyor (geçici deprecation katmanı). Web ve Flutter istemcilerinin
tüm çağrı noktaları açıkça `/api/v1/...`'e taşındı. SignalR hub'ı
(`/notificationHub`), `/health` ve swagger etkilenmedi. Test: `ApiVersioningTests`
(v1 yolu çalışır + sürümsüz yol v1'e yönlenir).

### Faz 3 — Mobil tamamlama (2-3 hafta)
15. FCM/APNs push notification (B7) — ✅

**B7 — Push bildirimi:**
- **Sunucu:** `DeviceToken` tablosu (benzersiz token; aynı cihazda başka
  hesapla girişte kayıt yeni kullanıcıya taşınır; hesap başına 20 cihaz
  sınırı) + `POST /api/v1/devices`, `POST /api/v1/devices/unregister`
  (kimlik doğrulamasız: oturumu düşmüş cihaz da push'u durdurabilsin).
  Tüm bildirimler artık tek yoldan geçer: `NotificationService` → kaydet →
  SignalR → push kuyruğu. Push istek yolunu bekletmez (bellek içi kuyruk +
  `PushDispatchService`); FCM HTTP v1, servis hesabı OAuth'u
  `Google.Apis.Auth` ile. FCM `UNREGISTERED` derse kayıt silinir;
  `INVALID_ARGUMENT` bizim yük hatamız da olabileceği için silmez.
  Push gövdesi mesaj içeriğini içermez (kilit ekranı gizliliği).
  `FCM_SERVICE_ACCOUNT_JSON` verilmezse push kapalı, her şey eskisi gibi.
- **Mobil:** `firebase_messaging`; Firebase seçenekleri `--dart-define` ile
  (yapılandırılmamış derleme çalışmaya devam eder). Giriş/çıkış/oturum
  düşmesi/token yenilemesine göre kayıt; dokununca ilgili ekran.
- **Test:** cihaz kaydı, uçtan uca boru hattı (sahte gönderici) ve gerçek
  `Program.cs` kurulumuyla FCM kabloları: üretilmiş bir servis hesabı
  anahtarı + yerel OAuth dinleyicisiyle Google JWT imzası/kapsamı, FCM v1
  URL/gövde/Bearer ve UNREGISTERED temizliği. Tüm API paketi gerçek
  PostgreSQL'de de geçti (`TEST_POSTGRES_CONNECTION`). Mobil: 12 birim testi.
- **Doğrulanamayan:** gerçek bir Firebase projesine/cihaza gönderim ve
  Android/iOS derlemesi (geliştirme ortamında Android SDK'ya erişim yoktu;
  CI de yalnızca analiz+test yapıyor) — ilk kurulumda cihazda denenmeli.
- **Sonraki adım:** firebase_messaging 17, kayıt token'ı yerine Firebase
  Installation ID (FID; `register()`/`onRegistered`) getirdi ve token
  API'sini deprecated yaptı. FID akışı çok yeni olduğu için cihazda
  doğrulanmadan geçilmedi; FCM v1 `message.token` geçiş süresince FID'i de
  kabul ettiğinden geçiş yalnızca istemci tarafında olacak.
16. Dosya yükleme + profil fotoğrafı (B8) — ✅

**B8 — Profil fotoğrafı yükleme:**
- **Uçlar:** `POST /api/v1/profile/photo` (multipart, alan `file`, ≤10 MB,
  kullanıcı başına dakikada 10) ve `DELETE /api/v1/profile/photo`. Yanıt:
  `{ profileImageUrl: "/media/avatars/{userId}/{rastgele}.jpg" }` — göreli
  yol; istemciler API adresiyle birleştirir (alan adı değişince kayıtlar bozulmaz).
- **Dosya olduğu gibi saklanmaz:** SkiaSharp (MIT) ile çözülür, EXIF yönü
  uygulanır, ortadan kare kırpılıp 512x512 JPEG olarak yeniden kodlanır →
  EXIF/GPS dahil tüm üst veri silinir, resim kılığındaki dosyalar elenir.
  Yalnızca JPEG/PNG/WebP. Bildirilen boyut piksel çözülmeden kontrol edilir
  ("decompression bomb": 50000x50000 bildiren 100 baytlık PNG reddedilir);
  JPEG ölçekli çözüldüğü için 48 MP telefon fotoğrafları geçer.
- **Sunma:** `/media` altında yalnızca `.jpg`, `nosniff`, kalıcı önbellek
  (dosya adı her yüklemede yeni). Klasör dışına çıkış yok. Yeniden yükleme ve
  silme eski dosyaları temizler. Docker: `sineuyum-media` volume (`/data/media`).
- **Davranış değişikliği:** `PUT /api/v1/profile` artık yeni bir harici
  `profileImageUrl` kabul etmiyor (yalnızca mevcut değer ya da boş = kaldır).
  Harici URL, profili görüntüleyen herkesin IP'sini o sunucuya sızdırıyordu
  (izleme pikseli). Mevcut kayıtlar görünmeye devam eder.
- **İstemciler:** web'de yükleme/kaldırma arayüzü; büyük fotoğraflar
  tarayıcıda 1600 px'e küçültülür (53 MB / 48 MP bir fotoğraf 1,4 MB'a inip
  yüklendi). Mobilde galeri/kamera (image_picker; iOS'ta HEIC → JPEG).
- **Yol üstünde düzeltilenler:** Kestrel'in reddettiği istekler (gövde çok
  büyük vb.) 500 "sunucu hatası" dönüyordu → kendi durum kodu. Beklenen iş
  kuralı hataları (404/413/415) ExceptionHandlerMiddleware tarafından ERR +
  stack trace ile loglanıyordu → kopya log kapatıldı. Rate limiter kimlik
  doğrulamadan önce çalıştığı için kullanıcıya göre sınır kurulamıyordu →
  sıra düzeltildi.
- **Doğrulama:** 32 yeni API testi (8 EXIF yönünün hepsi bağımsız tabloyla,
  mutasyonla sınandı; üst veri temizliği; bomba; sınırlar; sunma; yol
  aşımı). Üretim Docker imajında gerçek Kestrel ile: SkiaSharp yerel kitaplığı
  çalışıyor, Pillow ile üretilmiş GPS'li 12 MP fotoğraf temiz 512x512 çıktı,
  413 hem Content-Length'li hem chunked istekte, konteyner yeniden
  başlatılınca dosya kalıcı. Web akışı gerçek Chromium'da uçtan uca.
  Mobil: 7 yeni test (widget + birim).
- **İleride:** dosyalar yerel diskte (`IMediaStorage` arayüzü); birden çok
  sunucuya ölçeklenince S3/Blob + CDN'e taşınmalı.
17. Çevrimdışı cache (film listeleri, sohbetler) — ✅ (Faz 3 tamamlandı)

**Çevrimdışı kullanım (mobil):**
- **Okuma önbelleği** `ApiClient` katmanında: izinli GET uçları (profil,
  sohbet listesi ve mesajlar, izleme listeleri, bildirimler, etkinlik,
  popüler/vizyondaki filmler, film detayı) her başarılı yanıtta saklanır;
  ağ hatasında son kopya döner. Cihaz çevrimdışıysa ağ hiç beklenmez (15 sn
  zaman aşımı yok). Sunucunun verdiği yanıtlar (403/404) önbellekle
  örtülmez; arama/öneri saklanmaz. Film afişleri zaten
  `cached_network_image` ile diskte.
- **Gizlilik:** önbellekte aynı anda tek kullanıcı; başka hesapla girişte,
  çıkışta ve oturum düşünce silinir. İşletim sisteminin önbellek dizininde
  (yedeklemeye girmez), en fazla 300 kayıt. Önbellek hatası (disk dolu)
  hiçbir isteği/girişi bozmaz.
- **Ekranda:** tüm ekranların altında "Çevrimdışısın / Sunucuya
  ulaşılamıyor — kayıtlı veriler gösteriliyor" şeridi; ağ var ama internet
  yoksa (giriş yapılmamış otel Wi-Fi'ı) da gösterilir.

**Yol üstünde bulunan, çevrimdışını bozan hatalar (testle yeniden üretildi):**
- **İnternetsiz açılış oturumu kapatıyordu:** açılıştaki `/me` her hatada
  token'ları siliyordu; metroda uygulamayı açan kullanıcı yeniden giriş
  yapmak zorundaydı. Artık yalnızca sunucunun kesin reddi oturumu kapatır;
  ağ yoksa kayıtlı profille açılır ya da açılış ekranında bekleyip ağ
  dönünce devam eder.
- **Token yenilerken ağ kopması "oturum bitti" sayılıyordu:** refresh
  isteği sunucuya ulaşamayınca da token'lar siliniyordu. Artık yalnızca
  sunucu reddederse; aksi halde çağırana 401 değil ağ hatası gider.
- **SignalR kalıcı ölüyordu:** açılışta bağlanamazsa bir daha denenmiyordu;
  otomatik yeniden bağlanma ~40 sn sonra vazgeçince de bağlantı kapalı
  kalıyordu — canlı bildirim/sohbet uygulama yeniden açılana dek gelmiyordu.
  Artık kapanınca 5/15/30/60 sn aralıklarla yeniden denenir, ağ dönünce
  hemen bağlanır ve kaçanlar tazelenir. Push cihaz kaydı da ağ dönünce
  yeniden denenir.
- `/me` önbelleğe ancak üçüncü açılışta giriyordu (kullanıcı kimliği
  `/me`'nin yanıtından öğreniliyor); artık giriş/açılışta açıkça saklanır.

**Doğrulama:** 19 yeni mobil test (oturum + önbellek + dosya deposu +
şerit; iki ana düzeltme mutasyonla sınandı), toplam 68. Gerçek mobil yığın
Docker'daki API'ye karşı: sunucu 50 sn kapatıldı → veriler önbellekten,
şerit açık; sunucu dönünce istemci kendiliğinden bağlandı, kesintide
gönderilen mesaj geldi, sonraki canlı mesaj 124 ms'de düştü. Aynı senaryo
eski SignalR koduyla zaman aşımına düştü (mesaj hiç gelmedi).

**İleride:** çevrimdışı yazma (mesaj gönderme kuyruğu / outbox). Şimdilik
çevrimdışı gönderim anlaşılır bir hata verir. Web istemcisinde çevrimdışı
destek yok (PWA ayrı bir iş).
18. Gerçek zamanlı sohbet (SignalR üzerinden mesaj iletimi) (M11) — ✅
19. Mesaj "okundu" bilgisi (M10)
20. Store yayın hazırlığı: ikonlar, splash, gizlilik politikası, zorunlu güncelleme

### Faz 4 — İş modeli ve ölçekleme
21. Gerçek ödeme entegrasyonu (G4)
22. Eşleştirmeyi arka plan işine taşı (M7)
23. `SuggestedMovieIds` tablosuna normalizasyon (M8)
24. Rate limiting'i tüm API'ye yay (G5)
25. Gizlilik ayarları (G12)
