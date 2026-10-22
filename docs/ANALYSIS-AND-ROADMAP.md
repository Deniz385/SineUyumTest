# SineUyum — Project Analysis and Mobile Roadmap

> This document lists the gaps found while reviewing the existing code base
> (SineUyum.Api + sine-uyum-web) and the steps required to move to a mobile
> app.

**Reviewed version:** `claude/loving-carson-a7k46a` (4b5bd97)
**Scope:** .NET 8 Web API (SQLite + Identity + JWT + SignalR), React 19 + Vite + MUI web client

---

## 1. Current State Summary

| Layer | Technology | Status |
|---|---|---|
| Backend | ASP.NET Core 8, EF Core, Identity, JWT, SignalR | Working, 12 controllers, 14 entities |
| Database | SQLite (file) | Fine for development, not for production |
| Web | React 19, Vite 5, MUI 7, axios, SignalR client | 14 pages, 10 components, 4 contexts |
| Mobile | — | **None** (added with Flutter in this work) |
| Test | — | **None** (21 unit tests added on the mobile side; the API still has no tests) |
| CI/CD | — | GitHub Actions added (API build + migration check, web lint/build, mobile format/analyze/test) |
| Docker | — | **None** |
| README | Only the Vite template (`sine-uyum-web/README.md`) | Root README and `.env.example` added in this work |

**Working features:** registration/login, TMDb movie search and details, 1-10 movie
ratings, compatibility percentage calculation, user search/follow, custom
watchlists, messaging (including movie/list sharing), cinema events + automatic
group matching + in-group movie voting, real-time notifications (SignalR),
admin panel, subscription flag.

---

## 2. Security Vulnerabilities

These must be closed before going to production. The numbering follows priority.

### G1 — The JWT signing key was in the git history — ✅ cleaned up
A `Jwt:Key` value had been committed in `appsettings.json`. The value was not a
generated secret but a **placeholder** meaning "change this" — but since the
application signed tokens with it, anyone who knew it could create valid
tokens for any user (including admin).

It was removed from the entire history with `git filter-repo`. The key is now
read only from the `JWT_KEY` environment variable and validated on startup to
be ≥64 bytes for HS512, so the old value cannot technically be used either.

**Still to do:** if you ran the application with this key in any environment,
generate a new key (it was only a placeholder anyway, but for the sake of
cleanliness).

> Note: after a force-push, old commit objects may remain directly accessible
> by SHA for a while. Permanent removal via GitHub Support was also
> considered, but GitHub only offers this service when the risk cannot be
> mitigated by rotating credentials; since the key here was resolved by
> rotation, no request was needed.

> The TMDb API key was never committed (always `""` in the history).

### G2 — The database had been committed to the repository — ✅ cleaned up
`SineUyum.Api/sineuyum.db` (266 KB) was tracked in git and the repository was
**public**. It contained 11 user records (email addresses and password
hashes), 20 messages, notifications and ratings. The data consisted of test
records for development (throwaway addresses such as `test@gmail.com`), but
it was publicly accessible nonetheless.

SQLite's side files `sineuyum.db-shm` (32 KB) and `sineuyum.db-wal` had also
been committed; since these can carry data pages too, they were removed along
with it.

It was removed from the entire history with
`git filter-repo --invert-paths --path-glob 'SineUyum.Api/sineuyum.db*'`, and
`*.db` was added to `.gitignore`.

**Assessment:** the database consisted entirely of test records entered by hand
during development — random email addresses (such as `aasdfghjk@gmail.com`),
messages without content (`"sa"`, `"as"`), no real personal data. The password
hashes were also protected by ASP.NET Core Identity V3 (HMAC-SHA512, 100,000
iterations, per-user salt). Therefore no request was made to GitHub Support;
removing the file from the history and adding `*.db` to `.gitignore` was
considered sufficient. If accounts from this database such as `admin` are
still used in another environment, their passwords should be changed anyway.

### G3 — Hard-coded admin account (CRITICAL)
`Program.cs:120-128` creates the `admin` / `Password123!` account on every
startup — with no environment distinction, so it is created in production too.
Anyone who sees the repository becomes admin.
**To do:** the username/password should be read from configuration, and the
account should not be created when no password is given (fixed in this work).

