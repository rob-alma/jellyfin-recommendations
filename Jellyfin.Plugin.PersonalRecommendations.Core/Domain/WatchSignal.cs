using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.PersonalRecommendations.Domain;

/// <summary>
/// A single piece of evidence about what a user has watched and liked, decoupled from
/// Jellyfin's own entities so the taste-profile math can be unit tested without a server.
/// </summary>
/// <param name="ItemId">The Jellyfin item id this signal came from (a movie or a series).</param>
/// <param name="Genres">Genres of the watched item.</param>
/// <param name="Studios">Studios of the watched item.</param>
/// <param name="Directors">Directors of the watched item.</param>
/// <param name="Actors">Top-billed actors of the watched item.</param>
/// <param name="ProductionYear">Production year, if known.</param>
/// <param name="PlayCount">How many times the user played it (or, for a series, how many episodes were watched).</param>
/// <param name="IsFavorite">Whether the user marked it as a favorite.</param>
/// <param name="Likes">Explicit like/dislike, if the user set one.</param>
/// <param name="Rating">Explicit numeric rating, if the user set one.</param>
/// <param name="LastPlayedDate">When the user last played it.</param>
public sealed record WatchSignal(
    Guid ItemId,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Studios,
    IReadOnlyList<string> Directors,
    IReadOnlyList<string> Actors,
    int? ProductionYear,
    int PlayCount,
    bool IsFavorite,
    bool? Likes,
    double? Rating,
    DateTimeOffset? LastPlayedDate);
