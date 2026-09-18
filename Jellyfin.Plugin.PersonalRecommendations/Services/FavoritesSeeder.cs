using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Configuration;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Marks a handful of top recommendations as favorites, once, for an account that has never
/// favorited anything itself. Purely cosmetic (so Jellyfin's own Favorites tab isn't empty) -
/// an account with even one existing favorite is never touched, and each account is only ever
/// considered once even if it later loses every favorite again. See
/// <see cref="PluginConfiguration.SeedFavoritesForNewUsers"/>.
/// </summary>
public sealed class FavoritesSeeder
{
    private const int SeedCount = 3;

    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly ILogger<FavoritesSeeder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FavoritesSeeder"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin's library manager.</param>
    /// <param name="userDataManager">Jellyfin's user data manager.</param>
    /// <param name="logger">Logger.</param>
    public FavoritesSeeder(ILibraryManager libraryManager, IUserDataManager userDataManager, ILogger<FavoritesSeeder> logger)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _logger = logger;
    }

    /// <summary>
    /// Seeds favorites for <paramref name="user"/> if it qualifies (see class remarks).
    /// </summary>
    /// <param name="user">The user recommendations were just computed for.</param>
    /// <param name="userData">That user's data for every item in the library snapshot (already
    /// fetched for scoring - reused here instead of a fresh favorites query).</param>
    /// <param name="recommendations">That user's freshly computed, ranked recommendations.</param>
    /// <param name="config">Plugin configuration.</param>
    public void MaybeSeed(
        User user,
        IReadOnlyDictionary<Guid, UserItemData> userData,
        IReadOnlyList<LibraryCandidate> recommendations,
        PluginConfiguration config)
    {
        if (!config.SeedFavoritesForNewUsers || config.FavoritesSeedCompletedUserIds.Contains(user.Id))
        {
            return;
        }

        if (userData.Values.Any(d => d.IsFavorite))
        {
            MarkCompleted(user.Id, config);
            return;
        }

        var seeded = 0;
        foreach (var candidate in recommendations.Take(SeedCount))
        {
            try
            {
                var item = _libraryManager.GetItemById(candidate.ItemId);
                if (item is null)
                {
                    continue;
                }

                var data = userData.TryGetValue(candidate.ItemId, out var existing)
                    ? existing
                    : new UserItemData { Key = candidate.ItemId.ToString() };
                data.IsFavorite = true;
                _userDataManager.SaveUserData(user, item, data, UserDataSaveReason.UpdateUserData, CancellationToken.None);
                seeded++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to seed favorite {ItemId} for user {UserId}.", candidate.ItemId, user.Id);
            }
        }

        if (seeded > 0)
        {
            _logger.LogInformation("Seeded {Count} favorite(s) for user {UserId}, who had none.", seeded, user.Id);
        }

        MarkCompleted(user.Id, config);
    }

    private static void MarkCompleted(Guid userId, PluginConfiguration config)
    {
        config.FavoritesSeedCompletedUserIds.Add(userId);
        Plugin.Instance?.SaveConfiguration();
    }
}
