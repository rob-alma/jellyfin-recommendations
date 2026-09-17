using System;
using System.Collections.Generic;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Fetches a user's <see cref="UserItemData"/> for every item in a library snapshot exactly
/// once, so <see cref="CandidateProvider"/> and <see cref="WatchHistoryReader"/> don't each
/// independently call <see cref="IUserDataManager.GetUserData"/> on the same item.
/// </summary>
public sealed class UserDataLookup
{
    private readonly IUserDataManager _userDataManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDataLookup"/> class.
    /// </summary>
    /// <param name="userDataManager">Jellyfin's user data manager.</param>
    public UserDataLookup(IUserDataManager userDataManager)
    {
        _userDataManager = userDataManager;
    }

    /// <summary>
    /// Builds a per-item user data lookup covering every movie, series and episode in the
    /// snapshot.
    /// </summary>
    /// <param name="user">The user to fetch data for.</param>
    /// <param name="snapshot">A snapshot of the library.</param>
    public IReadOnlyDictionary<Guid, UserItemData> Build(User user, LibrarySnapshot snapshot)
    {
        var result = new Dictionary<Guid, UserItemData>(snapshot.Movies.Count + snapshot.Series.Count + snapshot.Episodes.Count);

        AddAll(result, user, snapshot.Movies);
        AddAll(result, user, snapshot.Series);
        AddAll(result, user, snapshot.Episodes);

        return result;
    }

    private void AddAll(Dictionary<Guid, UserItemData> result, User user, IReadOnlyList<BaseItem> items)
    {
        foreach (var item in items)
        {
            result[item.Id] = _userDataManager.GetUserData(user, item) ?? new UserItemData { Key = string.Empty };
        }
    }
}
