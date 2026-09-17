using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// One-time cleanup for users upgrading from a version (0.1.0-0.2.1) that delivered
/// recommendations as a per-user playlist. Removes those playlists on the first startup after
/// upgrading, since recommendations are now shown as a home screen widget instead.
/// </summary>
public sealed class LegacyPlaylistCleanupService : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LegacyPlaylistCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyPlaylistCleanupService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin's library manager.</param>
    /// <param name="logger">Logger.</param>
    public LegacyPlaylistCleanupService(ILibraryManager libraryManager, ILogger<LegacyPlaylistCleanupService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || config.PlaylistMappings.Count == 0)
        {
            return Task.CompletedTask;
        }

        var removed = 0;
        foreach (var mapping in config.PlaylistMappings)
        {
            try
            {
                var item = _libraryManager.GetItemById(mapping.PlaylistId);
                if (item is not null)
                {
                    _libraryManager.DeleteItem(item, new DeleteOptions { DeleteFileLocation = true });
                    removed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove legacy recommendation playlist for user {UserId}.", mapping.UserId);
            }
        }

        config.PlaylistMappings.Clear();
        Plugin.Instance?.SaveConfiguration();
        _logger.LogInformation("Removed {Count} legacy recommendation playlist(s) from pre-0.3.0 versions.", removed);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
