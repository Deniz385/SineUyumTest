# SineUyum

Film zevkine göre insanları eşleştiren, izleme listesi ve mesajlaşma içeren,
sinema etkinliklerinde otomatik grup oluşturan bir uygulama.

| Bileşen | Dizin | Teknoloji |
|---|---|---|
| API | `SineUyum.Api/` | .NET 8 Web API, EF Core, Identity, JWT, SignalR |
| Web | `sine-uyum-web/` | React 19, Vite, MUI 7 |
| Mobil | `sine_uyum_mobile/` | Flutter 3, Riverpod, go_router, dio |

Projedeki açık eksikler ve yol haritası:
**[`docs/ANALIZ-VE-YOL-HARITASI.md`](docs/ANALIZ-VE-YOL-HARITASI.md)**

---

## 1. API'yi çalıştırma

### Gerekli ayarlar

Hiçbir gizli bilgi repoda tutulmaz. Aşağıdakiler ortam değişkeni veya
`dotnet user-secrets` ile verilir:

| Ayar | Zorunlu | Açıklama |
|---|---|---|
| `JWT_KEY` | ✅ | JWT imzalama anahtarı. HS512 kullanıldığı için **en az 64 karakter** olmalı. |
| `TMDB_API_KEY` | ✅ | TMDb API anahtarı (film arama/detay/popüler için). |
| `Jwt__Issuer`, `Jwt__Audience` | — | Tanımlıysa doğrulanır; boşsa doğrulama kapalıdır (yerel geliştirme). |
| `Cors__AllowedOrigins__0` ... | — | Web istemcisinin adresi. Mobil istemci CORS'a tabi değildir. |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD`, `ADMIN_EMAIL` | — | Verilirse açılışta admin hesabı oluşturulur. **Verilmezse oluşturulmaz.** |
| `Database__Provider` | — | `Sqlite` (varsayılan, geliştirme) veya `Postgres` (production). |
| `ConnectionStrings__DefaultConnection` | — | Sqlite: `Data Source=sineuyum.db`. Postgres: `Host=...;Database=...;Username=...;Password=...`. |

`appsettings.template.json` tüm ayarların şablonudur.

**Veritabanı sağlayıcısı:** Geliştirmede varsayılan SQLite'tır; şema modelden
üretilir (migration gerekmez). Production'da `Database__Provider=Postgres`
verilince versiyonlu EF migration'ları uygulanır. `docker compose` yığını
PostgreSQL kullanır. Model değişirse Postgres migration'ı eklemek için:

```bash
Database__Provider=Postgres \
ConnectionStrings__DefaultConnection="Host=localhost;Database=sineuyum;Username=postgres;Password=postgres" \
JWT_KEY=<64+ karakter> \
dotnet ef migrations add <Ad> --project SineUyum.Api/SineUyum.Api.csproj
```

### Çalıştırma

```bash
cd SineUyum.Api

export JWT_KEY="$(openssl rand -base64 64 | tr -d '\n')"
export TMDB_API_KEY="<tmdb-anahtarınız>"
export ADMIN_USERNAME="admin"
export ADMIN_PASSWORD="<güçlü-bir-parola>"

dotnet run
```

Migration'lar açılışta otomatik uygulanır. Sağlık kontrolü: `GET /health`.
Swagger arayüzü yalnızca Development ortamında `/swagger` adresinde açılır.

### Yeni migration eklemek

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <MigrationAdı> --project SineUyum.Api/SineUyum.Api.csproj
```

> `dotnet ef` komutları uygulamayı ayağa kaldırdığı için `JWT_KEY` tanımlı olmalıdır.

### Docker ile çalıştırma

```bash
cp .env.docker.example .env     # JWT_KEY, TMDB_API_KEY, admin bilgilerini doldur
docker compose up --build       # API → http://localhost:5074
```

- İmaj çok aşamalı (SDK ile derlenir, `aspnet` runtime ile çalışır), **root
  olmayan** kullanıcıyla çalışır.
- Kalıcı veri iki volume'dadır; container silinse de kalır:
  - `sineuyum-pgdata` — PostgreSQL veritabanı
  - `sineuyum-media` — yüklenen profil fotoğrafları (`/data/media`)
- `/health` ucu sağlık kontrolü sunar. İmaja curl/wget gömülmedi (küçük
  kalsın diye); orchestrator HTTP ile problamalı:
  - **Kubernetes:** `httpGet: { path: /health, port: 8080 }` (container içi
    araç gerektirmez).
  - **docker-compose healthcheck** istenirse imaja `curl` eklenmeli.

> Yalnızca imajı build etmek: `docker build -f SineUyum.Api/Dockerfile -t sineuyum-api .`

---

## 2. Web istemcisini çalıştırma

```bash
cd sine-uyum-web
cp .env.example .env     # VITE_API_URL değerini düzenleyin
npm install
npm run dev              # http://localhost:5173
```

API tarafında `Cors__AllowedOrigins` listesine web adresinin eklenmesi gerekir
(Development ortamında `http://localhost:5173` zaten tanımlı).

