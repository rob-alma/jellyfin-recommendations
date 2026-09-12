using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Reads a user's watch history from Jellyfin and turns it into <see cref="WatchSignal"/>s.
/// </summary>
public sealed class WatchHistoryReader
{
    private readonly IUserDataManager _userDataManager;
    private readonly ItemFeatureExtractor _featureExtractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchHistoryReader"/> class.
    /// </summary>
    /// <param name="userDataManager">Jellyfin's user data manager.</param>
    /// <param name="featureExtractor">Extracts genres/people/studios from items.</param>
    public WatchHistoryReader(IUserDataManager userDataManager, ItemFeatureExtractor featureExtractor)
    {
        _userDataManager = userDataManager;
        _featureExtractor = featureExtractor;
    }

    /// <summary>
    /// Builds the user's watch signals from a library snapshot.
    /// </summary>
    /// <param name="user">The user to build signals for.</param>
    /// <param name="snapshot">A snapshot of the library.</param>
    public IReadOnlyList<WatchSignal> BuildSignals(User user, LibrarySnapshot snapshot)
    {
        var signals = new List<WatchSignal>();

        foreach (var movie in snapshot.Movies)
        {
            var userData = _userDataManager.GetUserData(user, movie) ?? new UserItemData { Key = string.Empty };
            if (!HasInteraction(userData))
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
                userData.PlayCount,
                userData.IsFavorite,
                userData.Likes,
                userData.Rating,
                ToDateTimeOffset(userData.LastPlayedDate)));
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

            var episodeUserData = seriesEpisodes.Select(e => _userDataManager.GetUserData(user, e) ?? new UserItemData { Key = string.Empty }).ToList();
            var watchedEpisodeCount = episodeUserData.Count(d => d.Played || d.PlayCount > 0);
            if (watchedEpisodeCount == 0)
            {
                continue;
            }

            var seriesUserData = _userDataManager.GetUserData(user, series) ?? new UserItemData { Key = string.Empty };
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
                seriesUserData.IsFavorite,
                seriesUserData.Likes,
                seriesUserData.Rating,
                ToDateTimeOffset(lastPlayed == default ? null : lastPlayed)));
        }

        return signals;
    }

    private static bool HasInteraction(UserItemData data) =>
        data.PlayCount > 0 || data.Played || data.IsFavorite || data.Likes.HasValue;

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
}
