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
        ScheduledRefreshIntervalHours = 6;
        AutoRefreshAfterPlayback = false;
        AutoRefreshDebounceMinutes = 3;
        IncludeMovies = true;
        IncludeSeries = true;
        MinimumCommunityRating = 0;
        DiversityCapPerGroup = 3;
        HomeScreenWidgetEnabled = true;
        FrontendInjectionMethod = FrontendInjectionMethods.Automatic;
        WidgetHeading = "Recommended For You";
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
    /// Gets or sets the scheduled refresh interval, in hours.
    /// </summary>
    public int ScheduledRefreshIntervalHours { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether recommendations refresh automatically a few
    /// minutes after a user finishes watching something, in addition to the scheduled task.
    /// Off by default so a server only pays the (single-threaded, see <see cref="Services.ComputeGate"/>)
    /// cost of a refresh on the schedule the admin set, not scattered through the day as people watch things.
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
    /// Gets or sets a value indicating whether the "Recommended For You" home screen widget is enabled.
    /// </summary>
    public bool HomeScreenWidgetEnabled { get; set; }

    /// <summary>
    /// Gets or sets which mechanism is used to inject the widget's script into the web client.
    /// One of <see cref="FrontendInjectionMethods"/>.
    /// </summary>
    public string FrontendInjectionMethod { get; set; }

    /// <summary>
    /// Gets or sets the heading shown above the widget's row.
    /// </summary>
    public string WidgetHeading { get; set; }

    /// <summary>
    /// Gets or sets the per-user playlist ids created by versions up to 0.2.1, before
    /// recommendations were delivered as a home screen widget instead of a playlist. Read once
    /// by <see cref="Services.LegacyPlaylistCleanupService"/> to remove those playlists, then
    /// always empty. The property name must not change: Jellyfin's XML config serializer
    /// matches by name, and renaming it would silently lose the ids of playlists to clean up.
    /// </summary>
    public List<UserPlaylistMapping> PlaylistMappings { get; set; }
}

/// <summary>
/// Tracks a playlist created by a pre-0.3.0 version of the plugin, for one-time cleanup.
/// </summary>
public class UserPlaylistMapping
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the playlist id.
    /// </summary>
    public Guid PlaylistId { get; set; }
}
