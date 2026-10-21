using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Reads why a user took titles out of Continue Watching in the Streamline plugin (when it's
/// installed), so those reasons shape recommendations too. Streamline keeps them per user in
/// its own data folder; titles it saved as a rating or as watched already reach this plugin
/// through Jellyfin's user data, so only two reasons matter here:
/// <list type="bullet">
/// <item>lost-interest: a mild dislike, and never recommended again.</item>
/// <item>not-me: someone else watched it on their account; not part of their taste.</item>
/// </list>
/// </summary>
public sealed class StreamlineCleanupReader
{
    /// <summary>Lost interest: a mild dislike.</summary>
    public const string LostInterest = "lost-interest";

    /// <summary>Someone else watched it: not theirs.</summary>
    public const string NotMe = "not-me";

    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<StreamlineCleanupReader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamlineCleanupReader"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin's paths (Streamline's data sits under the plugins folder).</param>
    /// <param name="logger">Logger.</param>
    public StreamlineCleanupReader(IApplicationPaths applicationPaths, ILogger<StreamlineCleanupReader> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <summary>
    /// Gets the titles a user took out of Continue Watching, with the reason. Empty without Streamline.
    /// </summary>
    /// <param name="userId">The user.</param>
    public IReadOnlyDictionary<Guid, string> Read(Guid userId)
    {
        var path = Path.Combine(_applicationPaths.PluginsPath, "Jellyfin.Plugin.Streamline", "continuewatching", userId.ToString("N") + ".json");
        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<Guid, string>();
            }

            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path)) ?? [];
            return entries
                .Where(entry => entry.Reason is LostInterest or NotMe)
                .GroupBy(entry => entry.Id)
                .ToDictionary(group => group.Key, group => group.First().Reason);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Couldn't read Streamline's Continue Watching clean-up for {User}.", userId);
            return new Dictionary<Guid, string>();
        }
    }

    private sealed class Entry
    {
        public Guid Id { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}
