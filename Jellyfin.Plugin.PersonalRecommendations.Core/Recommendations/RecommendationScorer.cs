using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.PersonalRecommendations.Domain;

namespace Jellyfin.Plugin.PersonalRecommendations.Recommendations;

/// <summary>
/// Scores library candidates against a taste profile. Pure function: no I/O, safe to unit test.
/// </summary>
public static class RecommendationScorer
{
    private const double DirectorWeightFactor = 1.2;
    private const double DecadeWeightFactor = 0.3;
    private const double CommunityRatingTieBreakFactor = 0.02;
    private const int MaxActorsPerCandidate = 5;

    /// <summary>
    /// Scores every candidate against the profile.
    /// </summary>
    /// <param name="candidates">Candidates to score.</param>
    /// <param name="profile">The user's taste profile.</param>
    public static IReadOnlyList<ScoredCandidate> ScoreAll(IReadOnlyList<LibraryCandidate> candidates, TasteProfile profile)
    {
        var results = new List<ScoredCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            results.Add(Score(candidate, profile));
        }

        return results;
    }

    /// <summary>
    /// Scores one candidate against the profile: a normalized weighted feature overlap, with a
    /// small community-rating tie-break so otherwise-equal candidates still order sensibly.
    /// </summary>
    /// <param name="candidate">The candidate to score.</param>
    /// <param name="profile">The user's taste profile.</param>
    public static ScoredCandidate Score(LibraryCandidate candidate, TasteProfile profile)
    {
        double score = 0;
        var featureCount = 0;
        var matchedGenres = new List<string>();

        foreach (var genre in candidate.Genres)
        {
            featureCount++;
            if (profile.GenreWeights.TryGetValue(genre, out var weight))
            {
                score += weight;
                if (weight > 0)
                {
                    matchedGenres.Add(genre);
                }
            }
        }

        foreach (var studio in candidate.Studios)
        {
            featureCount++;
            if (profile.StudioWeights.TryGetValue(studio, out var weight))
            {
                score += weight;
            }
        }

        foreach (var director in candidate.Directors)
        {
            featureCount++;
            if (profile.PersonWeights.TryGetValue(director, out var weight))
            {
                score += weight * DirectorWeightFactor;
            }
        }

        foreach (var actor in candidate.Actors.Take(MaxActorsPerCandidate))
        {
            featureCount++;
            if (profile.PersonWeights.TryGetValue(actor, out var weight))
            {
                score += weight;
            }
        }

        if (candidate.ProductionYear.HasValue)
        {
            featureCount++;
            var decade = candidate.ProductionYear.Value / 10 * 10;
            if (profile.DecadeWeights.TryGetValue(decade, out var weight))
            {
                score += weight * DecadeWeightFactor;
            }
        }

        // Normalize by feature count so heavily-tagged items don't win purely by having more
        // tags; sqrt keeps the effect gentle instead of punishing well-tagged items outright.
        var normalized = featureCount > 0 ? score / Math.Sqrt(featureCount) : score;
        normalized += (candidate.CommunityRating ?? 0) * CommunityRatingTieBreakFactor;

        return new ScoredCandidate(candidate, normalized, matchedGenres);
    }
}
