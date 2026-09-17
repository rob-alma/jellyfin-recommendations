using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Turns a user's watch history into <see cref="WatchSignal"/>s.
/// </summary>
public sealed class WatchHistoryReader
{
    private readonly ItemFeatureExtractor _featureExtractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchHistoryReader"/> class.
    /// </summary>
    /// <param name="featureExtractor">Extracts genres/people/studios from items.</param>
    public WatchHistoryReader(ItemFeatureExtractor featureExtractor)
    {
        _featureExtractor = featureExtractor;
    }

    /// <summary>
    /// Builds the user's watch signals from a library snapshot.
    /// </summary>
    /// <param name="snapshot">A snapshot of the library.</param>
    /// <param name="userData">Each item's user data, from <see cref="UserDataLookup"/>.</param>
    public IReadOnlyList<WatchSignal> BuildSignals(LibrarySnapshot snapshot, IReadOnlyDictionary<Guid, UserItemData> userData)
    {
        var signals = new List<WatchSignal>();

        foreach (var movie in snapshot.Movies)
        {
            var data = userData.TryGetValue(movie.Id, out var value) ? value : new UserItemData { Key = string.Empty };
            if (!HasInteraction(data))
            {
                continue;
            }

            var (directors, actors) = _featureExtractor.GetPeople(movie);
            signals.Add(new WatchSignal(
                movie.Id,
                movie.Genres ?? [],
                movie.Studios ?? [],
                directors,
                actors,
                movie.ProductionYear,
                data.PlayCount,
                data.IsFavorite,
                data.Likes,
                data.Rating,
                ToDateTimeOffset(data.LastPlayedDate)));
        }

        // Jellyfin only persists play count / played state reliably on leaf items (episodes),
        // not on the Series item itself, so series-level watch signal is derived by aggregating
        // its episodes. IsFavorite/Likes/Rating *are* stored directly against the Series item
        // (a user favorites the show itself), so those are still read from the Series row.
        foreach (var (seriesId, seriesEpisodes) in snapshot.EpisodesBySeriesId)
        {
            if (!snapshot.SeriesById.TryGetValue(seriesId, out var series))
            {
                continue;
            }

            var episodeUserData = seriesEpisodes
                .Select(e => userData.TryGetValue(e.Id, out var value) ? value : new UserItemData { Key = string.Empty })
                .ToList();
            var watchedEpisodeCount = episodeUserData.Count(d => d.Played || d.PlayCount > 0);
            if (watchedEpisodeCount == 0)
            {
                continue;
            }

            var seriesData = userData.TryGetValue(series.Id, out var seriesValue) ? seriesValue : new UserItemData { Key = string.Empty };
            var lastPlayed = episodeUserData
                .Where(d => d.LastPlayedDate.HasValue)
                .Select(d => d.LastPlayedDate!.Value)
                .DefaultIfEmpty()
                .Max();

            var (directors, actors) = _featureExtractor.GetPeople(series);
            signals.Add(new WatchSignal(
                series.Id,
                series.Genres ?? [],
                series.Studios ?? [],
                directors,
                actors,
                series.ProductionYear,
                watchedEpisodeCount,
                seriesData.IsFavorite,
                seriesData.Likes,
                seriesData.Rating,
                ToDateTimeOffset(lastPlayed == default ? null : lastPlayed)));
        }

        return signals;
    }

    private static bool HasInteraction(UserItemData data) =>
        data.PlayCount > 0 || data.Played || data.IsFavorite || data.Likes.HasValue;

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
}
