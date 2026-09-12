using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Configuration;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Creates and updates the per-user "Recommended For You" collection.
/// </summary>
public sealed class RecommendationCollectionService
{
    private readonly ICollectionManager _collectionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<RecommendationCollectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationCollectionService"/> class.
    /// </summary>
    /// <param name="collectionManager">Jellyfin's collection manager.</param>
    /// <param name="libraryManager">Jellyfin's library manager.</param>
    /// <param name="logger">Logger.</param>
    public RecommendationCollectionService(ICollectionManager collectionManager, ILibraryManager libraryManager, ILogger<RecommendationCollectionService> logger)
    {
        _collectionManager = collectionManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Creates or updates the user's managed recommendation collection to contain exactly the
    /// given items.
    /// </summary>
    /// <param name="user">The user the collection belongs to.</param>
    /// <param name="recommendations">The recommended items, in order.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateCollectionAsync(User user, IReadOnlyList<LibraryCandidate> recommendations, PluginConfiguration config, CancellationToken cancellationToken)
    {
        var desiredIds = recommendations.Select(r => r.ItemId).ToArray();
        var name = BuildCollectionName(config.CollectionNameTemplate, user.Username);

        var mapping = config.CollectionMappings.FirstOrDefault(m => m.UserId == user.Id);
        var existing = mapping is not null ? _libraryManager.GetItemById(mapping.CollectionId) : null;

        if (existing is null)
        {
            if (desiredIds.Length == 0)
            {
                return;
            }

            var boxSet = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
            {
                Name = name,
                UserIds = [user.Id],
                ItemIdList = desiredIds.Select(id => id.ToString("N")).ToArray(),
                IsLocked = true
            }).ConfigureAwait(false);

            if (mapping is null)
            {
                config.CollectionMappings.Add(new UserCollectionMapping { UserId = user.Id, CollectionId = boxSet.Id });
            }
            else
            {
                mapping.CollectionId = boxSet.Id;
            }

            Plugin.Instance?.SaveConfiguration();
            _logger.LogInformation("Created recommendation collection '{Name}' for user {UserId} with {Count} items.", name, user.Id, desiredIds.Length);
            return;
        }

        if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
        {
            existing.Name = name;
            await existing.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        }

        var currentIds = _libraryManager
            .GetItemList(new InternalItemsQuery { ParentId = existing.Id })
            .Select(i => i.Id)
            .ToHashSet();
        var desiredSet = desiredIds.ToHashSet();

        var toRemove = currentIds.Where(id => !desiredSet.Contains(id)).ToArray();
        var toAdd = desiredSet.Where(id => !currentIds.Contains(id)).ToArray();

        if (toRemove.Length > 0)
        {
            await _collectionManager.RemoveFromCollectionAsync(existing.Id, toRemove).ConfigureAwait(false);
        }

        if (toAdd.Length > 0)
        {
            await _collectionManager.AddToCollectionAsync(existing.Id, toAdd).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Updated recommendation collection '{Name}' for user {UserId}: +{Added} -{Removed}, {Total} total.",
            name,
            user.Id,
            toAdd.Length,
            toRemove.Length,
            desiredIds.Length);
    }

    private static string BuildCollectionName(string template, string username)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Recommended For You";
        }

        return template.Replace("{username}", username, StringComparison.OrdinalIgnoreCase);
    }
}
