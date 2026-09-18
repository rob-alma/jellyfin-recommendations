# Personal Recommendations for Jellyfin

A Jellyfin server plugin that builds a per-user taste profile from watch history (genres,
cast, crew, studios, decade) and shows a **"Recommended For You"** row on the home screen for
each user — no external services, no API keys, nothing but your own library and watch history.

Targets **Jellyfin 10.11.x** (builds against `Jellyfin.Controller`/`Jellyfin.Model` 10.11.11, .NET 9).

## How it works

1. For every user, it looks at what they've watched, favorited, liked, or rated — movies
   directly, series by aggregating episode-level playback (Jellyfin doesn't reliably track
   play count on the Series item itself).
2. It builds a weighted taste profile from genres, directors, top-billed actors, studios, and
   decade, with older activity decaying gradually rather than disappearing.
3. Every unwatched movie/series in the library is scored against that profile.
4. The top results (capped so one studio/franchise can't dominate the list) are cached per user,
   kept up to date by a scheduled task (default every 6 hours) and, optionally, a debounced
   refresh shortly after the user finishes watching something.
5. A user with no watch history yet gets a cold-start fallback: the highest community-rated
   unwatched titles.
6. A small script injected into the Jellyfin web client reads those recommendations for the
   logged-in user and renders a "Recommended For You" row on the home screen — movies and
   series shown as themselves (real posters, real detail pages), clicking the heading opens the
   full list. See "How the widget gets onto the home screen" below for how that injection works
   and what it depends on.

## Resource usage

Scoring a user against the whole library is the plugin's only real cost, and it's deliberately
kept bounded and predictable rather than trying to be maximally fast:

- **Never more than one refresh at a time, plugin-wide.** The scheduled task, the debounced
  playback-triggered refresh (off by default, see below), and an API cache-miss warm-up all go
  through a single shared gate (`ComputeGate`) that only lets one of them run at once, on one
  thread. If two would-be triggers land at the same time, one waits (the scheduled task) or is
  simply skipped for that cycle (the opportunistic ones) rather than running alongside the other.
- **Each user's watch data is fetched once per refresh**, not once per internal step - building
  the taste profile and building the candidate list used to independently re-fetch it.
- **The library scan is reused for a few minutes** across nearby triggers instead of re-querying
  Jellyfin for data that hasn't meaningfully changed.
- **The playback-triggered refresh is off by default.** Recommendations only update on the
  schedule you set (Scheduled refresh interval, or your own custom trigger under Dashboard →
  Scheduled Tasks) unless you opt into refreshing shortly after playback too.

## How the widget gets onto the home screen

Jellyfin has no first-party way for a server plugin to add a row to the home screen — there's no
supported API for it. The technique every plugin that does this (including
[Editor's Choice](https://github.com/lachlandcp/jellyfin-editors-choice-plugin), which is what
this was modeled on) actually uses is injecting a `<script>` tag into the web client's served
`index.html`. That script then does its own DOM work in the browser. This plugin tries, in
order (configurable under **Home screen widget → Frontend injection method**):

1. The community **[File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)**
   plugin, if installed — registers a callback that patches `index.html` as Jellyfin serves it.
2. The community **[JavaScript Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector)**
   plugin, if installed — same idea, different plugin.
3. **Direct**: patches `jellyfin-web/index.html` on disk itself, re-applied on every server
   start (so it survives Jellyfin updates that overwrite the file). This needs the Jellyfin
   server process to have write access to its own web files — usually fine, but see Editor's
   Choice's README for the Docker/permissions caveats if it doesn't seem to take effect (same
   underlying mechanism, same failure modes).

Neither helper plugin is required — "Automatic" (the default) falls through to direct injection
if neither is installed, and that's proven to work in a plain Jellyfin Docker setup (that's
literally how Editor's Choice's own hero banner renders without either helper plugin present).

This is inherently a bit more fragile than a normal server-side feature: it depends on finding
specific elements in Jellyfin's home page DOM (`#indexPage`, `#homeTab`, `.homeSectionsContainer`)
that aren't a documented, stable plugin surface. If a future Jellyfin web client update changes
that structure, the row may stop appearing until this plugin is updated to match — same risk
Editor's Choice itself carries.

**Also only works in the web client and apps built on it** (browser, PWA, the official Android/
iOS apps) — it can't appear in Infuse, Android TV, or other native clients, because there's no
script for them to run. Versions up to 0.2.1 tried to work around this with a private playlist,
which every client can see — but Jellyfin silently expands a Series added to a playlist into
every one of its episodes, so a recommended series never actually showed up as itself. 0.3.0
drops that approach entirely in favor of the widget, which doesn't have that limitation.

**After updating the plugin, do a hard refresh (or clear site data) for your Jellyfin domain**,
in the app too. The script itself is served with no-cache headers and its URL carries the
plugin's version (`?v=...`) so a version bump is always fetched fresh — but that only helps once
your browser/app asks for a new copy of `index.html` in the first place, and Jellyfin's own
`index.html` caching is outside this plugin's control. Without a hard refresh, you can be running
an old cached copy of the widget script indefinitely with no sign anything's wrong other than it
not behaving like the changelog says it should.

On a touch device, Jellyfin's own swipe-between-tabs gesture (Home → Favorites → …) explicitly
skips itself for a touch that starts on an element carrying the `scrollX` class — every native
scrollable row carries it for exactly this reason, which is why swiping Continue Watching never
changes tabs. This row carries the same class, so a swipe on it — any speed, starting anywhere on
the row — scrolls the row instead of ever reaching the tab switcher. If you do navigate to
Favorites (by tab or swipe) on an account that's never favorited anything, the optional
favorites-seeding setting below keeps that tab from being empty.

## Project layout

- `Jellyfin.Plugin.PersonalRecommendations.Core/` — the pure scoring/profile-building logic.
  No Jellyfin dependency, so it's plain unit-testable.
- `Jellyfin.Plugin.PersonalRecommendations/` — the actual Jellyfin plugin: reads watch history
  and the library via Jellyfin's APIs, maintains the recommendation cache, the scheduled tasks,
  the config page, the admin API, and the home screen widget's server-side injection + script.
- `Jellyfin.Plugin.PersonalRecommendations.Tests/` — unit tests for the `Core` project.

## Build

```bash
dotnet restore Jellyfin.Plugin.PersonalRecommendations.slnx
dotnet build Jellyfin.Plugin.PersonalRecommendations.slnx -c Release
dotnet test Jellyfin.Plugin.PersonalRecommendations.Tests/Jellyfin.Plugin.PersonalRecommendations.Tests.csproj
```

The plugin project targets `net9.0` (matching Jellyfin 10.11.x's runtime) and the `Core`/test
projects target `net8.0`, so `dotnet test` runs without needing the .NET 9 runtime installed.

## Install via the Jellyfin plugin catalog (recommended)

`.github/workflows/release.yml` builds the plugin, packages it, and publishes a GitHub Release
containing the plugin zip and a `manifest.json` whenever a tag like `v0.4.8` is pushed (or via
"Run workflow" in the Actions tab).

1. Push a tag, e.g. `git tag v0.4.8 && git push origin v0.4.8`, and wait for the "Release"
   workflow to finish (Actions tab).
2. In Jellyfin, go to **Dashboard → Plugins → Repositories → Add Repository** and add:
   - Repository name: anything, e.g. `Personal Recommendations`
   - Repository URL: `https://github.com/<owner>/<repo>/releases/latest/download/manifest.json`
3. Go to **Catalog**, find "Personal Recommendations" under General, install it, and restart
   Jellyfin when prompted.
4. To update later: bump the version, push a new tag, and Jellyfin will offer the update from
   the same repository the normal way.

## Install manually (no GitHub needed)

1. Build in Release mode (above), or `dotnet publish Jellyfin.Plugin.PersonalRecommendations -c Release -o out`.
2. Copy every `.dll` from the publish/build output plus `meta.json` into a new folder under your
   Jellyfin server's plugin directory, e.g. `<jellyfin-config>/plugins/PersonalRecommendations_0.4.8.0/`:
   - `Jellyfin.Plugin.PersonalRecommendations.dll`
   - `Jellyfin.Plugin.PersonalRecommendations.Core.dll`
   - `Newtonsoft.Json.dll` (a runtime dependency — don't skip it, the plugin won't load without it)
   - `meta.json`
3. Restart Jellyfin.

## After installing, either way

1. Check **Dashboard → Plugins** — "Personal Recommendations" should be listed and enabled.
   A freshly (re)installed plugin shows **Status: Restart** — it isn't actually loaded (no
   config page, no scheduled tasks, no widget registration) until you restart the server.
2. After restarting, its settings page should appear as its own entry under **Plugins** in the
   left nav (not just the plugin info card) — that's where the actual settings and the
   **Refresh recommendations now** button live.
3. Check **Dashboard → Logs** for a line from `FrontendRegistrationStartupTask`/
   `DirectScriptInjector` confirming the widget script was registered (or a warning if it
   couldn't be — see "How the widget gets onto the home screen" above for why that might happen).
4. Either wait for the scheduled task or click **Refresh recommendations now**. This queues the
   refresh as a background task and returns immediately.
5. Check **Dashboard → Scheduled Tasks → Personal Recommendations** for the refresh task's and
   the widget registration task's logs/manual runs.
6. Load the Jellyfin home screen (not the admin dashboard) as a user with some watch history —
   a "Recommended For You" row should appear; clicking its heading opens the full list.

If you're upgrading from 0.1.0–0.2.1: the playlist those versions created is removed
automatically on the first startup after updating (check the log for
`LegacyPlaylistCleanupService` to confirm) — nothing to do manually.

## Configuration

Available on the plugin's settings page (Dashboard → Plugins → Personal Recommendations):

| Setting | Default | Notes |
|---|---|---|
| Enabled | on | |
| Recommendations per user | 20 | |
| Max results per studio | 3 | 0 disables the cap |
| Minimum community rating | 0 (off) | |
| Recommend movies / series | both on | |
| Show the widget on the home screen | on | |
| Seed a few favorites for accounts that have none | on | Cosmetic: the first time recommendations are computed for an account with zero favorites of its own, marks its top 3 recommendations as favorites, so Jellyfin's own Favorites tab isn't empty. Never touches an account that already has any favorites; only ever runs once per account. |
| Widget heading | `Recommended For You` | |
| Frontend injection method | Automatic | see "How the widget gets onto the home screen" |
| Scheduled refresh interval | 6 hours | also runnable on demand; the only refresh trigger by default |
| Refresh automatically after playback | off | opt-in; adds refreshes scattered through the day instead of only on schedule |
| Auto-refresh debounce | 3 minutes | wait this long after the last watched item (only relevant if the above is on) |

## API

- `GET /Recommendations/{userId}` — current recommendations for a user (served from cache,
  computed and cached on a miss). Requires an authenticated Jellyfin session/API key. This is
  what the home screen widget itself calls.
- `POST /Recommendations/Refresh` — queues a cache refresh for every user on Jellyfin's
  background task queue and returns immediately. Requires authentication.
- `GET /PersonalRecommendations/script` — the widget's client script. Deliberately
  unauthenticated (it's loaded via a bare `<script src>` before any session exists); the data it
  goes on to fetch is still authenticated as normal.

## Known limitations

- Recommendations are drawn only from your existing library — this plugin doesn't discover or
  suggest content you don't already have (that's a different kind of plugin, e.g. one that
  integrates with the *arr stack).
- The home screen widget only works in the web client and apps built on it — see "How the
  widget gets onto the home screen" above.
- Verified by building against the real `Jellyfin.Controller`/`Jellyfin.Model` 10.11.11
  packages, unit-testing the scoring logic, and confirming on a real Jellyfin 10.11.11 server
  that recommendations generate correctly. The DOM injection/rendering itself can't be tested
  from this project's dev environment (no browser, no live server) — it's built directly from
  Editor's Choice's proven, working source rather than guessed, but the first real-server check
  after installing 0.3.0 is still the actual test of that part.
