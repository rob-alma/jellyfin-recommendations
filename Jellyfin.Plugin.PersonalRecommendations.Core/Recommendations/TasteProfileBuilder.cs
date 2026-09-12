using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.PersonalRecommendations.Domain;

namespace Jellyfin.Plugin.PersonalRecommendations.Recommendations;

/// <summary>
/// Turns a user's raw watch signals into a weighted taste profile. Pure function: no I/O,
/// safe to unit test with synthetic signals.
/// </summary>
public static class TasteProfileBuilder
{
    private const double RecencyHalfLifeDays = 180.0;
    private const double MinDecayFactor = 0.15;
    private const double StudioWeightFactor = 0.5;
    private const double DirectorWeightFactor = 1.2;
    private const double ActorWeightFactor = 0.8;
    private const double DecadeWeightFactor = 0.5;
    private const int MaxActorsPerSignal = 5;

    /// <summary>
    /// Builds a taste profile from a user's watch signals.
    /// </summary>
    /// <param name="signals">The user's watch signals.</param>
    /// <param name="now">The current time, used to decay older signals.</param>
    public static TasteProfile Build(IReadOnlyList<WatchSignal> signals, DateTimeOffset now)
    {
        var genreWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var studioWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var personWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var decadeWeights = new Dictionary<int, double>();

        foreach (var signal in signals)
        {
            var weight = CalculateWeight(signal, now);
            if (weight == 0)
            {
                continue;
            }

            Accumulate(genreWeights, signal.Genres, weight);
            Accumulate(studioWeights, signal.Studios, weight * StudioWeightFactor);
            Accumulate(personWeights, signal.Directors, weight * DirectorWeightFactor);
            Accumulate(personWeights, signal.Actors.Take(MaxActorsPerSignal), weight * ActorWeightFactor);

            if (signal.ProductionYear.HasValue)
            {
                var decade = signal.ProductionYear.Value / 10 * 10;
                decadeWeights[decade] = decadeWeights.GetValueOrDefault(decade) + (weight * DecadeWeightFactor);
            }
        }

        return new TasteProfile
        {
            GenreWeights = genreWeights,
            StudioWeights = studioWeights,
            PersonWeights = personWeights,
            DecadeWeights = decadeWeights
        };
    }

    /// <summary>
    /// Calculates how strongly one watch signal should count, combining favorite/like/rating
    /// boosts, repeat-play boosts, and recency decay (older activity still counts, just less).
    /// </summary>
    /// <param name="signal">The signal to weigh.</param>
    /// <param name="now">The current time.</param>
    internal static double CalculateWeight(WatchSignal signal, DateTimeOffset now)
    {
        var weight = 1.0;

        if (signal.IsFavorite)
        {
            weight += 1.0;
        }

        if (signal.Likes == true)
        {
            weight += 1.0;
        }
        else if (signal.Likes == false)
        {
            // An explicit dislike should suppress similar content, not just fail to boost it.
            weight -= 2.0;
        }

        if (signal.Rating.HasValue)
        {
            weight += (signal.Rating.Value - 5.0) * 0.3;
        }

        if (signal.PlayCount > 1)
        {
            weight += Math.Min(1.0, (signal.PlayCount - 1) * 0.25);
        }

        var decay = 1.0;
        if (signal.LastPlayedDate.HasValue)
        {
            var ageDays = Math.Max(0, (now - signal.LastPlayedDate.Value).TotalDays);
            decay = Math.Max(MinDecayFactor, Math.Pow(0.5, ageDays / RecencyHalfLifeDays));
        }

        return weight * decay;
    }

    private static void Accumulate(Dictionary<string, double> weights, IEnumerable<string> keys, double weight)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            weights[key] = weights.GetValueOrDefault(key) + weight;
        }
    }
}
