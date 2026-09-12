using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.PersonalRecommendations.Domain;

namespace Jellyfin.Plugin.PersonalRecommendations.Recommendations;

/// <summary>
/// Picks the top-N scored candidates, capping how many can share the same group key (e.g. the
/// same studio) so results aren't dominated by one franchise. Pure function: no I/O.
/// </summary>
public static class DiversitySelector
{
    /// <summary>
    /// Selects up to <paramref name="limit"/> candidates, applying <paramref name="capPerGroup"/>
    /// (0 or less disables the cap) before backfilling from the remaining highest scores.
    /// </summary>
    /// <param name="scored">Scored candidates.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="capPerGroup">Maximum number of results allowed per <see cref="LibraryCandidate.GroupKey"/>.</param>
    public static IReadOnlyList<ScoredCandidate> Select(IReadOnlyList<ScoredCandidate> scored, int limit, int capPerGroup)
    {
        if (limit <= 0)
        {
            return [];
        }

        var ordered = scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Candidate.CommunityRating ?? 0)
            .ThenBy(s => s.Candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = new List<ScoredCandidate>(Math.Min(limit, ordered.Count));
        var selectedIds = new HashSet<Guid>();
        var groupCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in ordered)
        {
            if (selected.Count >= limit)
            {
                break;
            }

            var group = candidate.Candidate.GroupKey;
            var count = groupCounts.GetValueOrDefault(group);
            if (capPerGroup > 0 && count >= capPerGroup)
            {
                continue;
            }

            selected.Add(candidate);
            selectedIds.Add(candidate.Candidate.ItemId);
            groupCounts[group] = count + 1;
        }

        if (selected.Count < limit)
        {
            foreach (var candidate in ordered)
            {
                if (selected.Count >= limit)
                {
                    break;
                }

                if (selectedIds.Add(candidate.Candidate.ItemId))
                {
                    selected.Add(candidate);
                }
            }
        }

        return selected;
    }
}
