# SineUyum

An app that matches people by movie taste, with watchlists and messaging, and
that automatically forms groups for cinema events.

| Component | Directory | Technology |
|---|---|---|
| API | `SineUyum.Api/` | .NET 8 Web API, EF Core, Identity, JWT, SignalR |
| Web | `sine-uyum-web/` | React 19, Vite, MUI 7 |
| Mobile | `sine_uyum_mobile/` | Flutter 3, Riverpod, go_router, dio |

Open gaps and the roadmap of the project:
**[`docs/ANALYSIS-AND-ROADMAP.md`](docs/ANALYSIS-AND-ROADMAP.md)**

---

## 1. Running the API

### Required settings

No secrets are kept in the repository. The following are provided as
environment variables or via `dotnet user-secrets`:

| Setting | Required | Description |
|---|---|---|
| `JWT_KEY` | ✅ | JWT signing key. Since HS512 is used, it must be **at least 64 characters**. |
| `TMDB_API_KEY` | ✅ | TMDb API key (for movie search/details/popular). |
| `Jwt__Issuer`, `Jwt__Audience` | — | Validated when set; validation is disabled when empty (local development). |
| `Cors__AllowedOrigins__0` ... | — | Address of the web client. The mobile client is not subject to CORS. |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD`, `ADMIN_EMAIL` | — | If provided, an admin account is created on startup. **If not, none is created.** |
| `Database__Provider` | — | `Sqlite` (default, development) or `Postgres` (production). |
| `ConnectionStrings__DefaultConnection` | — | Sqlite: `Data Source=sineuyum.db`. Postgres: `Host=...;Database=...;Username=...;Password=...`. |

`appsettings.template.json` is the template for all settings.

**Database provider:** SQLite is the default in development; the schema is
generated from the model (no migrations needed). In production, setting
`Database__Provider=Postgres` applies the versioned EF migrations. The
`docker compose` stack uses PostgreSQL. To add a Postgres migration when the
model changes:

```bash
Database__Provider=Postgres \
ConnectionStrings__DefaultConnection="Host=localhost;Database=sineuyum;Username=postgres;Password=postgres" \
JWT_KEY=<64+ characters> \
dotnet ef migrations add <Name> --project SineUyum.Api/SineUyum.Api.csproj
```

### Running

```bash
cd SineUyum.Api

export JWT_KEY="$(openssl rand -base64 64 | tr -d '\n')"
export TMDB_API_KEY="<your-tmdb-key>"
export ADMIN_USERNAME="admin"
export ADMIN_PASSWORD="<a-strong-password>"

dotnet run
```

Migrations are applied automatically on startup. Health check: `GET /health`.
The Swagger UI is only available at `/swagger` in the Development environment.

### Adding a new migration

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <MigrationName> --project SineUyum.Api/SineUyum.Api.csproj
```

> `dotnet ef` commands start the application, so `JWT_KEY` must be set.

### Running with Docker

```bash
cp .env.docker.example .env     # fill in JWT_KEY, TMDB_API_KEY and the admin details
docker compose up --build       # API → http://localhost:5074
```

- The image is multi-stage (built with the SDK, run on the `aspnet` runtime)
  and runs as a **non-root** user.
- Persistent data lives in two volumes and survives container removal:
  - `sineuyum-pgdata` — PostgreSQL database
  - `sineuyum-media` — uploaded profile photos (`/data/media`)
- The `/health` endpoint provides a health check. curl/wget are not bundled
  in the image (to keep it small); the orchestrator should probe over HTTP:
  - **Kubernetes:** `httpGet: { path: /health, port: 8080 }` (no tools needed
    inside the container).
  - For a **docker-compose healthcheck**, `curl` has to be added to the image.

> To only build the image: `docker build -f SineUyum.Api/Dockerfile -t sineuyum-api .`

---

## 2. Running the web client

```bash
cd sine-uyum-web
cp .env.example .env     # edit VITE_API_URL
npm install
npm run dev              # http://localhost:5173
```

The web address must be added to the API's `Cors__AllowedOrigins` list
(`http://localhost:5173` is already configured in the Development environment).

---

## 3. Running the mobile app

```bash
cd sine_uyum_mobile
flutter pub get

# Android emulator
flutter run --dart-define=API_URL=http://10.0.2.2:5074

# iOS simulator
flutter run --dart-define=API_URL=http://localhost:5074
```

Details: [`sine_uyum_mobile/README.md`](sine_uyum_mobile/README.md)

---

## Authentication flow

