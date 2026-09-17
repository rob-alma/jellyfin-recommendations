using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Jellyfin.Plugin.PersonalRecommendations.Domain;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Holds the most recently computed recommendations per user, so the home screen widget (and
/// the API) can serve them quickly instead of recomputing on every request. Populated by the
/// scheduled task and the playback-triggered refresh; read by the API. In-memory only — it
/// repopulates on first request or the next scheduled run after a restart.
/// </summary>
public sealed class RecommendationCache
{
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<LibraryCandidate>> _cache = new();

    /// <summary>
    /// Stores the current recommendations for a user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="recommendations">The recommended items, in order.</param>
    public void Set(Guid userId, IReadOnlyList<LibraryCandidate> recommendations)
    {
        _cache[userId] = recommendations;
    }

    /// <summary>
    /// Gets the current recommendations for a user, if any have been computed yet.
    /// </summary>
    /// <param name="userId">The user id.</param>
    public IReadOnlyList<LibraryCandidate>? Get(Guid userId)
    {
        return _cache.TryGetValue(userId, out var value) ? value : null;
    }
}
