using System;

namespace Jellyfin.Plugin.PersonalRecommendations.Api.Dto;

/// <summary>
/// A recommended item, as returned by the plugin's API.
/// </summary>
public sealed class RecommendedItemDto
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type ("Movie" or "Series").
    /// </summary>
    public string ItemType { get; set; } = string.Empty;
}
