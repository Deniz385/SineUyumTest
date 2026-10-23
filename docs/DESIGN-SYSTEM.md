# SineUyum Design System

The shared visual language of the web (`sine-uyum-web`) and mobile
(`sine_uyum_mobile`) apps. Both platforms implement the same tokens; when a
value changes here, it changes on both.

**Direction: cinematic.** A dark, poster-first interface that feels like the
lights going down in a theatre: deep near-black surfaces, a warm marquee-gold
accent, large artwork, confident display type and calm, quick motion. Dark is
the default; light is a first-class alternative (warm "paper"), never an
afterthought.

---

## 1. Colour

All text/background pairs below were checked against WCAG 2.1 AA (4.5:1 for
body text, 3:1 for large text and UI glyphs).

### Dark (default)

| Token | Value | Use |
|---|---|---|
| `bg` | `#0B0C10` | Page background |
| `surface` | `#14161C` | Cards, app bars, bottom navigation |
| `surfaceRaised` | `#1C1F27` | Hovered cards, inputs, menus |
| `surfaceOverlay` | `#262A34` | Dialogs, sheets, tooltips |
| `border` | `#2E3340` | Hairlines, dividers, input outlines |
| `text` | `#F2F2F5` | Primary text |
| `textSecondary` | `#A9ADBA` | Secondary text, metadata |
| `textMuted` | `#868B99` | Captions, placeholders — only on `bg`, `surface`, `surfaceRaised` (4.2:1 on `surfaceOverlay`: large text only) |
| `primary` | `#F5B841` | Marquee gold: primary buttons, active tab, focus ring, rating stars |
| `onPrimary` | `#1A1300` | Text/icons on `primary` |
| `accent` | `#FF5A5F` | Cinema red: likes, hearts, "live" badges, destructive icons |
| `accentStrong` | `#D93A40` | Filled destructive buttons (white text 4.5:1) |
| `success` | `#3DD68C` | Positive states, high compatibility |
| `error` | `#FF6B70` | Errors |
| `info` | `#6CB4FF` | Links in body text, info notices |

### Light

| Token | Value | Use |
|---|---|---|
| `bg` | `#F7F6F3` | Warm paper background |
| `surface` | `#FFFFFF` | Cards, app bars |
| `surfaceRaised` | `#F0EEE9` | Inputs, hovered rows |
| `surfaceOverlay` | `#FFFFFF` + shadow | Dialogs, sheets |
| `border` | `#E4E1DA` | Hairlines |
| `text` | `#16171B` | Primary text |
| `textSecondary` | `#545866` | Secondary text |
| `textMuted` | `#6B6F7B` | Captions — only on `bg`, `surface` |
| `primary` | `#9A6200` | Deep gold (same hue, darkened for contrast); `primary` text only on `bg`/`surface` |
| `onPrimary` | `#FFFFFF` | |
| `accent` | `#C9252C` | |
| `success` | `#137A4B` | |
| `error` | `#C42B31` | |
| `info` | `#1F63B8` | |

### Gradients and scrims

- **Marquee gradient** (hero titles, year in review, onboarding):
  `#FFD54F → #FF8A65 → #FF5A5F` at 120°.
- **Backdrop scrim** (text over movie backdrops): `bg` from 0% opacity at the
  top to 100% at the bottom, plus a 40% `bg` overlay — text over artwork must
  stay above 4.5:1.
- **Compatibility scale**: `< 50%` `accent`, `50–74%` `primary`, `≥ 75%`
  `success`. Always shown with the number, never colour alone.

## 2. Typography

| Role | Font | Size / line height | Weight |
|---|---|---|---|
| `display` | Bricolage Grotesque | 40 / 44 | 800 |
| `h1` | Bricolage Grotesque | 30 / 36 | 700 |
| `h2` | Bricolage Grotesque | 22 / 28 | 700 |
| `h3` | Inter | 18 / 24 | 600 |
| `title` (card titles) | Inter | 16 / 22 | 600 |
| `body` | Inter | 15 / 22 | 400 |
| `bodySmall` | Inter | 13 / 18 | 400 |
| `caption` / `overline` | Inter | 12 / 16 | 600, +0.04em, overline uppercase |
| `number` (scores, counts) | Bricolage Grotesque, tabular figures | context | 700 |

- Both fonts are SIL OFL and cover Latin Extended (Turkish İ ı ş ğ ç ö ü).
  They are **bundled with the apps** (web: npm `@fontsource-variable/*`;
  mobile: font assets) — no runtime requests to font CDNs.
- The page language must match the UI language so `text-transform: uppercase`
  produces "İ" in Turkish (the web keeps `<html lang>` in sync).
- Mobile respects the system text size up to 1.3×; layouts must not clip.

## 3. Space, size and shape

- **Spacing**: 4-point grid — `4, 8, 12, 16, 20, 24, 32, 40, 48, 64`. Screen
  edge padding: 16 (mobile), 24 (tablet), content max width 1200 on the web
  (reading columns such as the feed: 680).
- **Radii**: `sm 8` (chips, inputs, small posters), `md 12` (cards, posters),
  `lg 20` (sheets, dialogs, hero cards), `pill 999` (buttons, badges).
- **Posters** are always 2:3, `md` radius, a 1px inner hairline
  (`rgba(255,255,255,.06)` on dark) and a placeholder with the film icon while
  loading or when missing. Backdrops are 16:9.
