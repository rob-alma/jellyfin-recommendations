using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Configuration;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Builds the set of movies/series a user could be recommended: everything in scope that
/// they haven't (sufficiently) already watched.
/// </summary>
public sealed class CandidateProvider
{
    private readonly IUserDataManager _userDataManager;
    private readonly ItemFeatureExtractor _featureExtractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProvider"/> class.
    /// </summary>
    /// <param name="userDataManager">Jellyfin's user data manager.</param>
    /// <param name="featureExtractor">Extracts genres/people/studios from items.</param>
    public CandidateProvider(IUserDataManager userDataManager, ItemFeatureExtractor featureExtractor)
    {
        _userDataManager = userDataManager;
        _featureExtractor = featureExtractor;
    }

    /// <summary>
    /// Gets the candidate movies/series for a user.
    /// </summary>
    /// <param name="user">The user to build candidates for.</param>
    /// <param name="snapshot">A snapshot of the library.</param>
    /// <param name="config">Plugin configuration (item types, rating filter).</param>
    public IReadOnlyList<LibraryCandidate> GetCandidates(User user, LibrarySnapshot snapshot, PluginConfiguration config)
    {
        var candidates = new List<LibraryCandidate>();

        if (config.IncludeMovies)
        {
            candidates.AddRange(snapshot.Movies.Where(item => !IsAlreadyWatched(user, item)).Select(_featureExtractor.ToCandidate));
        }

        if (config.IncludeSeries)
        {
            candidates.AddRange(snapshot.Series.Where(item => !IsAlreadyWatched(user, item)).Select(_featureExtractor.ToCandidate));
        }

        if (config.MinimumCommunityRating > 0)
        {
            candidates = candidates.Where(c => (c.CommunityRating ?? 0) >= config.MinimumCommunityRating).ToList();
        }

        return candidates;
    }

    private bool IsAlreadyWatched(User user, BaseItem item)
    {
        // BaseItem.IsPlayed is polymorphic: for a Series it recursively checks whether every
        // episode has been watched, so this correctly excludes fully-watched shows too.
        var userData = _userDataManager.GetUserData(user, item) ?? new UserItemData { Key = string.Empty };
        return item.IsPlayed(user, userData);
    }
}
