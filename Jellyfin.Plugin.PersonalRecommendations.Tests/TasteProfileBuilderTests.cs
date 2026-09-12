using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PersonalRecommendations.Domain;
using Jellyfin.Plugin.PersonalRecommendations.Recommendations;
using Xunit;

namespace Jellyfin.Plugin.PersonalRecommendations.Tests;

public class TasteProfileBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

    private static WatchSignal PlainSignal(string genre = "Action", DateTimeOffset? lastPlayed = null) =>
        new(
            Guid.NewGuid(),
            [genre],
            ["Test Studio"],
            ["Test Director"],
            ["Test Actor"],
            2020,
            1,
            IsFavorite: false,
            Likes: null,
            Rating: null,
            lastPlayed ?? Now);

    [Fact]
    public void Build_NoSignals_HasNoSignal()
    {
        var profile = TasteProfileBuilder.Build([], Now);

        Assert.False(profile.HasSignal);
    }

    [Fact]
    public void Build_SingleWatch_RecordsPositiveGenreWeight()
    {
        var profile = TasteProfileBuilder.Build([PlainSignal("Action")], Now);

        Assert.True(profile.HasSignal);
        Assert.True(profile.GenreWeights["Action"] > 0);
    }

    [Fact]
    public void Build_FavoriteItem_WeighsMoreThanPlainWatch()
    {
        var plain = TasteProfileBuilder.Build([PlainSignal("Action")], Now);
        var favorite = TasteProfileBuilder.Build(
            [PlainSignal("Action") with { IsFavorite = true }],
            Now);

        Assert.True(favorite.GenreWeights["Action"] > plain.GenreWeights["Action"]);
    }

    [Fact]
    public void Build_DislikedItem_ProducesNegativeGenreWeight()
    {
        var profile = TasteProfileBuilder.Build(
            [PlainSignal("Horror") with { Likes = false }],
            Now);

        Assert.True(profile.GenreWeights["Horror"] < 0);
    }

    [Fact]
    public void Build_RepeatedGenreAcrossSignals_Accumulates()
    {
        var profile = TasteProfileBuilder.Build(
            [PlainSignal("Comedy"), PlainSignal("Comedy")],
            Now);

        var single = TasteProfileBuilder.Build([PlainSignal("Comedy")], Now);

        Assert.True(profile.GenreWeights["Comedy"] > single.GenreWeights["Comedy"]);
    }

    [Fact]
    public void Build_OldSignal_DecaysTowardsFloorButNeverReachesZero()
    {
        var recent = TasteProfileBuilder.Build([PlainSignal("Drama", Now)], Now);
        var old = TasteProfileBuilder.Build([PlainSignal("Drama", Now.AddYears(-5))], Now);

        Assert.True(old.GenreWeights["Drama"] > 0);
        Assert.True(old.GenreWeights["Drama"] < recent.GenreWeights["Drama"]);
    }

    [Fact]
    public void Build_DirectorsAndActors_BothContributeToPersonWeights()
    {
        var profile = TasteProfileBuilder.Build([PlainSignal("Action")], Now);

        Assert.True(profile.PersonWeights.ContainsKey("Test Director"));
        Assert.True(profile.PersonWeights.ContainsKey("Test Actor"));
        Assert.True(profile.PersonWeights["Test Director"] > profile.PersonWeights["Test Actor"]);
    }

    [Fact]
    public void Build_ProductionYear_AccumulatesIntoDecadeBucket()
    {
        var profile = TasteProfileBuilder.Build([PlainSignal("Action") with { ProductionYear = 1994 }], Now);

        Assert.True(profile.DecadeWeights.ContainsKey(1990));
    }

    [Fact]
    public void Build_MoreThanFiveActors_OnlyFirstFiveCounted()
    {
        var actors = new List<string> { "A1", "A2", "A3", "A4", "A5", "A6" };
        var signal = PlainSignal("Action") with { Actors = actors };

        var profile = TasteProfileBuilder.Build([signal], Now);

        Assert.True(profile.PersonWeights.ContainsKey("A5"));
        Assert.False(profile.PersonWeights.ContainsKey("A6"));
    }
}