- **Elevation**: on dark, depth comes from lighter surfaces
  (`surface → surfaceRaised → surfaceOverlay`), not shadows. Light theme uses
  soft shadows (`0 1px 2px rgba(0,0,0,.06), 0 4px 16px rgba(0,0,0,.06)`).
- **Touch targets**: at least 44×44 (web) / 48×48 (mobile).

## 4. Motion

- Durations: `fast 120ms` (hover, press), `standard 200ms` (state changes,
  menus), `emphasized 320ms` (screen/sheet enter). Exit is ~70% of enter.
- Easing: standard `cubic-bezier(0.2, 0, 0, 1)`; enter
  `cubic-bezier(0.05, 0.7, 0.1, 1)`.
- Patterns: poster cards lift slightly on hover (web) and scale to 0.98 on
  press; likes/saves pop (scale 1 → 1.2 → 1, 200ms); skeletons shimmer;
  shared poster → movie hero transition where the platform makes it cheap.
- **Reduced motion** (OS setting): no shimmer, no auto-advance, cross-fades
  instead of movement.

## 5. Iconography and imagery

- Rounded icon family on both platforms (MUI `*Rounded` icons, Flutter
  `Icons.*_rounded`), 20/24px, `textSecondary` by default.
- Movie artwork is the hero of every screen: lead with posters/backdrops,
  keep chrome quiet around them. Avatars are circles with initials as the
  fallback.
- Every poster/backdrop has an accessible label (the movie title); decorative
  images are hidden from screen readers.

## 6. Components

Built once per platform and used everywhere:

- **Buttons**: `primary` (filled gold, pill), `tonal` (surfaceRaised), `ghost`
  (text), `danger` (accentStrong). One primary action per view.
- **PosterCard**: poster + title (2 lines max) + optional meta row (year,
  rating, compatibility badge). Sizes: S 96, M 140, L 200 wide.
- **MovieRow**: horizontal, snap-scrolling carousel of PosterCards with a
  section title and "See all".
- **ListCard**: 2×2 poster collage (placeholders for missing posters), name,
  owner chip, film count, like/save actions.
- **UserChip / UserRow**: avatar, @name, optional compatibility badge and
  follow button.
- **CompatibilityBadge**: ring or pill with "%" and the compatibility scale
  colour.
- **RatingInput**: 10-point rating shown as 5 stars with halves; tap/drag and
  keyboard arrows; the number is always shown next to it.
- **Chips** (genres, filters), **Tabs** (underline in `primary`),
  **Segmented control** (sorts).
- **Skeletons** for every list/grid/feed on first load (no bare spinners in
  content areas); **EmptyState** (icon, one-line title, one-line hint, optional
  action); **ErrorState** (message + "Try again").
- **Sheets** (mobile) / **dialogs** (web) for quick actions: rate, add to
  diary, add to list, share.
- **Snackbars/toasts**: bottom, `surfaceOverlay`, one line, optional action.

## 7. Information architecture and navigation

**Web** (top bar): wordmark · movie search · Feed · Discover · Event ·
Messages · notifications · avatar menu. *Discover* gathers movies (popular,
now playing, top rated), lists and people. The avatar menu holds Profile, My
lists, Diary, Year in review, Settings (privacy, language, theme) and Log out.
Below 900px: a compact top bar + bottom navigation mirroring mobile.

**Mobile** (bottom navigation, 5 tabs): **Home** (feed + movie rows) ·
**Discover** (search, lists, people) · **Event** · **Messages** · **Profile**
(diary, lists, year in review, settings). Detail screens push on top; quick
actions open sheets.

**Key screens**

- **Home**: greeting + taste-twin / year-in-review cards when relevant, then
  the following feed; when the feed is thin, movie rows ("Popular now", "Top
  rated", "For you") fill the screen instead of an empty state.
- **Movie**: full-bleed backdrop hero with scrim, poster, title, year,
  runtime, genre chips; scores side by side (For you · TMDb); one action row
  (Rate · Diary · Add to list · Share); cast row; reviews.
- **Profile**: cover (their favourite film's poster, blurred and darkened
  behind a scrim; the catalog stores posters, not backdrops), overlapping avatar,
  name, bio, identity-title pill, stats (films, average, followers,
  following), compatibility badge on others' profiles, then tabs: Ratings ·
  Diary · Lists · Reviews.

## 8. First-run welcome (onboarding)

Shown while `onboardingCompleted` is false (`/account/me`); every step can be
skipped, skipping or finishing calls `POST /account/onboarding/complete`.

1. **Welcome**: wordmark, one sentence ("Find the people who watch like you
   do"), three benefits, language choice, "Let's start".
2. **Rate a few films**: a poster grid from `GET /movies/top_rated` and
   `/movies/popular`; tap a poster to rate it (RatingInput in a sheet); a
   progress line "3 / 5"; "Continue" unlocks at 5 ratings (or "Skip").
3. **People like you**: `GET /people/suggestions` — taste-based when
   possible ("92% compatible, 4 films in common"), otherwise popular people;
   follow buttons.
4. **Done**: confetti-free, calm confirmation, then Home.

## 9. Accessibility checklist

- AA contrast for all text (tokens above); focus visible everywhere
  (2px `primary` outline, 2px offset); never rely on colour alone.
- All interactive elements have accessible names; icons-only buttons have
  tooltips (web) / semantics labels (mobile).
- Keyboard: every action reachable, logical order, `Esc` closes dialogs,
  arrow keys move through tabs and ratings.
- Screen readers: posters announce the title, ratings announce "8 out of 10",
  live regions for snackbars.
- Respect reduced motion and the system text size; no information only on
  hover.
