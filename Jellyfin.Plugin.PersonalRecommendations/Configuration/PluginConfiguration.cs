using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PersonalRecommendations.Configuration;

/// <summary>
/// Plugin configuration persisted by Jellyfin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Enabled = true;
        RecommendationLimit = 20;
        PlaylistNameTemplate = "Recommended For You";
        ScheduledRefreshIntervalHours = 6;
        AutoRefreshAfterPlayback = true;
        AutoRefreshDebounceMinutes = 3;
        IncludeMovies = true;
        IncludeSeries = true;
        MinimumCommunityRating = 0;
        DiversityCapPerGroup = 3;
        PlaylistMappings = new List<UserPlaylistMapping>();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of recommendations to keep per user.
    /// </summary>
    public int RecommendationLimit { get; set; }

    /// <summary>
    /// Gets or sets the playlist name template. Supports the <c>{username}</c> placeholder.
    /// </summary>
    public string PlaylistNameTemplate { get; set; }

    /// <summary>
    /// Gets or sets the scheduled refresh interval, in hours.
    /// </summary>
    public int ScheduledRefreshIntervalHours { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether recommendations refresh automatically a few
    /// minutes after a user finishes watching something.
    /// </summary>
    public bool AutoRefreshAfterPlayback { get; set; }

    /// <summary>
    /// Gets or sets how many minutes to wait after the last playback change before refreshing,
    /// so a binge session doesn't trigger a refresh per episode.
    /// </summary>
    public int AutoRefreshDebounceMinutes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether movies are recommended.
    /// </summary>
    public bool IncludeMovies { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether series are recommended.
    /// </summary>
    public bool IncludeSeries { get; set; }

    /// <summary>
    /// Gets or sets the minimum community rating a candidate needs to be considered. Zero disables the filter.
    /// </summary>
    public double MinimumCommunityRating { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of recommendations allowed to share the same primary studio.
    /// </summary>
    public int DiversityCapPerGroup { get; set; }

    /// <summary>
    /// Gets or sets the per-user managed playlist ids, so refreshes update the same playlist
    /// instead of creating a new one every time.
    /// </summary>
    public List<UserPlaylistMapping> PlaylistMappings { get; set; }
}

/// <summary>
/// Tracks which Jellyfin playlist the plugin created for a given user.
/// </summary>
public class UserPlaylistMapping
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the managed playlist id.
    /// </summary>
    public Guid PlaylistId { get; set; }
}
