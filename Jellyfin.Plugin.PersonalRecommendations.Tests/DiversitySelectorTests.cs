using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using Jellyfin.Plugin.PersonalRecommendations.Recommendations;
using Xunit;

namespace Jellyfin.Plugin.PersonalRecommendations.Tests;

public class DiversitySelectorTests
{
    private static ScoredCandidate Scored(string groupKey, double score, string? name = null)
    {
        var candidate = new LibraryCandidate(
            Guid.NewGuid(),
            name ?? Guid.NewGuid().ToString(),
            "Movie",
            [],
            [groupKey],
            [],
            [],
            2020,
            null,
            groupKey);

        return new ScoredCandidate(candidate, score, []);
    }

    [Fact]
    public void Select_ZeroLimit_ReturnsEmpty()
    {
        var result = DiversitySelector.Select([Scored("A", 1.0)], limit: 0, capPerGroup: 0);

        Assert.Empty(result);
    }

    [Fact]
    public void Select_RespectsLimit()
    {
        var items = Enumerable.Range(0, 10).Select(i => Scored("A", i)).ToList();

        var result = DiversitySelector.Select(items, limit: 3, capPerGroup: 0);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Select_OrdersByScoreDescending()
    {
        var items = new[] { Scored("A", 1.0), Scored("A", 5.0), Scored("A", 3.0) };

        var result = DiversitySelector.Select(items, limit: 3, capPerGroup: 0);

        Assert.Equal([5.0, 3.0, 1.0], result.Select(r => r.Score));
    }

    [Fact]
    public void Select_CapsPerGroup_EvenIfHigherScoring()
    {
        // Enough diversity exists (2 groups, 2 items each) to fill the limit without ever
        // needing to relax the cap, so it should hold exactly.
        var items = new[]
        {
            Scored("StudioA", 10.0),
            Scored("StudioA", 9.0),
            Scored("StudioA", 8.0),
            Scored("StudioB", 1.0),
            Scored("StudioB", 0.5)
        };

        var result = DiversitySelector.Select(items, limit: 4, capPerGroup: 2);

        Assert.Equal(2, result.Count(r => r.Candidate.GroupKey == "StudioA"));
        Assert.Equal(2, result.Count(r => r.Candidate.GroupKey == "StudioB"));
    }

    [Fact]
    public void Select_BackfillsFromOverCappedGroupWhenNotEnoughDiversity()
    {
        var items = new[]
        {
            Scored("StudioA", 10.0),
            Scored("StudioA", 9.0),
            Scored("StudioA", 8.0)
        };

        var result = DiversitySelector.Select(items, limit: 3, capPerGroup: 1);

        // Only one distinct group exists, so the cap can't be honored for all 3 slots;
        // the remaining slots should still be filled from the same group instead of left empty.
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Select_NoCap_WhenCapPerGroupIsZero()
    {
        var items = Enumerable.Range(0, 5).Select(i => Scored("A", i)).ToList();

        var result = DiversitySelector.Select(items, limit: 5, capPerGroup: 0);

        Assert.Equal(5, result.Count);
    }
}
