using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Configuration;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Creates and updates the per-user "Recommended For You" playlist.
/// </summary>
/// <remarks>
/// Playlists are used instead of collections because Jellyfin's <see cref="MediaBrowser.Controller.Collections.ICollectionManager"/>
/// ignores <see cref="MediaBrowser.Controller.Collections.CollectionCreationOptions.UserIds"/> entirely — a
/// collection created that way is visible to every user, not just the intended one. Playlists have a real
/// <c>OwnerUserId</c> and are only visible to their owner (and anyone they're explicitly shared with), which is
/// what "personal" recommendations need.
///
/// One consequence: adding a Series to a playlist makes Jellyfin expand it into every one of its episodes
/// (playlists are flat, playable-item lists), which would turn 20 recommendations into hundreds of entries. To
/// keep one entry per recommendation, a recommended series is represented by its next unwatched episode instead
/// of the series item itself.
/// </remarks>
public sealed class RecommendationPlaylistService
{
    private readonly IPlaylistManager _playlistManager;
    private readonly IUserDataManager _userDataManager;
    private readonly ILogger<RecommendationPlaylistService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationPlaylistService"/> class.
    /// </summary>
    /// <param name="playlistManager">Jellyfin's playlist manager.</param>
    /// <param name="userDataManager">Jellyfin's user data manager.</param>
    /// <param name="logger">Logger.</param>
    public RecommendationPlaylistService(IPlaylistManager playlistManager, IUserDataManager userDataManager, ILogger<RecommendationPlaylistService> logger)
    {
        _playlistManager = playlistManager;
        _userDataManager = userDataManager;
        _logger = logger;
    }

    /// <summary>
    /// Creates or updates the user's managed recommendation playlist to contain exactly the given
    /// recommendations (series are represented by their next unwatched episode).
    /// </summary>
    /// <param name="user">The user the playlist belongs to.</param>
    /// <param name="recommendations">The recommended items, in order.</param>
    /// <param name="snapshot">The library snapshot used to resolve a series' next unwatched episode.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdatePlaylistAsync(User user, IReadOnlyList<LibraryCandidate> recommendations, LibrarySnapshot snapshot, PluginConfiguration config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var desiredIds = recommendations
            .Select(r => ResolvePlayableItemId(r, user, snapshot))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var name = BuildPlaylistName(config.PlaylistNameTemplate, user.Username);
        var mapping = config.PlaylistMappings.FirstOrDefault(m => m.UserId == user.Id);
        var existing = mapping is not null ? _playlistManager.GetPlaylistForUser(mapping.PlaylistId, user.Id) : null;

        if (existing is null)
        {
            if (desiredIds.Length == 0)
            {
                return;
            }

            var result = await _playlistManager.CreatePlaylist(new PlaylistCreationRequest
            {
                Name = name,
                ItemIdList = desiredIds,
                UserId = user.Id,
                Public = false
            }).ConfigureAwait(false);

            var playlistId = Guid.Parse(result.Id);
            if (mapping is null)
            {
                config.PlaylistMappings.Add(new UserPlaylistMapping { UserId = user.Id, PlaylistId = playlistId });
            }
            else
            {
                mapping.PlaylistId = playlistId;
            }

            Plugin.Instance?.SaveConfiguration();
            _logger.LogInformation("Created recommendation playlist '{Name}' for user {UserId} with {Count} items.", name, user.Id, desiredIds.Length);
            return;
        }

        await _playlistManager.UpdatePlaylist(new PlaylistUpdateRequest
        {
            Id = existing.Id,
            UserId = user.Id,
            Name = name,
            Ids = desiredIds
        }).ConfigureAwait(false);

        _logger.LogInformation("Updated recommendation playlist '{Name}' for user {UserId} with {Count} items.", name, user.Id, desiredIds.Length);
    }

    private Guid? ResolvePlayableItemId(LibraryCandidate candidate, User user, LibrarySnapshot snapshot)
    {
        if (!string.Equals(candidate.ItemType, "Series", StringComparison.OrdinalIgnoreCase))
        {
            return candidate.ItemId;
        }

        if (!snapshot.EpisodesBySeriesId.TryGetValue(candidate.ItemId, out var episodes) || episodes.Count == 0)
        {
            return null;
        }

        var nextUnwatched = episodes.FirstOrDefault(e =>
        {
            var data = _userDataManager.GetUserData(user, e) ?? new UserItemData { Key = string.Empty };
            return !data.Played && data.PlayCount == 0;
        });

        return (nextUnwatched ?? episodes[0]).Id;
    }

    private static string BuildPlaylistName(string template, string username)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Recommended For You";
        }

        return template.Replace("{username}", username, StringComparison.OrdinalIgnoreCase);
    }
}
