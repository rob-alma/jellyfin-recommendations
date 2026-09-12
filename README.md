# Personal Recommendations for Jellyfin

A Jellyfin server plugin that builds a per-user taste profile from watch history (genres,
cast, crew, studios, decade) and maintains a **"Recommended For You"** collection for each
user — no external services, no API keys, nothing but your own library and watch history.

Targets **Jellyfin 10.11.x** (builds against `Jellyfin.Controller`/`Jellyfin.Model` 10.11.11, .NET 9).

## How it works

1. For every user, it looks at what they've watched, favorited, liked, or rated — movies
   directly, series by aggregating episode-level playback (Jellyfin doesn't reliably track
   play count on the Series item itself).
2. It builds a weighted taste profile from genres, directors, top-billed actors, studios, and
   decade, with older activity decaying gradually rather than disappearing.
3. Every unwatched movie/series in the library is scored against that profile.
4. The top results (capped so one studio/franchise can't dominate the list) become the user's
   "Recommended For You" collection, kept up to date by a scheduled task (default every 6
   hours) and, optionally, a debounced refresh shortly after the user finishes watching
   something.
5. A user with no watch history yet gets a cold-start fallback: the highest community-rated
   unwatched titles.

## Project layout

- `Jellyfin.Plugin.PersonalRecommendations.Core/` — the pure scoring/profile-building logic.
  No Jellyfin dependency, so it's plain unit-testable.
- `Jellyfin.Plugin.PersonalRecommendations/` — the actual Jellyfin plugin: reads watch history
  and the library via Jellyfin's APIs, manages the per-user collection, exposes the scheduled
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
containing the plugin zip and a `manifest.json` whenever a tag like `v0.1.0` is pushed (or via
"Run workflow" in the Actions tab).

1. Push a tag, e.g. `git tag v0.1.0 && git push origin v0.1.0`, and wait for the "Release"
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
   `<jellyfin-config>/plugins/PersonalRecommendations_0.1.0.0/`:
   - `Jellyfin.Plugin.PersonalRecommendations.dll`
   - `Jellyfin.Plugin.PersonalRecommendations.Core.dll`
   - `meta.json`
3. Restart Jellyfin.

## After installing, either way

1. Check **Dashboard → Plugins** — "Personal Recommendations" should be listed and enabled.
2. Open its settings page to review/adjust configuration, then either wait for the scheduled
   task or click **Refresh recommendations now**.
3. Check **Dashboard → Scheduled Tasks → Personal Recommendations → Refresh personal
   recommendations** for logs/manual runs.
4. As a user with some watch history, look for the "Recommended For You" collection.

## Configuration

Available on the plugin's settings page (Dashboard → Plugins → Personal Recommendations):

| Setting | Default | Notes |
|---|---|---|
| Enabled | on | |
| Recommendations per user | 20 | |
| Collection name | `Recommended For You` | supports `{username}` |
| Max results per studio | 3 | 0 disables the cap |
| Minimum community rating | 0 (off) | |
| Recommend movies / series | both on | |
| Scheduled refresh interval | 6 hours | also runnable on demand |
| Refresh automatically after playback | on | debounced |
| Auto-refresh debounce | 3 minutes | wait this long after the last watched item |

## API

All endpoints require an authenticated Jellyfin session/API key.

- `GET /Recommendations/{userId}` — current recommendations for a user, computed on demand
  (does not touch the managed collection).
- `POST /Recommendations/Refresh` — refresh recommendations and the managed collection for
  every user.
- `POST /Recommendations/Refresh/{userId}` — refresh for one user.

## Known limitations

- Recommendations are drawn only from your existing library — this plugin doesn't discover or
  suggest content you don't already have (that's a different kind of plugin, e.g. one that
  integrates with the *arr stack).
- Verified by building against the real `Jellyfin.Controller`/`Jellyfin.Model` 10.11.11
  packages and unit-testing the scoring logic; it has not been loaded into a live Jellyfin
  server yet (no Docker available in the environment this was built in). Please do the manual
  install steps above and confirm end to end on a real server before relying on it.
