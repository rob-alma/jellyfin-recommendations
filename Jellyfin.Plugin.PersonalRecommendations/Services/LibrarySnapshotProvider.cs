using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// A one-time snapshot of the library's movies, series and episodes, so a single refresh pass
/// (scheduled task or per-user trigger) only queries the library once instead of per user.
/// </summary>
public sealed class LibrarySnapshot
{
    /// <summary>
    /// Gets all non-virtual movies.
    /// </summary>
    public required IReadOnlyList<BaseItem> Movies { get; init; }

    /// <summary>
    /// Gets all non-virtual series.
    /// </summary>
    public required IReadOnlyList<BaseItem> Series { get; init; }

    /// <summary>
    /// Gets all non-virtual episodes.
    /// </summary>
    public required IReadOnlyList<BaseItem> Episodes { get; init; }

    /// <summary>
    /// Gets the series, indexed by id.
    /// </summary>
    public required IReadOnlyDictionary<Guid, BaseItem> SeriesById { get; init; }

    /// <summary>
    /// Gets each series' episodes, indexed by series id and pre-sorted by season/episode
    /// number, so callers don't each re-scan the full episode list.
    /// </summary>
    public required IReadOnlyDictionary<Guid, IReadOnlyList<Episode>> EpisodesBySeriesId { get; init; }
}

/// <summary>
/// Builds a <see cref="LibrarySnapshot"/> from Jellyfin's library manager.
/// </summary>
public sealed class LibrarySnapshotProvider
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySnapshotProvider"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin's library manager.</param>
    public LibrarySnapshotProvider(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Queries the library once for all movies, series and episodes.
    /// </summary>
    public LibrarySnapshot GetSnapshot()
    {
        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Movie],
            IsVirtualItem = false
        });

        var series = _libraryManager.GetItemList(new InternalItemsQuery
        {
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Series],
            IsVirtualItem = false
        });

        var episodes = _libraryManager.GetItemList(new InternalItemsQuery
        {
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Episode],
            IsVirtualItem = false
        });

        var episodesBySeriesId = episodes
            .OfType<Episode>()
            .GroupBy(e => e.SeriesId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Episode>)g
                    .OrderBy(e => e.ParentIndexNumber ?? int.MaxValue)
                    .ThenBy(e => e.IndexNumber ?? int.MaxValue)
                    .ToList());

        return new LibrarySnapshot
        {
            Movies = movies,
            Series = series,
            Episodes = episodes,
            SeriesById = series.ToDictionary(s => s.Id),
            EpisodesBySeriesId = episodesBySeriesId
        };
    }
}
