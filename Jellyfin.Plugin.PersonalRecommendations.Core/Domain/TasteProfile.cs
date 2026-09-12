using System.Collections.Generic;

namespace Jellyfin.Plugin.PersonalRecommendations.Domain;

/// <summary>
/// A user's weighted taste profile, built from their watch history.
/// </summary>
public sealed class TasteProfile
{
    /// <summary>
    /// Gets the weight per genre. Positive means the user likes it, negative means they've shown they don't.
    /// </summary>
    public IReadOnlyDictionary<string, double> GenreWeights { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Gets the weight per studio.
    /// </summary>
    public IReadOnlyDictionary<string, double> StudioWeights { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Gets the weight per person (director or actor).
    /// </summary>
    public IReadOnlyDictionary<string, double> PersonWeights { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Gets the weight per decade (e.g. 1990, 2000, 2010).
    /// </summary>
    public IReadOnlyDictionary<int, double> DecadeWeights { get; init; } = new Dictionary<int, double>();

    /// <summary>
    /// Gets a value indicating whether the profile carries any signal at all. When false, the
    /// user has no usable watch history yet and a cold-start fallback should be used instead.
    /// </summary>
    public bool HasSignal =>
        GenreWeights.Count > 0 || StudioWeights.Count > 0 || PersonWeights.Count > 0 || DecadeWeights.Count > 0;
}