1. `POST /api/v1/account/login` → `{ token, refreshToken, expiresAt, user }`
2. `token` is short-lived (60 min by default) and sent as `Authorization: Bearer`.
3. On a 401 the client gets a new token via `POST /api/v1/account/refresh` and
   retries the request. The refresh token also changes on every refresh
   (rotation); an old refresh token cannot be reused.
4. `POST /api/v1/account/logout` revokes the refresh token on the server.

Both the web and the mobile clients implement this flow.

### Changing a password

```bash
# 1) Log in and get the token
TOKEN=$(curl -s -X POST "$API/api/v1/account/login" \
  -H 'Content-Type: application/json' \
  -d '{"username":"<user>","password":"<current-password>"}' | jq -r .token)

# 2) Change the password
curl -s -X POST "$API/api/v1/account/change-password" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"currentPassword":"<current-password>","newPassword":"<new-password>"}'
```

When the password changes, **all other sessions of the user are signed out**
(so that a stolen refresh token does not stay valid); a new token pair is
returned for the device that made the request, so that device stays signed in.

---

## Subscription

Joining and voting in events requires an active subscription (the server
returns 402). A subscription is only activated by a verified payment or by an
administrator; no payment provider is connected yet. An administrator can
grant a subscription to a user manually:

```bash
curl -s -X POST "$API/api/v1/admin/users/<userId>/subscription" \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"days": 30}'
```

## Privacy

Each user can toggle the following settings independently (web: Edit Profile >
Privacy, mobile: Settings > Privacy; API: `GET/PUT /api/v1/account/privacy`):

| Setting | Values | Effect |
|---|---|---|
| `profileVisibility` | `Public` · `Following` · `Private` | Who can see ratings, statistics, follow lists and compatibility |
| `messagePermission` | `Everyone` · `Following` · `Nobody` | Who can send new messages |
| `hiddenFromSearch` | `true` / `false` | Not appearing in user search |
| `showReadReceipts` | `true` / `false` | Read receipts ("Seen"); mutual |

`Following` means the people the owner follows. Blocking
(`POST/DELETE /api/v1/blocks/{userId}`, list via `GET /api/v1/blocks`) works
both ways: the two parties cannot see each other's profiles, message or follow
each other, and are not brought together in search or in event groups.

## Rate limits

Every request is limited to 300 requests per minute per user (per IP when not
logged in); movie/TMDb endpoints 60, messages+follows 30, login/registration
10 per IP. When exceeded, `429` + `Retry-After` is returned. The values can be
changed with `RateLimit__GlobalPermitLimit`, `RateLimit__TmdbPermitLimit`,
`RateLimit__SocialWritePermitLimit`, `RateLimit__AuthPermitLimit` and
`RateLimit__UploadPermitLimit`. If the API is behind a reverse proxy,
`TRUSTED_PROXIES` (e.g. `172.18.0.0/16`) must be set; otherwise the per-IP
limits throttle all users together.

## Localization

The apps support multiple languages (Turkish and English to start); this is
being implemented across all three platforms.

- Clients send the selected language in the `Accept-Language` header.
- The API localizes every user-facing message (errors, notifications,
  generated texts, genre names, TMDb data) and falls back to English.
- The user's language is stored (`PUT /api/v1/account/language`), so
  notifications and push messages are rendered in the recipient's language.
- Adding a language means adding one translation file per platform and
  registering the language code in each platform's list of supported
  languages:

  | Platform | Translation file |
  |---|---|
  | API | `SineUyum.Api/Resources/Locales/<lang>.json` |
  | Web | `sine-uyum-web/src/locales/<lang>.json` |
  | Mobile | `sine_uyum_mobile/lib/l10n/app_<lang>.arb` |

  (Exact file names may still change.)

## Tests and CI

On every PR, `.github/workflows/ci.yml` runs three jobs in parallel:

| Job | Steps |
|---|---|
| **API** | `dotnet restore` · `dotnet build --configuration Release` · `dotnet test` · check for model changes without a migration |
| **Web** | `npm ci` · `npm run lint` · `npm run build` |
| **Mobile** | `flutter pub get` · `dart format --set-exit-if-changed` · `flutter analyze` · `flutter test` |

To run the same locally:

```bash
dotnet test SineUyum.Api.sln --configuration Release
cd sine-uyum-web   && npm ci && npm run lint && npm run build
cd sine_uyum_mobile && flutter pub get && dart format --output=none --set-exit-if-changed . \
                    && flutter analyze && flutter test
```

The API tests live under `SineUyum.Api.Tests/`: unit tests for
`CompatibilityCalculator` and end-to-end auth flow tests using
`WebApplicationFactory` + in-memory SQLite. The tests set up their own
configuration (JWT key, admin account, in-memory database); no environment
variables are needed to run them.