The passwords of accounts previously created with `Password123!` must still be
changed. There was no way to do that in the application;
`POST /api/account/change-password` was added (usage: root README → "Changing
a password").

### G4 — Subscription without payment verification — ✅ hole closed, infrastructure ready
`ActivateSubscription` set `IsSubscribed = true` without asking for proof of
payment: anyone could subscribe for free with a single request. In addition:
- **The paid feature was only hidden on the client:** event participation/voting
  was not checked on the server at all (and on mobile it was not even hidden on
  the client); anyone calling the API directly could join without paying.
- **Subscriptions never ended:** `SubscriptionExpires` was written but never
  compared with the current date anywhere.
- The mobile screen listed features that did not exist ("unlimited watchlists",
  "priority in matching") as subscription benefits.

**Done:**
- The `Subscriptions` table (provider, provider reference, status, period end)
  is the single source; `AppUser.IsSubscribed/SubscriptionExpires` were removed.
  The subscription is computed from the database every time, **taking the end
  date into account**. (The "subscriptions" in the old flag were not migrated,
  since they were never paid.)
- `[RequiresSubscription]`: joining an event and voting return **402** without an
  active subscription (except for administrators). The JWT claim is for the UI
  only.
- Provider-agnostic `IPaymentProvider` (verification + signed webhook) and
  endpoints: `GET /api/v1/subscription`, `POST /api/v1/subscription/verify`
  (verified with the provider, returns a new token), `POST /api/v1/subscription/webhooks/{provider}`
  (renewal / cancellation → valid until the end of the period / refund → ends
  immediately). The same payment cannot activate a second account (409).
  `activate` → **410 Gone**.
- An administrator can grant a subscription manually: `POST /api/v1/admin/users/{id}/subscription {days}`
  (promotions, support; trying out premium features before the payment
  integration).
- The unused `Iyzipay` and `Stripe.net` packages were removed (to be added in
  the real integration).
- G13 was closed as well: joining a non-existent event returns 404, a past event 400.
- Clients: the "test subscription" buttons were removed, the status is read
  from the server (refreshed if the token is stale), and a 402 redirects to the
  subscription page; the mobile benefits list was corrected to the real
  features.

**Remaining (requires payment account details):** real providers — iyzico for
web (payment form + server-side verification + webhook signature), App Store /
Google Play subscription verification and server notifications for mobile.
Each of them is just an `IPaymentProvider` class + a registration line.

### G5 — No rate limiting — ✅ closed
`/api/account/login` is open to brute force; the TMDb proxy endpoints
(`/api/movies/*`) can be called without limit, so the TMDb quota can be
exhausted.
**To do:** ASP.NET Core rate limiting. In this work a limit of 10 requests per
minute per IP was added to the login/register/refresh endpoints; the other
endpoints are open.

**Done:** `RateLimitingSetup` (per minute, `RateLimit:*` settings):
- **Global limit** on every request: 300 per user when logged in, per IP
  otherwise. `/health` and swagger are exempt.
- Strict policies (applied together with the global limit): `tmdb` 60 (movie
  endpoints, shared recommendations), `social-write` 30 (messages + follows;
  both generate notifications, so they share the quota), `auth` 10 (IP),
  `upload`/`payment` 10.
- Rejection: 429 + ProblemDetails (same format as other errors) + `Retry-After`.
- **Reverse proxy:** when `TRUSTED_PROXIES` (IP/CIDR) is set, `X-Forwarded-For`
  is accepted only from those proxies; otherwise all users would share the
  proxy's IP and be throttled together. A pitfall found in testing: in ASP.NET,
  if the trusted list is empty the header is accepted **from anyone** (a client
  could write a different IP on every request and bypass the limit); when no
  proxy is defined, the header is not processed at all. An invalid value fails
  on startup.

### G6 — Tokens cannot be revoked, no refresh token
The token is valid for 7 days and there is no server-side revocation
mechanism. Since the `IsSubscribed` claim is inside the token, the user stays
subscribed for 7 days even if the subscription is cancelled. Logging out only
clears `localStorage`.
**To do:** short-lived access token + revocable refresh token (added in this
work).

### G7 — Unvalidated movie records (over-posting) — ✅ closed
`AddMovie(Movie movie)` wrote the `Title`/`PosterPath` sent by the client into
the shared Movies table without validation; a client could save a real TMDb id
with a fake title and corrupt the data other users see.
Solution: `MovieCatalogService.EnsureMovieAsync(movieId)` creates the movie only
from metadata fetched from TMDb; the client only provides `movieId`.
- `AddRating` now fetches the movie itself when needed (no separate "add
  first" call needed).
- The `addmovie` endpoint stays backward compatible: the title/poster sent by
  the client are **ignored**, only `id` is read.
- If TMDb is missing/unreachable, a DomainException (503/404) is returned; no
  details leak.
- Tests (`MovieOverPostingTests`): a fake title is ignored; rating directly
  without `addmovie` also fetches the movie from TMDb.

### G8 — CORS and JWT issuer/audience hard-coded
`Program.cs:20` contains two hard-coded Codespaces URLs; `Jwt:Issuer`/`Audience`
in `appsettings.json` are the same temporary URL. When the Codespace changes,
the project stops working; it cannot be deployed to different environments.
**To do:** moved to configuration (in this work).

### G9 — No HTTPS enforcement and no security headers — ✅ closed
No `UseHttpsRedirection`, HSTS or security headers (`X-Content-Type-Options`,
`Referrer-Policy` etc.).

**Done:** HTTPS redirection and HSTS were already enabled in production. Every
response (including errors and 429) now gets `X-Content-Type-Options: nosniff`,
`X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`,
`Cross-Origin-Opener-Policy` and a strict CSP (`default-src 'none'`) suited to
an API that only returns JSON; the Swagger UI in development is exempt from the
CSP. The `Server: Kestrel` header, which revealed the server software, was
removed. Verified on the production image. The web app's own headers belong to
the server that serves it (nginx etc.).

### G10 — Error details were leaking — ✅ central error handling added
`AdminController.TriggerMatching` returned `ex.Message` directly to the client,
and there was no global exception handler. Resolved:
- `GlobalExceptionHandler` (IExceptionHandler) converts all unhandled exceptions
  into `ProblemDetails`. Expected business-rule errors (`DomainException`) are
  returned with their own status code (400/404) and a safe message; everything
  else becomes 500 + a generic message, with details written only to the log.
  Stack traces no longer leak.
- A `DomainException` type was added; the raw `Exception` throws in
  `MatchingService` were switched to it, and the try/catch in `AdminController`
  was removed.
- Every response has a `traceId`; the same id also appears in the log
  (correlation).
- Tests: `ErrorHandlingTests` — verifies the 404 ProblemDetails format and that
  the stack trace does not leak.

### G10b — Structured logging (Serilog) — ✅ added
Previously there was no structured logging and no request logging. Serilog was
added: structured console output, a per-request summary with
`UseSerilogRequestLogging` (method, path, status code, duration), and the level
from the `Serilog` section in `appsettings`. Request logging sits outside the
exception handler, so errors converted to 404/400 appear in the log with their
real status code.

### G11 — Password policy and email uniqueness undefined
`AddIdentity` defaults are used; `RegisterDto` says "at least 6 characters" but
the Identity default also requires a digit + upper/lower case + a special
character → the user is shown a wrong message. Since `RequireUniqueEmail` is not
set, multiple accounts can be created with the same email.

### G12 — No privacy settings — ✅ closed
`ProfileController.GetUserProfile` exposes all of the target user's ratings and
statistics to any logged-in user. There is no concept of a private account /
visibility preference.

**Done:** Four settings, each toggled independently (the defaults match the old
behaviour; existing accounts start out that way via the migration). The rules
live in one place, `PrivacyService`; clients only display them.
- **Profile visibility** (`Public` / `Following` / `Private`): ratings,
  statistics, follow lists and compatibility (since it is derived from ratings)
  are hidden; name, photo and bio stay visible. "Following" = the people the
  owner follows: since following requires no approval, "my followers" would not
  offer any protection. Administrators see everything.
- **Message permission** (`Everyone` / `Following` / `Nobody`): only blocks new
  messages (403, with a reason); past conversations can still be read.
- **Hidden from search:** removed from user search; the profile link still works.
- **Blocking** (`/api/v1/blocks`): works both ways. The blocked user cannot see
  the blocker at all (404); the parties cannot message or follow each other,
  cannot find each other in search, and do not end up in the same group in
  event matching. Mutual follows are deleted and do not come back when the
  block is lifted.
- Matching: when there are blocks, the person with the most conflicts is placed
  first; otherwise users who blocked each other were left to the end and stayed
  outside the groups even when a valid assignment existed (caught in testing).
- Along the way: following a non-existent user returned 500 due to an FK error
  (now 404).
- Web: Edit Profile > Privacy; Block / Unblock on the profile. Mobile:
  Settings > Privacy; Block in the profile menu.

**Remaining:** the shared watchlist link (`watchlist/public/{id}`) is independent
of the privacy settings; a list is deliberately shared via a message.

### G13 — Missing validation when joining events — ✅ closed (with G4)
`EventController.JoinEvent` does not check whether the `eventId` exists or
whether the event date has passed; an invalid id causes an FK error, and joining
a past event is possible.

---

## 3. Architecture and Code Quality

### M1 — There were no tests — ✅ starter suite added
Previously there were no tests at all. The `SineUyum.Api.Tests` project was
added (21 tests, running in CI):
- **Unit tests:** `CompatibilityCalculator` — the behaviour of the compatibility
  algorithm (extreme/middle band weights, 0-100 bounds, common movie filter).
- **Integration tests:** end-to-end auth flow with `WebApplicationFactory` +
  in-memory SQLite — registration, login, wrong password, `/me`, token refresh
  (rotation + replay rejection), password change (old/new + revoking the other
  sessions), admin seeding.

**Remaining:** the matching service (`MatchingService.CreateGroupsForEventAsync`),
and the watchlist and message controllers are not tested yet. Coverage should
be expanded.

### M2 — SQLite was not suitable for production — ✅ dual provider added
SQLite is a single file; it locks under concurrent writes and cannot scale
horizontally. Solution: the provider is selected from the `Database:Provider`
setting.
- **Postgres** (production): `Npgsql.EntityFrameworkCore.PostgreSQL`, versioned
  EF migrations (`InitialPostgres`), `MigrateAsync` on startup.
- **SQLite** (default; development/test): the schema is generated from the model
  (`EnsureCreatedAsync`), no migrations are kept.

Why mixed: EF migrations are provider-specific, and two separate migration sets
in one project conflict because of the single model snapshot. Instead of
splitting the data layer into a separate library, this common pattern was
chosen (production with migrations, dev with EnsureCreated). Both use a single
`ApplicationDbContext`.

**Verification:** the Postgres + API stack was brought up with `docker compose up`;
the migration was applied (23 tables, `__EFMigrationsHistory` entry),
registration/login 200. The SQLite path was verified with 23 tests. The CI
migration check was set to the Postgres provider.

> Later: if SQLite should also use migrations, the data layer should be moved to
> a separate project with a migration project per provider (heavier, postponed).

### M3 — API contract returning anonymous objects — ✅ moved to response DTOs
Almost all endpoints returned a `new { ... }` anonymous object or an EF entity
directly: no Swagger schema could be generated, and field name changes were not
caught at compile time.

**Done:**
- All JSON endpoints return explicit response DTOs and `ActionResult<T>`
  (`Dtos/*ResponseDtos.cs`, `NotificationDto`). In Swagger, 42 of the 47
  operations have a typed response schema; the remaining 5 are endpoints that
  pass the TMDb response through as is (the schema is TMDb's, not ours).
- **Contract test:** `ResponseContractTests` makes 54 calls in a realistic
  scenario and compares the shape of every response (status code + field names
  + value types) with the golden file
  `SineUyum.Api.Tests/Contracts/response-shapes.txt`. If a field the client
  reads is accidentally removed/renamed, CI breaks. For a deliberate change:
  `UPDATE_CONTRACTS=1 dotnet test --filter ResponseContractTests`.
- The shapes before and after the refactor were compared; the only difference is
  the leaking fields deliberately removed below. Clients were not affected.

**Bugs found and fixed along the way:**
- **Security — the password hash was leaking via SignalR:** the message
  notification was pushed as a `Notification` entity; since the recipient was
  loaded as tracked, EF populated `notification.User`, and the recipient's
  `passwordHash`, `securityStamp`, `email` and `normalizedEmail` fields were
  sent to the client (verified with a test). Now the same `NotificationDto` as
  in REST is pushed (`NotificationPushTests`).
- **Security — over-posting when adding a movie to a watchlist (G7's twin):**
  if the movie did not exist locally, the title/poster sent by the client was
  written into the shared `Movies` table. Now it is fetched from TMDb via
  `MovieCatalogService`; `title`/`posterPath` are accepted for backward
  compatibility and ignored.
- Matching notifications were pushed **before** being saved, so clients received
  `id: 0` and could not mark the notification as read; they are now saved first.
- Removed leaking fields: `userId`/`user` in notifications, `userId`/`user` in
  the list creation response, and the never-loaded `selectedMovie` navigation in
  events (`selectedMovieId` is kept).

### M4 — The compatibility algorithm was copied in three places — ✅ moved into a single service
The same weighted score calculation existed as three independent copies in
`AccountController.SearchUsers`, `CompatibilityController` and
`MatchingService.CalculateCompatibility`. It was extracted into the
`CompatibilityCalculator` service; the unit tests in `SineUyum.Api.Tests` pin
its behaviour.

> **Improvement candidate (algorithm behaviour):** the score is normalized
> against the maximum weight of 1.5; the weight is 1.5 only in the two extreme
> bands (both ≥8 or both ≤3). Result: two users who give exactly the same rating
> in the middle band (4-7) cannot get more than 66.7% compatibility — only full
> agreement on extreme ratings gives 100%. This is probably an unintended side
> effect; if it is fixed, the existing tests (`SameRating_ScoresByBand`)
> must be updated.

### M5 — N+1 queries and in-memory processing — ✅ closed
- ✅ `AccountController.SearchUsers`: the N+1 query had already been reduced to a
  single query earlier (in the first mobile work).
- ✅ `MessageController.GetConversations`: no longer loads all of the user's
  messages into memory. The last message time per conversation is computed in
  the database (GroupBy + Max) and paginated, and only the last messages on that
  page are fetched with their relations. It was verified that the conditional
  GroupBy key is translated on both SQLite and Postgres.
- ✅ `ProfileController.GetUserProfile`: count and average are computed in the
  database; the returned rating list is limited to the top 100.

### M6 — No pagination — ✅ critical lists paginated
Initially only notifications were limited with `Take(50)`. Added:
- Message thread: `?page/?pageSize` (in the first mobile work).
- Conversation list (`GET /api/messages`): `?page/?pageSize` (default 30).
- User search: `limit` (in the first mobile work).
- Profile rating list: limited to the top 100.

The response formats were kept as plain arrays (not envelopes), so clients did
not break. The remaining large/growing lists (watchlist items etc.) can be
paginated with the same pattern when needed.

### M7 — The matching service is fragile — ✅ closed
`MatchingService.CreateGroupsForEventAsync`:
- calls `SaveChangesAsync` inside a `while` loop with no transaction → on
  failure, half-created groups are persisted
- makes synchronous TMDb HTTP calls inside the loop → risk of request timeouts
- O(n²) comparison, all participant ratings are loaded into memory
- throws the `Exception` class directly (no typed domain error)

**To do:** it should be moved to a background job (Hangfire/Quartz) and run in
a single transaction.

**Done:** a database-backed queue instead of an extra library (Hangfire/Quartz):
- `POST /admin/events/{id}/create-groups` performs the pre-checks immediately
  (event missing 404, groups exist / too few participants 400, job running 409),
  writes a job to `MatchingRuns` and returns **202**; the result is at
  `GET /admin/events/{id}/matching` (Queued → Running → Succeeded / Failed, with
  the number of groups and of people without a group).
- `MatchingWorker` (BackgroundService) **claims the job atomically** (multiple
  server instances do not pick up the same job); the queue is not lost on
  restart, and a job that has been "Running" for 10 minutes is picked up again.
- Groups are built in memory first, recommendations (TMDb) are computed
  **outside** the transaction, and all groups + recommendations + notifications
  are written in a **single transaction**; live notifications/push go out after
  the commit. Previously a TMDb error in the second group left the first group
  and its notifications persisted, and a retry was rejected with "groups already
  created" (exactly this was caught when the old behaviour was put back in a
  test).
- Only a safe message is stored in the failure record; details go to the log.
  The web admin page waits for the result and shows it. The old `message` field
  is kept in the response.

**Remaining:** the O(n²) compatibility comparison remains; for an event with
thousands of participants it makes the job longer but no longer blocks the
request.

### M8 — `SuggestedMovieIds` as a comma-separated string — ✅ closed
`EventGroup.SuggestedMovieIds` is a string in the form `"1,2,3"` instead of a
relational table. `Split(',').Select(int.Parse)` in `EventController.cs` throws
a `FormatException` on corrupt data; it cannot be queried or indexed.

**Done:** the `EventGroupSuggestions` table (group, movie, order; movie with FK,
removed when the group is deleted). The migration moves the existing values in
order; non-numeric parts, non-existent movies and duplicates are skipped
(up/down tested on Postgres with corrupt data). The response shape is the same:
`suggestedMovies` is now in recommendation order, and `group.suggestedMovieIds`
is generated from the table for old clients.
- Votes can only be cast for movies recommended to the group (previously any id;
  a non-existent movie gave a 500 due to an FK error).
- Along the way: the text in `Forbid("message")` was taken as an authentication
  scheme, so it returned 500 instead of 403 (voting without being a group member,
  sharing someone else's list). Changed to a DomainException with 403.

### M9 — TMDb responses were not cached — ✅ IMemoryCache added
`MoviesController` proxied every request to TMDb. Now all four endpoints are in
a cache with a TTL: popular & now playing 1 hour, movie details 24 hours, search
(per query) 30 minutes. `SizeLimit=2048` prevents different queries from
bloating memory; errors are not cached. A test (`MovieCacheTests`) uses a fake
counting handler to verify that the second request does not reach TMDb.

> Note: `CompatibilityController` and `MatchingService` also call TMDb; they do
> not use this cache yet (separate task). It can be moved to Redis when scale
> requires it.

### M10 — "Read" status is never written — ✅ closed
The `Message.DateRead` field is defined but never set anywhere. Since
`MessageController.cs:158` returns `IsRead` based on this field, every incoming
message appears "unread" forever. There is no endpoint to mark a message as
read.

**Done:** the mark-as-read endpoint had already been added; what was missing was
"Seen".
- `dateRead` in the thread response; when messages are marked as read, a live
  `MessagesRead` event is sent to the sender (id of the last read message +
  timestamp). Web and mobile show "Seen HH:mm" only under the last read message.
- **Read receipts** are a privacy setting (on by default) and mutual: whoever
  turns them off does not show their reads and does not see others' either. Read
  records are still kept (for the unread count). Old clients that do not know
  this field do not change the setting when saving.

### M11 — Real-time chat — ✅ added
SignalR only sent notifications; the chat screen had to be reopened to see a new
message.

**Done:**
- **Server:** `POST /api/v1/messages` returns the created message (no client read
  the previous `{message}` text) and pushes the same message over the hub with
  the `ReceiveMessage` event to both the recipient and the sender (so the
  sender's other tabs/devices are updated too). The thread list, the send
  response and the live event share a single projection; shape equality is
  pinned by a test. Sending stays on REST (validation, reliable result); SignalR
  only does the distribution. No groups/rooms were needed: events go to the user
  identity.
- **The hub now requires authentication** (`[Authorize]`): previously
  unauthenticated connections were accepted as well (they received no events
  but held resources).
- **Clients (web + mobile):** messages are added instantly and deduplicated by
  id (sender response + live echo); a message arriving while the conversation is
  open counts as read, and that conversation's notification is not shown/marked
  as read; **the thread is refreshed when the connection is re-established**
  (messages arriving while disconnected are not lost); the conversation list is
  updated live. On mobile, a message arriving while the first load is still in
  progress is not lost either.

**Web bugs found along the way (reproduced in the browser with the old code):**
- **Double SignalR connection:** cleanup only stopped the connection in the
  "Connected" state; when the component was unmounted while connecting (React
  Strict Mode, user change) the old connection stayed alive and every event
  arrived twice (badge 2 for a single message). Listeners were also registered
  after start, so the first events could be missed.
- **Forced re-login after one hour:** on startup an expired access token was
  treated as "not logged in" and redirected to login; since no request was made,
  the refresh token (60 days) was never tried. Now a refresh is attempted first
  on startup (concurrent calls are collapsed into a single request).
- **First request unauthenticated:** the axios interceptors were set up in
  AuthContext's useEffect; since React runs child effects first, the page's first
  request went out without a token, got a 401 and caused an unnecessary refresh.
  The interceptors were also stacked every time the effect ran. They are now set
  up once when the module loads.
- The web chat page did not mark messages as read when opened; the "..." in the
  conversation list was also appended to short messages.

**Verification:** API tests with a real SignalR client (both parties + a second
tab receive it, a third person does not; live/response/thread shapes are the
same; the hub rejects unauthenticated connections and accepts the query
parameter — tested with mutation). Web end to end with two users and separate
browser contexts (13 checks; including offline/reconnection); the same suite
caught the double connection, the forced logout and the 401 on the old code. The
mobile stack (real ApiClient + Dart SignalR + providers) was run end to end
against the API in Docker; 9 new mobile unit tests.

**Later:** read receipts (✓✓) and a "typing..." indicator; loading older
messages when scrolling up for message pagination.

### M12 — Infrastructure gaps — ✅ largely closed
- ✅ Migrations are applied automatically on startup (`Database.MigrateAsync()`)
- ✅ Health check endpoint added (`/health`)
- ✅ Structured logging (Serilog + request log + traceId) — G10
- ✅ Dockerfile + docker-compose added (multi-stage, non-root user, SQLite on a
  persistent volume). The image was verified by building and running it locally:
  `/health` → Healthy, admin login → 200. `.dockerignore` and
  `.env.docker.example` were added as well.
- ✅ CI (GitHub Actions): build + test + lint
- ✅ Root README (setup/running, including Docker)

### M13 — Frontend gaps — ✅ largely closed
- No `.env.example`; `axiosConfig.js` silently falls back to `localhost:5074`
- No error boundary → a single render error results in a white screen
- In `NotificationContext` the SignalR connection can be re-created through the
  `showSnackbar` dependency; cleanup only closes the `Connected` state, so the
  connection leaks in the `Connecting` state
- Loading / empty state / error displays are inconsistent across pages
- No code splitting (lazy routes); a single 711 kB bundle
- The `react-refresh/only-export-components` rule was lowered to a warning:
  context files export both the provider and their own hook (a common pattern,
  it only affects hot reload)

**Done:** `.env.example` and the SignalR cleanup had been fixed earlier.
- Error boundary: a page error replaces only the page area with an error card
  (the navigation bar stays, and it resets when moving to another page); there is
  one more boundary at the outermost level. After a new release, a missing page
  chunk shows "The app has been updated, please reload the page".
- Code splitting: pages are loaded on demand, SignalR only after login;
  libraries are in separate, cacheable chunks. Entry bundle 733 kB → 22 kB
  (libraries: react 220, mui 318, vendor 74 kB; from the cache after the first
  visit).
- Tested with the production bundle (`vite preview`) in real Chromium.

**Remaining:** unifying the loading / empty / error displays across pages.

---

## 4. Backend Changes Required for the Mobile App

Gaps that **must** be closed in the backend for the mobile client to work:

| # | Gap | Why it is required for mobile | Status |
|---|---|---|---|
| B1 | Refresh token | On mobile users do not want to log in again every week; the access token must be short-lived | ✅ Added in this work |
| B2 | Typed auth response (`token`, `refreshToken`, `expiresAt`, `user`) | Safe deserialization on the Dart side | ✅ Added |
| B3 | JWT issuer/audience from configuration | A token tied to the Codespaces URL becomes invalid in the mobile build | ✅ Added |
| B4 | Key-reading bug in `SubscriptionController` | When the `JWT_KEY` env variable was used, subscription activation crashed with a `NullReferenceException` | ✅ Fixed |
| B5 | `/health` and `/api/app/config` | Connectivity check and forced-update check for the mobile app | ✅ Added |
| B6 | Message pagination | Mobile data usage | ✅ Added with `?page`/`pageSize` (default 100) |
| B7 | Push notifications (FCM/APNs) | SignalR does not run in the background on mobile; no notifications arrive while the app is closed | ✅ Added (enabled once a Firebase project is set up) |
| B8 | File upload endpoint | The profile photo is currently just a URL; picking from the gallery on mobile requires an upload | ✅ Added |
| B9 | API versioning (`/api/v1/...`) | Old versions in the stores break when the API changes | ✅ Added (backward compatible) |
| B10 | Pagination on all lists | Performance | ⬜ To do (B6 partial) |

### Also closed in this work

Items not required for mobile but fixed because they were along the way:

- **G3** The admin account is now created with `ADMIN_USERNAME`/`ADMIN_PASSWORD`;
  if no password is given, no account is created at all.
- **G2** `*.db` was added to `.gitignore` and `sineuyum.db` was untracked.
  *(The file is still in the git history — to be cleaned up in Phase 0.)*
- **G8** The CORS origin list and the JWT issuer/audience were moved to
  configuration.
- **G9** HSTS + HTTPS redirection are active in production.
- **G11** The password policy was aligned with the message in `RegisterDto`, and
  `RequireUniqueEmail` was enabled.
- **M4** The compatibility calculation was moved into a single
  `CompatibilityCalculator` service; the three copies were removed.
- **M5** The N+1 query in `AccountController.SearchUsers` was reduced to a single
  query, the `(dynamic)` sorting was replaced with a typed DTO, and the number of
  results was limited.
- **M10** `POST /api/messages/thread/{userId}/mark-as-read` was added;
  `DateRead` is now actually written.
- **Password change** did not exist in the application at all (neither for users
  nor for admins). `POST /api/account/change-password` was added; when the
  password changes, all other sessions of the user are revoked.
  **Note:** password *reset* (a "forgot my password" email) still does not exist;
  it requires an email sending infrastructure.
- **M12** Migrations are applied automatically on startup, `/health` was added,
  the root README and `sine-uyum-web/.env.example` were written, and GitHub
  Actions CI was set up (`.github/workflows/ci.yml`).
- **M13 partial** Web lint was brought down to zero errors: 17 unused `catch`
  bindings were cleaned up and **a real bug** was fixed —
  the `WatchlistDetailPage` edit modal called the non-existent `setNewListName` /
  `setNewListDesc` functions, so the list name/description could not be edited.
- **G1 partial** The key length for HS512 (≥64 bytes) is validated on startup.
  *(Key rotation and history cleanup still need to be done.)*

Since the access token became shorter, the web client was also connected to the
refresh token flow (`axiosConfig.js` + `AuthContext.jsx`); otherwise web users
would be logged out after 1 hour.

---

## 5. Mobile App (Flutter) — Done in This Work

A Flutter 3 app in the `sine_uyum_mobile/` directory:

**Infrastructure**
- State management with Riverpod, navigation and auth redirects with `go_router`
- `dio`-based API layer; automatic token injection via an interceptor and
  **silent refresh with the refresh token on 401**
- Token storage with `flutter_secure_storage` (iOS Keychain / Android Keystore)
- Material 3 theme, light/dark mode, matching the colour palette of the web app
- Live notification listening with `signalr_netcore`
- Build-time API address via `--dart-define=API_URL=...`

**Verification**
- `flutter analyze`: 0 issues
- `flutter test`: 21 tests passing (API error parsing, model deserialization,
  notification route mapping)
- Android: `INTERNET` permission for the release build; unencrypted HTTP is only
  allowed in the debug build and only for local addresses
- iOS: `NSAllowsLocalNetworking` for the local network only

**Screens**
- Login / Registration
- Home (now playing + popular movie carousels)
- Movie search and movie details (rating, adding to a list)
- Profile (statistics, rated movies, follow/unfollow)
- User search (with compatibility percentage)
- Watchlists and list details
- Messages and chat
- Event (status, joining, group members, movie voting)
- Notifications
- Settings (theme, logout)

---

## 6. Proposed Roadmap

### Phase 0 — Emergency security response ✅
1. ✅ The JWT key was removed from the git history (G1)
2. ✅ `sineuyum.db` and its side files were removed from the git history (G2)
3. ✅ The hard-coded admin account was removed; a known password was set for
   `admin`, and `POST /api/account/change-password` was added to change it (G3)
4. ➖ Request to GitHub Support: not considered necessary (see the G2 assessment above).

### Phase 1 — Production readiness (1-2 weeks)
4. ✅ Move to PostgreSQL — dual provider, selected via env (M2)
5. ✅ Global error handling + Serilog (G10)
6. ✅ Dockerfile + docker-compose (M12)
7. ✅ Test project for the API — `CompatibilityCalculator` + auth + error handling (M1)

### Phase 2 — API maturation (2-3 weeks)
9. Response DTOs for all endpoints (M3) — ✅ (Phase 2 completed)
10. Extract the compatibility calculation into a single service + unit tests (M4, M1) — ✅
11. Pagination (M6) and fixing the N+1 queries (M5) — ✅
12. TMDb cache (M9) — ✅
13. `AddMovie` security: fetch the metadata on the server (G7) — ✅
14. API versioning (B9) — ✅

**B9 — API versioning (`/api/v1/...`):** all controller routes were moved under
`api/v1/...` with a single `ApiV1RouteConvention` (no per-controller `[Route]`
changes were needed; the version lives in one place). To avoid breaking
deployed old clients, unversioned `/api/*` requests are rewritten to `/api/v1/*`
before routing (a temporary deprecation layer). All call sites of the web and
Flutter clients were explicitly moved to `/api/v1/...`. The SignalR hub
(`/notificationHub`), `/health` and swagger were not affected. Test:
`ApiVersioningTests` (the v1 path works + the unversioned path is routed to v1).

### Phase 3 — Mobile completion (2-3 weeks)
15. FCM/APNs push notifications (B7) — ✅

**B7 — Push notifications:**
- **Server:** `DeviceToken` table (unique token; when another account logs in on
  the same device the record moves to the new user; a limit of 20 devices per
  account) + `POST /api/v1/devices`, `POST /api/v1/devices/unregister`
  (unauthenticated: a device whose session has expired can still stop push).
  All notifications now go through a single path: `NotificationService` → save →
  SignalR → push queue. Push does not hold up the request path (in-memory queue +
  `PushDispatchService`); FCM HTTP v1, with service account OAuth via
  `Google.Apis.Auth`. If FCM says `UNREGISTERED` the record is deleted;
  `INVALID_ARGUMENT` does not delete it, since it may also be an error in our
  payload. The push body does not include the message content (lock screen
  privacy). If `FCM_SERVICE_ACCOUNT_JSON` is not provided, push is disabled and
  everything works as before.
- **Mobile:** `firebase_messaging`; the Firebase options come via `--dart-define`
  (an unconfigured build keeps working). Registration follows
  login/logout/session expiry/token refresh; tapping opens the relevant screen.
- **Test:** device registration, the end-to-end pipeline (fake sender) and the
  FCM wiring with the real `Program.cs` setup: the Google JWT signature/scope with
  a generated service account key + a local OAuth listener, the FCM v1
  URL/body/Bearer and the UNREGISTERED cleanup. The whole API suite also passed on
  real PostgreSQL (`TEST_POSTGRES_CONNECTION`). Mobile: 12 unit tests.
- **Not verified:** sending to a real Firebase project/device and the
  Android/iOS builds (the development environment had no access to the Android
  SDK; CI also only runs analysis+tests) — to be tried on a device at first setup.
- **Next step:** firebase_messaging 17 introduced the Firebase Installation ID
  (FID; `register()`/`onRegistered`) instead of the registration token and
  deprecated the token API. Since the FID flow is very new, we did not switch
  without verifying it on a device; since FCM v1 `message.token` also accepts the
  FID during the transition period, the switch will be client-side only.
16. File upload + profile photo (B8) — ✅

**B8 — Profile photo upload:**
- **Endpoints:** `POST /api/v1/profile/photo` (multipart, field `file`, ≤10 MB,
  10 per minute per user) and `DELETE /api/v1/profile/photo`. Response:
  `{ profileImageUrl: "/media/avatars/{userId}/{random}.jpg" }` — a relative
  path; clients combine it with the API address (records do not break when the
  domain changes).
- **The file is not stored as is:** it is decoded with SkiaSharp (MIT), the EXIF
  orientation is applied, it is center-cropped to a square and re-encoded as a
  512x512 JPEG → all metadata including EXIF/GPS is removed, and files disguised
  as images are filtered out. JPEG/PNG/WebP only. The declared size is checked
  before decoding the pixels ("decompression bomb": a 100-byte PNG declaring
  50000x50000 is rejected); since JPEG is decoded scaled, 48 MP phone photos get
  through.
- **Serving:** only `.jpg` under `/media`, `nosniff`, permanent caching (the file
  name is new on every upload). No escaping the folder. Re-uploading and deleting
  clean up old files. Docker: `sineuyum-media` volume (`/data/media`).
- **Behaviour change:** `PUT /api/v1/profile` no longer accepts a new external
  `profileImageUrl` (only the existing value, or empty = remove). An external URL
  leaked the IP of everyone viewing the profile to that server (tracking pixel).
  Existing records keep being displayed.
- **Clients:** upload/remove UI on the web; large photos are downscaled to
  1600 px in the browser (a 53 MB / 48 MP photo shrank to 1.4 MB and was
  uploaded). Gallery/camera on mobile (image_picker; HEIC → JPEG on iOS).
- **Fixed along the way:** requests rejected by Kestrel (body too large etc.)
  returned 500 "server error" → now their own status code. Expected business-rule
  errors (404/413/415) were logged as ERR + stack trace by
  ExceptionHandlerMiddleware → the duplicate log was turned off. Since the rate
  limiter ran before authentication, per-user limits could not be applied → the
  order was fixed.
- **Verification:** 32 new API tests (all 8 EXIF orientations against an
  independent table, tested with mutation; metadata cleanup; bomb; limits;
  serving; path traversal). On the production Docker image with real Kestrel:
  the SkiaSharp native library works, a 12 MP photo with GPS generated with
  Pillow came out as a clean 512x512, 413 for both Content-Length and chunked
  requests, and the file persists when the container is restarted. The web flow
  end to end in real Chromium. Mobile: 7 new tests (widget + unit).
- **Later:** the files are on local disk (`IMediaStorage` interface); when
  scaling to multiple servers they should move to S3/Blob + CDN.
17. Offline cache (movie lists, conversations) — ✅ (Phase 3 completed)

**Offline use (mobile):**
- **Read cache** in the `ApiClient` layer: allowed GET endpoints (profile,
  conversation list and messages, watchlists, notifications, event,
  popular/now-playing movies, movie details) are stored on every successful
  response; on a network error the last copy is returned. If the device is
  offline, the network is not waited for at all (no 15 s timeout). Responses
  given by the server (403/404) are not masked by the cache; search/recommendations
  are not stored. Movie posters are already on disk via `cached_network_image`.
- **Privacy:** the cache holds a single user at a time; it is cleared when
  another account logs in, on logout and when the session expires. It lives in
  the operating system's cache directory (not included in backups), at most 300
  entries. A cache error (disk full) never breaks any request/login.
- **On screen:** a "You're offline / Can't reach the server — showing saved data"
  banner at the bottom of every screen; it is also shown when there is a network
  but no internet (hotel Wi-Fi without login).

**Bugs breaking offline use found along the way (reproduced with tests):**
- **Starting without internet logged the user out:** the `/me` call on startup
  deleted the tokens on any error; a user opening the app on the metro had to log
  in again. Now only a definite rejection by the server ends the session; without
  a network the app opens with the saved profile, or waits on the splash screen
  and continues when the network comes back.
- **A network drop while refreshing the token counted as "session ended":** the
  tokens were also deleted when the refresh request could not reach the server.
  Now only if the server rejects it; otherwise the caller gets a network error,
  not a 401.
- **SignalR died permanently:** if it could not connect on startup it was never
  retried; when automatic reconnection gave up after ~40 s the connection stayed
  closed too — live notifications/chat did not arrive until the app was reopened.
  Now it is retried at 5/15/30/60 s intervals after closing, connects immediately
  when the network comes back, and missed items are refreshed. The push device
  registration is also retried when the network comes back.
- `/me` only made it into the cache on the third launch (the user id is learned
  from the `/me` response); it is now stored explicitly on login/startup.

**Verification:** 19 new mobile tests (session + cache + file store + banner; the
two main fixes tested with mutation), 68 in total. The real mobile stack against
the API in Docker: the server was shut down for 50 s → data from the cache,
banner shown; when the server came back the client reconnected on its own, the
message sent during the outage arrived, and the next live message arrived in
124 ms. The same scenario with the old SignalR code timed out (the message never
arrived).

**Later:** offline writes (message send queue / outbox). For now, sending offline
gives an understandable error. The web client has no offline support (a PWA is a
separate task).
18. Real-time chat (message delivery via SignalR) (M11) — ✅
19. Message "read" status (M10) — ✅
20. Store release preparation: icons, splash, privacy policy, forced update

### Phase 4 — Business model and scaling
21. Real payment integration (G4) — ✅ hole closed + infrastructure; providers are waiting for account details
22. Move matching to a background job (M7) — ✅
23. Normalize `SuggestedMovieIds` into a table (M8) — ✅
24. Extend rate limiting to the whole API (G5) — ✅
25. Privacy settings (G12) — ✅

### Phase 5 — A social network for movie lovers
Goal: turn the app from "ratings + follows + messages" into a social network for
movie lovers; the core differentiating feature is the compatibility score.
26. Reason for compatibility + "discussion movie" — ✅
    - `insights` in the compatibility response: genres both love / disagree on
      (from all ratings, at least 2 movies per genre), a one-sentence summary, a
      shared favourite, and the shared movie with the biggest disagreement. Shown
      on the profile on web and mobile; "Ask them" ("Ona sor") opens the chat with
      a ready-made draft.
    - Along the way: **genres were never written** (`Genres`/`MovieGenres` were
      always empty; this is also why genre matching in "shared recommendations"
      silently did not work). TMDb genres are saved for new movies; old ones are
      backfilled by `GenreBackfillWorker`, 40 movies every 30 minutes.
27. Social core: reviews (rating + text, spoiler), following feed, likes and
    comments — ✅
    - A review is the written form of a rating (a rating is required first), with
      a spoiler flag; every review shows a "N% compatible" badge with the author.
      Likes and comments send a notification to the author (without the comment
      text in the notification).
    - Feed: ratings and reviews of the people you follow and your own, with cursor
      pagination. Changing a rating does not move it to the top; the first review
      does. Ratings from before the feed existed do not appear in it, since their
      time is unknown.
    - Visibility follows the same rule as G12 (private profile, block); comments by
      blocked people are hidden, and you cannot comment on a blocked person.
    - Along the way: "one rating per user" was only enforced in code; a unique
      index was added (the migration first removes duplicates, keeping the latest
      rating).
    - Web: reviews on the movie page, a "Feed" page. Mobile: Feed / Movies tabs on
      the home screen, reviews on the movie page, a comment panel.
28. "Score for you": the average rating compatible people gave a movie — ✅
    - The average of the ratings of people who rated the movie and have at least 3
      movies in common and at least 60% compatibility with you, weighted by the
      square of the compatibility; not shown if there are fewer than 2 such
      people. Follows the privacy/block rules. Shown on the movie page next to the
      TMDb rating, together with the three most compatible people.
29. Movie identity card (shareable) — ✅
    - `GET /profile/{id}/identity`: total and average, most-watched genres (share
      and average), deviation from the TMDb average, "most contrarian rating" (at
      least 3 points apart), favourite, and a rule-based title (Contrarian Critic,
      Generous Viewer, Hard to Please, {Genre} Enthusiast, All-Rounder, Film
      Explorer, New Explorer). Same visibility rule as the profile details.
    - The TMDb average is now stored (`Movies.TmdbVoteAverage`); the migration
      flags the movies to be reprocessed once.
    - Web: card + 1080×1350 PNG (system share menu or download).
      Mobile: card screen, the image is captured and shared with `share_plus`.
30. Weekly taste twin — ✅
    - Every week (starting on Monday) the person most compatible with you whom you
      do not follow and who has not been suggested before; at least 5 movies in
      common and 70% compatibility. Only people open to discovery are candidates
      (public profile, not hidden from search, no block). `TasteTwinWorker`
      notifies once a week in hourly rounds; `GET /taste-twin` computes it
      immediately if needed (204 if there is no suitable candidate). A card at the
      top of the feed on web and mobile.
    - Along the way: since the web page's language was "en", "i" became "I" in all
      upper-case buttons and headings (e.g. "TAKIP ET" instead of "TAKİP ET" for
      "Follow", "BU FILMI PUANLA" instead of "BU FİLMİ PUANLA" for "Rate this
      movie"); fixed with `lang="tr"`.
31. Multi-language support (Turkish + English, extensible) — ✅
    - Web: i18next with a language switcher (navbar, drawer, login/register,
      edit profile); `<html lang>` follows the language. Mobile: gen-l10n ARB
      files and a System / Türkçe / English setting; the offline cache is keyed
      by language.
    - Clients send the selected language in the `Accept-Language` header; the API
      localizes every user-facing message (errors, notifications, generated texts,
      genre names, TMDb data) and falls back to English.
    - The user's language is stored (`PUT /api/v1/account/language`), so
      notifications and push messages are rendered in the recipient's language.
    - Adding a language = one translation file per platform (API
      `SineUyum.Api/Resources/Locales/<lang>.json`, web
      `sine-uyum-web/src/locales/<lang>.json`, mobile
      `sine_uyum_mobile/lib/l10n/app_<lang>.arb`) + registering the code in each
      platform's list of supported languages. See the "Localization" section of
      the root README.
    - Movie titles and posters in the local catalog are stored per language
      (`MovieTranslations`; existing ones were kept as Turkish; TMDb picks a
      poster per language, often with the localized title on it). A missing
      one is fetched when someone opens or rates the movie in that language
      (usually a cache hit, since it is the same request as the details page)
      and `MovieTranslationWorker` fills in the rest (40 movies every 10
      minutes); until then the stored title and poster are shown.
      Notifications keep the movie id, so "liked your review of …" shows the
      title in the reader's language.
    - Along the way: the catalog cached movie details without credits under the
      same key as the details page, so a details page could come back without
      the cast; both now use the same request.
    - Framework validation messages (implicit `[Required]`, `[EnumDataType]`,
      model binding errors, unreadable JSON) are localized too, and generic
      messages use localized field names ("Alıcı alanı zorunludur." instead of
      "RecipientId alanı zorunludur."); JSON parser details are no longer sent
      to the client.
32. Shared watchlists — ✅
    - The owner invites people (at most 20); invitees accept or decline, accepted
      members add and remove movies, the owner removes members, members can
      leave. Blocking ends memberships between the two users.
    - Each item shows who added it and which of the list's people have rated it
      ("watched by"), with an "Unwatched by all of us" filter.
    - Web and mobile: invitations section, members dialog/sheet with user search,
      leave list. The invitation notification opens the Lists tab on mobile.
33. Film diary — ✅
    - `DiaryEntry`: a dated log of watched movies, separate from ratings
      (rewatches are separate entries, with an optional note up to 500
      characters). The same movie cannot be logged twice on the same day; dates
      in the future or before 1900 are rejected.
    - `POST /diary` can also set the rating; rating writes now go through
      `RatingService`, shared with the ratings endpoint, so the feed behaves the
      same either way.
    - `GET /diary/user/{id}` (paginated, by year) and `/summary` (per-month counts,
      rewatches, available years) follow the profile-details privacy rule.
    - Web: "Add to diary" dialog on the movie page, diary section with a month
      chart on the profile. Mobile: add sheet on the movie page, diary page
      (`/profile/:userId/diary`), readable offline.
    - `GET /diary/movie/{movieId}`: my entries of one movie, so the "Add to
      diary" form pre-selects "Rewatch" when the movie is already logged.
