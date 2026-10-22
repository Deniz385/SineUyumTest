# SineUyum Mobile (Flutter)

iOS + Android app running on top of the SineUyum API (`SineUyum.Api`).

## Requirements

- Flutter 3.35 or later (developed with Flutter 3.47 / Dart 3.13)
- A running SineUyum.Api instance

## Running

The API address is not hard-coded; it is provided at build time:

```bash
# Android emulator (the host machine is visible as 10.0.2.2)
flutter run --dart-define=API_URL=http://10.0.2.2:5074

# iOS simulator
flutter run --dart-define=API_URL=http://localhost:5074

# Physical device (the computer's local IP)
flutter run --dart-define=API_URL=http://192.168.1.25:5074
```

If `API_URL` is not given, the default `http://10.0.2.2:5074` is used.

Other build variables:

| Variable | Default | Description |
|---|---|---|
| `API_URL` | `http://10.0.2.2:5074` | API base address |
| `TMDB_IMAGE_BASE_URL` | `https://image.tmdb.org/t/p/` | Poster URLs |
| `APP_VERSION` | `1.0.0` | Compared with the minimum version on the server |

> Unencrypted HTTP is only allowed in **debug** builds and only for local
> addresses (`10.0.2.2`, `localhost`, `127.0.0.1`). Release builds require
> HTTPS — when publishing, `API_URL` must start with `https://`.

## Building

```bash
flutter build apk --release --dart-define=API_URL=https://api.example.com
flutter build ipa --release --dart-define=API_URL=https://api.example.com
```

## Tests and static analysis

```bash
flutter analyze
flutter test
```

## Architecture

```
lib/
  core/
    config/      Build-time configuration (API_URL etc.)
    network/     Dio client, token injection, silent refresh on 401, error mapping
    router/      go_router routes and session-based redirects
    storage/     Keeping tokens in secure storage (Keychain / Keystore)
    theme/       Material 3 light/dark themes (aligned with the web palette)
  data/
    models/      Dart counterparts of the API responses
    repositories/ Data access per endpoint
  providers/     Riverpod providers (session, notifications, theme, data)
  features/      Screens (auth, home, movies, profile, users, watchlists,
                 messages, events, notifications, settings, subscription, shell)
  widgets/       Shared components (poster, avatar, state views,
                 compatibility badge, rating bottom sheet)
```

### Session management

1. After login/registration, `token` + `refreshToken` are written to secure storage.
2. `Authorization: Bearer <token>` is added to every request.
3. On a 401, a new token is obtained via `/api/v1/account/refresh` and the
   **request is retried automatically**; the user does not notice.
4. For concurrent 401s only a single refresh request is sent (since the server
   rotates refresh tokens, a second request would be invalid).
5. If the refresh also fails, the tokens are deleted and the app returns to
   the login screen.

### Notifications

The same SignalR connection also carries **live chat** (`ReceiveMessage`): a
message shows up in the open conversation instantly, missed messages are
refreshed from the server when the connection is re-established, and no
notification is shown for messages from the conversation that is open.

Two channels work together:

- **While the app is open:** SignalR (`/notificationHub`) — the notification appears in the list instantly.
- **While the app is in the background/closed:** FCM push (on iOS too, via FCM → APNs).
  Tapping it opens the relevant screen (including a cold start) and marks the notification as read.

Since FCM does not show a system notification in the foreground, there are no
duplicate notifications. On login the device is registered at
`/api/v1/devices`; on logout **and when the session expires** it is
unregistered (so the device does not receive the previous user's
notifications). The status is shown in the *Settings → Push notifications* row.

#### Enabling push (Firebase setup)

The app builds and runs without Firebase configuration; push is simply disabled.

1. Create a project in the [Firebase console](https://console.firebase.google.com).
   - Add an Android app: package name `com.sineuyum.sine_uyum_mobile`
   - Add an iOS app: bundle id `com.sineuyum.sineUyumMobile`
2. Take the values from *Project settings → General → Your apps* and pass them
   to the build (`google-services.json` / `GoogleService-Info.plist` are **not
   needed**):

   ```bash
   flutter run \
     --dart-define=API_URL=https://api.example.com \
     --dart-define=FIREBASE_API_KEY=AIza... \
     --dart-define=FIREBASE_PROJECT_ID=sineuyum-xxxx \
     --dart-define=FIREBASE_MESSAGING_SENDER_ID=1234567890 \
     --dart-define=FIREBASE_ANDROID_APP_ID=1:1234567890:android:abc... \
     --dart-define=FIREBASE_IOS_APP_ID=1:1234567890:ios:def...
   ```

3. **Server:** pass the JSON downloaded via *Project settings → Service
   accounts → Generate new private key* to the API as
   `FCM_SERVICE_ACCOUNT_JSON` (contents) or `FCM_SERVICE_ACCOUNT_FILE` (path).
   This key is **secret** and is not committed to the repository (it is in
   `.gitignore`). Details: `.env.docker.example`.
4. **iOS only:** create an APNs authentication key (.p8) in Apple Developer and
   upload it under *Project settings → Cloud Messaging → Apple app*. In Xcode,
   add *Runner → Signing & Capabilities → + Capability → Push Notifications*
   (this requires a paid Apple developer account, which is why it is not
   enabled in the project by default — so device builds with a free account
   keep working). Push is tested on a physical device, not in the iOS simulator.
5. Verification: log in to the app; *Settings → Push notifications* should
   show "On". Close the app and send a message from another account.

### Offline use

Without internet access, the profile, conversations and messages, watchlists,
notifications and movie lists open in their last saved state; a "showing saved
data" banner appears at the bottom of the screen. When the connection comes
back, the live connection is re-established automatically and the data is
refreshed. Starting the app without internet does not log the user out. The
cache holds a single user; it is cleared on logout and when the account
changes.

## Known gaps

Work still to be done on the mobile side is listed in the "Phase 3 — Mobile
completion" section of `docs/ANALYSIS-AND-ROADMAP.md`. Highlights:

- Sending messages offline (send queue) — reading already works offline
- App icon and splash screen images (currently the Flutter defaults)
