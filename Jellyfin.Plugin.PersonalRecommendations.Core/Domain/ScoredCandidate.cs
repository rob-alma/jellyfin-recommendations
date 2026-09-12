using System.Collections.Generic;

namespace Jellyfin.Plugin.PersonalRecommendations.Domain;

/// <summary>
/// A candidate together with its computed recommendation score.
/// </summary>
/// <param name="Candidate">The scored candidate.</param>
/// <param name="Score">Higher is a stronger recommendation.</param>
/// <param name="MatchedGenres">Genres that contributed positively, for display/debugging.</param>
public sealed record ScoredCandidate(LibraryCandidate Candidate, double Score, IReadOnlyList<string> MatchedGenres);
