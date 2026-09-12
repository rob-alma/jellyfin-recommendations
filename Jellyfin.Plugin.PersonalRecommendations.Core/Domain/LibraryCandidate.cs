using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.PersonalRecommendations.Domain;

/// <summary>
/// A movie or series that could be recommended, projected down to the fields the scorer needs.
/// </summary>
/// <param name="ItemId">The Jellyfin item id.</param>
/// <param name="Name">Display name.</param>
/// <param name="ItemType">"Movie" or "Series".</param>
/// <param name="Genres">Genres.</param>
/// <param name="Studios">Studios.</param>
/// <param name="Directors">Directors.</param>
/// <param name="Actors">Top-billed actors.</param>
/// <param name="ProductionYear">Production year, if known.</param>
/// <param name="CommunityRating">Community rating, if known.</param>
/// <param name="GroupKey">A grouping key (typically the primary studio) used to cap how many similar items land in one recommendation batch.</param>
public sealed record LibraryCandidate(
    Guid ItemId,
    string Name,
    string ItemType,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Studios,
    IReadOnlyList<string> Directors,
    IReadOnlyList<string> Actors,
    int? ProductionYear,
    float? CommunityRating,
    string GroupKey);
