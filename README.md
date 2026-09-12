# Personal Recommendations for Jellyfin

A Jellyfin server plugin that builds a per-user taste profile from watch history (genres,
cast, crew, studios, decade) and maintains a private **"Recommended For You"** playlist for
each user — no external services, no API keys, nothing but your own library and watch history.

Targets **Jellyfin 10.11.x** (builds against `Jellyfin.Controller`/`Jellyfin.Model` 10.11.11, .NET 9).

## How it works

1. For every user, it looks at what they've watched, favorited, liked, or rated — movies
   directly, series by aggregating episode-level playback (Jellyfin doesn't reliably track
   play count on the Series item itself).
2. It builds a weighted taste profile from genres, directors, top-billed actors, studios, and
   decade, with older activity decaying gradually rather than disappearing.
3. Every unwatched movie/series in the library is scored against that profile.
4. The top results (capped so one studio/franchise can't dominate the list) become the user's
   "Recommended For You" playlist, kept up to date by a scheduled task (default every 6 hours)
   and, optionally, a debounced refresh shortly after the user finishes watching something.
   A recommended series is represented in the playlist by its next unwatched episode rather
   than the series itself — see "Why a playlist, not a collection" below for why.
5. A user with no watch history yet gets a cold-start fallback: the highest community-rated
   unwatched titles.

## Why a playlist, not a collection

The first version of this plugin used Jellyfin **collections** for this, matching a common
pattern in other Jellyfin recommendation plugins. That turned out to be wrong: Jellyfin's
`ICollectionManager.CreateCollectionAsync` completely ignores the per-user `UserIds` option —
a collection created that way is a normal, globally shared collection, visible to *every* user,
not just the one it was generated for. For a plugin whose whole point is *personal*
recommendations, that's a real privacy bug, not a cosmetic one.

**Playlists** don't have this problem — a Jellyfin playlist has a real `OwnerUserId` and is only
visible to its owner (and anyone explicitly shared with), which is exactly what's needed here.

The trade-off: playlists are flat, playable-item lists. Adding a Series to a playlist makes
Jellyfin silently expand it into *every one of its episodes* — 20 recommendations could balloon
into hundreds of playlist entries. To avoid that, a recommended series is added to the playlist
as its next unwatched episode (S1E1 if nothing's been watched yet), which keeps it to one entry
per recommendation and doubles as "here's where to jump in."

## Project layout

- `Jellyfin.Plugin.PersonalRecommendations.Core/` — the pure scoring/profile-building logic.
  No Jellyfin dependency, so it's plain unit-testable.
- `Jellyfin.Plugin.PersonalRecommendations/` — the actual Jellyfin plugin: reads watch history
  and the library via Jellyfin's APIs, manages the per-user playlist, exposes the scheduled
  task, the config page, and the admin API.
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
containing the plugin zip and a `manifest.json` whenever a tag like `v0.2.0` is pushed (or via
"Run workflow" in the Actions tab).

1. Push a tag, e.g. `git tag v0.2.0 && git push origin v0.2.0`, and wait for the "Release"
   workflow to finish (Actions tab).
2. In Jellyfin, go to **Dashboard → Plugins → Repositories → Add Repository** and add:
   - Repository name: anything, e.g. `Personal Recommendations`
   - Repository URL: `https://github.com/<owner>/<repo>/releases/latest/download/manifest.json`
3. Go to **Catalog**, find "Personal Recommendations" under General, install it, and restart
   Jellyfin when prompted.
4. To update later: bump the version, push a new tag, and Jellyfin will offer the update from
   the same repository the normal way.

## Install manually (no GitHub needed)

1. Build in Release mode (above).
2. Copy these files from `Jellyfin.Plugin.PersonalRecommendations/bin/Release/net9.0/` into a
   new folder under your Jellyfin server's plugin directory, e.g.
   `<jellyfin-config>/plugins/PersonalRecommendations_0.2.0.0/`:
   - `Jellyfin.Plugin.PersonalRecommendations.dll`
   - `Jellyfin.Plugin.PersonalRecommendations.Core.dll`
   - `meta.json`
3. Restart Jellyfin.

## After installing, either way

1. Check **Dashboard → Plugins** — "Personal Recommendations" should be listed and enabled.
   A freshly (re)installed plugin shows **Status: Restart** — it isn't actually loaded (no
   config page, no scheduled task) until you restart the server.
2. After restarting, its settings page should appear as its own entry under **Plugins** in the
   left nav (not just the plugin info card) — that's where the actual settings and the
   **Refresh recommendations now** button live.
3. Either wait for the scheduled task or click **Refresh recommendations now**.
4. Check **Dashboard → Scheduled Tasks → Personal Recommendations → Refresh personal
   recommendations** for logs/manual runs.
5. As a user with some watch history, look for the "Recommended For You" playlist in the main
   Jellyfin web/app UI (not the admin dashboard) under Playlists.

## Configuration

Available on the plugin's settings page (Dashboard → Plugins → Personal Recommendations):

| Setting | Default | Notes |
|---|---|---|
| Enabled | on | |
| Recommendations per user | 20 | |
| Playlist name | `Recommended For You` | supports `{username}` |
| Max results per studio | 3 | 0 disables the cap |
| Minimum community rating | 0 (off) | |
| Recommend movies / series | both on | |
| Scheduled refresh interval | 6 hours | also runnable on demand |
| Refresh automatically after playback | on | debounced |
| Auto-refresh debounce | 3 minutes | wait this long after the last watched item |

## API

All endpoints require an authenticated Jellyfin session/API key.

- `GET /Recommendations/{userId}` — current recommendations for a user, computed on demand
  (does not touch the managed playlist).
- `POST /Recommendations/Refresh` — refresh recommendations and the managed playlist for
  every user.
- `POST /Recommendations/Refresh/{userId}` — refresh for one user.

## Known limitations

- Recommendations are drawn only from your existing library — this plugin doesn't discover or
  suggest content you don't already have (that's a different kind of plugin, e.g. one that
  integrates with the *arr stack).
- A recommended series shows up as its next unwatched episode, not the series itself (see "Why
  a playlist, not a collection" above).
- Verified by building against the real `Jellyfin.Controller`/`Jellyfin.Model` 10.11.11
  packages, unit-testing the scoring logic, and confirming on a real Jellyfin 10.11.11 server
  that recommendations generate correctly (20 items from 1922 candidates on a real library) —
  the collection-vs-playlist privacy issue above was itself found this way, after the 0.1.0
  release, and fixed in 0.2.0.
