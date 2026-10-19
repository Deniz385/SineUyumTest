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
| `ConnectionStrings__DefaultConnection` | — | Varsayılan: `Data Source=sineuyum.db` (SQLite). |

`appsettings.template.json` tüm ayarların şablonudur.

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

1. `POST /api/account/login` → `{ token, refreshToken, expiresAt, user }`
2. `token` kısa ömürlüdür (varsayılan 60 dk) ve `Authorization: Bearer` ile gönderilir.
3. 401 alındığında istemci `POST /api/account/refresh` ile yeni token alır ve
   isteği tekrarlar. Her yenilemede refresh token da değişir (rotation);
   eski refresh token tekrar kullanılamaz.
4. `POST /api/account/logout` refresh token'ı sunucu tarafında iptal eder.

Web ve mobil istemcilerin ikisi de bu akışı uygular.

---

## Testler ve CI

Her PR'da `.github/workflows/ci.yml` üç işi paralel çalıştırır:

| İş | Adımlar |
|---|---|
| **API** | `dotnet restore` · `dotnet build --configuration Release` · bekleyen model değişikliği için migration kontrolü |
| **Web** | `npm ci` · `npm run lint` · `npm run build` |
| **Mobil** | `flutter pub get` · `dart format --set-exit-if-changed` · `flutter analyze` · `flutter test` |

Aynısını yerelde çalıştırmak için:

```bash
dotnet build SineUyum.Api.sln --configuration Release
cd sine-uyum-web   && npm ci && npm run lint && npm run build
cd sine_uyum_mobile && flutter pub get && dart format --output=none --set-exit-if-changed . \
                    && flutter analyze && flutter test
```

> API tarafında henüz otomatik test yok, bu yüzden CI yalnızca derliyor.
> Test projesi eklendiğinde workflow'daki `api` işine `dotnet test` girecek.
> Bkz. yol haritası → M1.
