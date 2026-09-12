using System;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Projects Jellyfin <see cref="BaseItem"/> instances into the plain DTOs the pure
/// recommendation logic works with.
/// </summary>
public sealed class ItemFeatureExtractor
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemFeatureExtractor"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin's library manager.</param>
    public ItemFeatureExtractor(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Projects an item into a <see cref="LibraryCandidate"/>.
    /// </summary>
    /// <param name="item">The item to project.</param>
    public LibraryCandidate ToCandidate(BaseItem item)
    {
        var (directors, actors) = GetPeople(item);
        var studios = item.Studios ?? [];

        return new LibraryCandidate(
            item.Id,
            item.Name,
            item.GetClientTypeName(),
            item.Genres ?? [],
            studios,
            directors,
            actors,
            item.ProductionYear,
            item.CommunityRating,
            studios.FirstOrDefault() ?? item.GetClientTypeName());
    }

    /// <summary>
    /// Gets the directors and top-billed actors for an item.
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    public (string[] Directors, string[] Actors) GetPeople(BaseItem item)
    {
        var people = _libraryManager.GetPeople(item);

        var directors = people
            .Where(p => p.Type == PersonKind.Director && !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var actors = people
            .Where(p => p.Type == PersonKind.Actor && !string.IsNullOrWhiteSpace(p.Name))
            .OrderBy(p => p.SortOrder ?? int.MaxValue)
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return (directors, actors);
    }
}
