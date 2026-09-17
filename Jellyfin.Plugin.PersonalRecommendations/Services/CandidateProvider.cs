using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Configuration;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Builds the set of movies/series a user could be recommended: everything in scope that
/// they haven't (sufficiently) already watched.
/// </summary>
public sealed class CandidateProvider
{
    private readonly ItemFeatureExtractor _featureExtractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProvider"/> class.
    /// </summary>
    /// <param name="featureExtractor">Extracts genres/people/studios from items.</param>
    public CandidateProvider(ItemFeatureExtractor featureExtractor)
    {
        _featureExtractor = featureExtractor;
    }

    /// <summary>
    /// Gets the candidate movies/series for a user.
    /// </summary>
    /// <param name="user">The user to build candidates for.</param>
    /// <param name="snapshot">A snapshot of the library.</param>
    /// <param name="userData">Each item's user data, from <see cref="UserDataLookup"/>.</param>
    /// <param name="config">Plugin configuration (item types, rating filter).</param>
    public IReadOnlyList<LibraryCandidate> GetCandidates(
        User user,
        LibrarySnapshot snapshot,
        IReadOnlyDictionary<Guid, UserItemData> userData,
        PluginConfiguration config)
    {
        var candidates = new List<LibraryCandidate>();

        if (config.IncludeMovies)
        {
            candidates.AddRange(snapshot.Movies.Where(item => !IsAlreadyWatched(user, item, userData)).Select(_featureExtractor.ToCandidate));
        }

        if (config.IncludeSeries)
        {
            candidates.AddRange(snapshot.Series.Where(item => !IsAlreadyWatched(user, item, userData)).Select(_featureExtractor.ToCandidate));
        }

        if (config.MinimumCommunityRating > 0)
        {
            candidates = candidates.Where(c => (c.CommunityRating ?? 0) >= config.MinimumCommunityRating).ToList();
        }

        return candidates;
    }

    private static bool IsAlreadyWatched(User user, BaseItem item, IReadOnlyDictionary<Guid, UserItemData> userData)
    {
        // BaseItem.IsPlayed is polymorphic: for a Series it recursively checks whether every
        // episode has been watched, so this correctly excludes fully-watched shows too.
        var data = userData.TryGetValue(item.Id, out var value) ? value : new UserItemData { Key = string.Empty };
        return item.IsPlayed(user, data);
    }
}