---

## 3. Mobil uygulamayı çalıştırma

```bash
cd sine_uyum_mobile
flutter pub get

# Android emülatörü
flutter run --dart-define=API_URL=http://10.0.2.2:5074

# iOS simülatörü
flutter run --dart-define=API_URL=http://localhost:5074
```

Ayrıntılar: [`sine_uyum_mobile/README.md`](sine_uyum_mobile/README.md)

---

## Kimlik doğrulama akışı

1. `POST /api/v1/account/login` → `{ token, refreshToken, expiresAt, user }`
2. `token` kısa ömürlüdür (varsayılan 60 dk) ve `Authorization: Bearer` ile gönderilir.
3. 401 alındığında istemci `POST /api/v1/account/refresh` ile yeni token alır ve
   isteği tekrarlar. Her yenilemede refresh token da değişir (rotation);
   eski refresh token tekrar kullanılamaz.
4. `POST /api/v1/account/logout` refresh token'ı sunucu tarafında iptal eder.

Web ve mobil istemcilerin ikisi de bu akışı uygular.

### Parola değiştirme

```bash
# 1) Giriş yap, token'ı al
TOKEN=$(curl -s -X POST "$API/api/v1/account/login" \
  -H 'Content-Type: application/json' \
  -d '{"username":"<kullanici>","password":"<mevcut-parola>"}' | jq -r .token)

# 2) Parolayı değiştir
curl -s -X POST "$API/api/v1/account/change-password" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"currentPassword":"<mevcut-parola>","newPassword":"<yeni-parola>"}'
```

Parola değişince kullanıcının **diğer tüm oturumları kapatılır** (çalınmış bir
refresh token geçerli kalmasın diye); isteği yapan cihaz için yeni bir token
çifti döner, yani o cihaz oturumdan düşmez.

---

## Abonelik

Etkinliklere katılım ve oylama aktif abonelik ister (sunucu 402 döner).
Abonelik yalnızca doğrulanmış ödeme ya da yönetici tanımlamasıyla açılır;
ödeme sağlayıcısı henüz bağlanmadı. Yönetici bir kullanıcıya elle abonelik
tanımlayabilir:

```bash
curl -s -X POST "$API/api/v1/admin/users/<kullaniciId>/subscription" \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"days": 30}'
```

## Gizlilik

Her kullanıcı şu ayarları bağımsız açıp kapatabilir (web: Profili Düzenle >
Gizlilik, mobil: Ayarlar > Gizlilik; API: `GET/PUT /api/v1/account/privacy`):

| Ayar | Değerler | Etki |
|---|---|---|
| `profileVisibility` | `Public` · `Following` · `Private` | Puanlar, istatistikler, takip listeleri ve uyum kimlere açık |
| `messagePermission` | `Everyone` · `Following` · `Nobody` | Kim yeni mesaj gönderebilir |
| `hiddenFromSearch` | `true` / `false` | Kullanıcı aramasında görünmeme |

`Following`, sahibin takip ettiği kişiler demektir. Engelleme
(`POST/DELETE /api/v1/blocks/{userId}`, liste `GET /api/v1/blocks`) iki
yönlüdür: taraflar birbirinin profilini göremez, mesajlaşamaz, takip edemez,
aramada ve etkinlik gruplarında bir araya gelmez.

## Testler ve CI

Her PR'da `.github/workflows/ci.yml` üç işi paralel çalıştırır:

| İş | Adımlar |
|---|---|
| **API** | `dotnet restore` · `dotnet build --configuration Release` · `dotnet test` · bekleyen model değişikliği için migration kontrolü |
| **Web** | `npm ci` · `npm run lint` · `npm run build` |
| **Mobil** | `flutter pub get` · `dart format --set-exit-if-changed` · `flutter analyze` · `flutter test` |

Aynısını yerelde çalıştırmak için:

```bash
dotnet test SineUyum.Api.sln --configuration Release
cd sine-uyum-web   && npm ci && npm run lint && npm run build
cd sine_uyum_mobile && flutter pub get && dart format --output=none --set-exit-if-changed . \
                    && flutter analyze && flutter test
```

API testleri `SineUyum.Api.Tests/` altında: `CompatibilityCalculator` için
birim testleri ve `WebApplicationFactory` + in-memory SQLite ile uçtan uca
auth akışı testleri. Testler kendi yapılandırmasını (JWT anahtarı, admin
hesabı, in-memory veritabanı) kurar; çalıştırmak için ortam değişkeni
gerekmez.
