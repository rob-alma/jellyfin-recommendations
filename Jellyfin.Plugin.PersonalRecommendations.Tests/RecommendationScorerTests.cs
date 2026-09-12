using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using Jellyfin.Plugin.PersonalRecommendations.Recommendations;
using Xunit;

namespace Jellyfin.Plugin.PersonalRecommendations.Tests;

public class RecommendationScorerTests
{
    private static LibraryCandidate Candidate(
        string[]? genres = null,
        string[]? studios = null,
        string[]? directors = null,
        string[]? actors = null,
        int? productionYear = 2020,
        float? communityRating = null) =>
        new(
            Guid.NewGuid(),
            "Test Item",
            "Movie",
            genres ?? ["Action"],
            studios ?? ["Test Studio"],
            directors ?? [],
            actors ?? [],
            productionYear,
            communityRating,
            (studios ?? ["Test Studio"])[0]);

    private static TasteProfile ProfileWith(
        Dictionary<string, double>? genres = null,
        Dictionary<string, double>? studios = null,
        Dictionary<string, double>? people = null,
        Dictionary<int, double>? decades = null) =>
        new()
        {
            GenreWeights = genres ?? new Dictionary<string, double>(),
            StudioWeights = studios ?? new Dictionary<string, double>(),
            PersonWeights = people ?? new Dictionary<string, double>(),
            DecadeWeights = decades ?? new Dictionary<int, double>()
        };

    [Fact]
    public void Score_MatchingGenre_ProducesPositiveScore()
    {
        var candidate = Candidate(genres: ["Action"]);
        var profile = ProfileWith(genres: new Dictionary<string, double> { ["Action"] = 3.0 });

        var result = RecommendationScorer.Score(candidate, profile);

        Assert.True(result.Score > 0);
        Assert.Contains("Action", result.MatchedGenres);
    }

    [Fact]
    public void Score_NoOverlap_DoesNotMatchGenres()
    {
        var candidate = Candidate(genres: ["Documentary"]);
        var profile = ProfileWith(genres: new Dictionary<string, double> { ["Action"] = 3.0 });

        var result = RecommendationScorer.Score(candidate, profile);

        Assert.Empty(result.MatchedGenres);
    }

    [Fact]
    public void Score_DislikedGenre_PullsScoreDown()
    {
        var candidate = Candidate(genres: ["Horror"]);
        var likedProfile = ProfileWith(genres: new Dictionary<string, double> { ["Horror"] = 2.0 });
        var dislikedProfile = ProfileWith(genres: new Dictionary<string, double> { ["Horror"] = -2.0 });

        var likedScore = RecommendationScorer.Score(candidate, likedProfile).Score;
        var dislikedScore = RecommendationScorer.Score(candidate, dislikedProfile).Score;

        Assert.True(dislikedScore < likedScore);
    }

    [Fact]
    public void Score_DirectorMatch_WeighsMoreThanEquivalentActorMatch()
    {
        var directorCandidate = Candidate(genres: [], directors: ["Denis Villeneuve"]);
        var actorCandidate = Candidate(genres: [], actors: ["Denis Villeneuve"]);
        var profile = ProfileWith(people: new Dictionary<string, double> { ["Denis Villeneuve"] = 2.0 });

        var directorScore = RecommendationScorer.Score(directorCandidate, profile).Score;
        var actorScore = RecommendationScorer.Score(actorCandidate, profile).Score;

        Assert.True(directorScore > actorScore);
    }

    [Fact]
    public void Score_HigherCommunityRating_BreaksTiesUpward()
    {
        var lowRated = Candidate(genres: [], communityRating: 5.0f);
        var highRated = Candidate(genres: [], communityRating: 9.0f);
        var profile = ProfileWith();

        var lowScore = RecommendationScorer.Score(lowRated, profile).Score;
        var highScore = RecommendationScorer.Score(highRated, profile).Score;

        Assert.True(highScore > lowScore);
    }

    [Fact]
    public void ScoreAll_ScoresEveryCandidate()
    {
        var candidates = new[] { Candidate(), Candidate(), Candidate() };
        var profile = ProfileWith();

        var results = RecommendationScorer.ScoreAll(candidates, profile);

        Assert.Equal(3, results.Count);
    }
}
