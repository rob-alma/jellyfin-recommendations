using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Configuration;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using Jellyfin.Plugin.PersonalRecommendations.Recommendations;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Orchestrates building a taste profile and scoring candidates for one user.
/// </summary>
public sealed class RecommendationEngine
{
    private readonly LibrarySnapshotProvider _snapshotProvider;
    private readonly UserDataLookup _userDataLookup;
    private readonly WatchHistoryReader _watchHistoryReader;
    private readonly CandidateProvider _candidateProvider;
    private readonly FavoritesSeeder _favoritesSeeder;
    private readonly ILogger<RecommendationEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationEngine"/> class.
    /// </summary>
    /// <param name="snapshotProvider">Provides library snapshots.</param>
    /// <param name="userDataLookup">Fetches each item's user data once per refresh.</param>
    /// <param name="watchHistoryReader">Reads per-user watch signals.</param>
    /// <param name="candidateProvider">Builds per-user candidate lists.</param>
    /// <param name="favoritesSeeder">Seeds favorites for accounts that have none.</param>
    /// <param name="logger">Logger.</param>
    public RecommendationEngine(
        LibrarySnapshotProvider snapshotProvider,
        UserDataLookup userDataLookup,
        WatchHistoryReader watchHistoryReader,
        CandidateProvider candidateProvider,
        FavoritesSeeder favoritesSeeder,
        ILogger<RecommendationEngine> logger)
    {
        _snapshotProvider = snapshotProvider;
        _userDataLookup = userDataLookup;
        _watchHistoryReader = watchHistoryReader;
        _candidateProvider = candidateProvider;
        _favoritesSeeder = favoritesSeeder;
        _logger = logger;
    }

    /// <summary>
    /// Takes a fresh snapshot of the library. Callers refreshing multiple users should take one
    /// snapshot and reuse it, instead of re-querying the library per user.
    /// </summary>
    public LibrarySnapshot GetSnapshot() => _snapshotProvider.GetSnapshot();

    /// <summary>
    /// Generates the ordered list of recommendations for one user.
    /// </summary>
    /// <param name="user">The user to recommend for.</param>
    /// <param name="snapshot">A library snapshot (see <see cref="GetSnapshot"/>).</param>
    /// <param name="config">Plugin configuration.</param>
    public IReadOnlyList<LibraryCandidate> GenerateForUser(User user, LibrarySnapshot snapshot, PluginConfiguration config)
    {
        // Fetched once per item here and reused by both readers below, instead of each of them
        // independently calling IUserDataManager.GetUserData on every movie/series/episode.
        var userData = _userDataLookup.Build(user, snapshot);

        var signals = _watchHistoryReader.BuildSignals(snapshot, userData);
        var candidates = _candidateProvider.GetCandidates(user, snapshot, userData, config);
        var profile = TasteProfileBuilder.Build(signals, DateTimeOffset.UtcNow);

        IReadOnlyList<Domain.ScoredCandidate> scored = profile.HasSignal
            ? RecommendationScorer.ScoreAll(candidates, profile)
            : candidates.Select(c => new Domain.ScoredCandidate(c, c.CommunityRating ?? 0, [])).ToList();

        var selected = DiversitySelector.Select(scored, config.RecommendationLimit, config.DiversityCapPerGroup);

        _logger.LogInformation(
            "Generated {Count} recommendations for user {UserId} from {CandidateCount} candidates ({SignalCount} taste signals, cold start: {ColdStart}).",
            selected.Count,
            user.Id,
            candidates.Count,
            signals.Count,
            !profile.HasSignal);

        var result = selected.Select(s => s.Candidate).ToArray();
        _favoritesSeeder.MaybeSeed(user, userData, result, config);
        return result;
    }
}
