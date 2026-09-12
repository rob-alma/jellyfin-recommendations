using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PersonalRecommendations.Api.Dto;
using Jellyfin.Plugin.PersonalRecommendations.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PersonalRecommendations.Api;

/// <summary>
/// API endpoints for reading and manually refreshing recommendations.
/// </summary>
[ApiController]
[Authorize]
[Route("Recommendations")]
public sealed class RecommendationsController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly RecommendationEngine _engine;
    private readonly RecommendationCollectionService _collectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationsController"/> class.
    /// </summary>
    /// <param name="userManager">Jellyfin's user manager.</param>
    /// <param name="engine">The recommendation engine.</param>
    /// <param name="collectionService">Updates the managed collection.</param>
    public RecommendationsController(IUserManager userManager, RecommendationEngine engine, RecommendationCollectionService collectionService)
    {
        _userManager = userManager;
        _engine = engine;
        _collectionService = collectionService;
    }

    /// <summary>
    /// Gets the current recommendations for a user, computed on demand (does not touch the
    /// managed collection).
    /// </summary>
    /// <param name="userId">The user id.</param>
    [HttpGet("{userId}")]
    public ActionResult<IReadOnlyList<RecommendedItemDto>> GetRecommendations([FromRoute] Guid userId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound();
        }

        var config = Plugin.Instance!.Configuration;
        var snapshot = _engine.GetSnapshot();
        var recommendations = _engine.GenerateForUser(user, snapshot, config);

        return Ok(recommendations.Select(r => new RecommendedItemDto { Id = r.ItemId, Name = r.Name, ItemType = r.ItemType }).ToArray());
    }

    /// <summary>
    /// Refreshes recommendations, and the managed collection, for every user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Refresh")]
    public async Task<ActionResult> RefreshAll(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        var snapshot = _engine.GetSnapshot();

        foreach (var user in _userManager.GetUsers())
        {
            var recommendations = _engine.GenerateForUser(user, snapshot, config);
            await _collectionService.UpdateCollectionAsync(user, recommendations, config, cancellationToken).ConfigureAwait(false);
        }

        return NoContent();
    }

    /// <summary>
    /// Refreshes recommendations, and the managed collection, for one user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Refresh/{userId}")]
    public async Task<ActionResult> RefreshUser([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound();
        }

        var config = Plugin.Instance!.Configuration;
        var snapshot = _engine.GetSnapshot();
        var recommendations = _engine.GenerateForUser(user, snapshot, config);
        await _collectionService.UpdateCollectionAsync(user, recommendations, config, cancellationToken).ConfigureAwait(false);

        return NoContent();
    }
}
