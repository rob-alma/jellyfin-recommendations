using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.PersonalRecommendations.Api.Dto;
using Jellyfin.Plugin.PersonalRecommendations.ScheduledTasks;
using Jellyfin.Plugin.PersonalRecommendations.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
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
    private readonly RecommendationCache _cache;
    private readonly ITaskManager _taskManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationsController"/> class.
    /// </summary>
    /// <param name="userManager">Jellyfin's user manager.</param>
    /// <param name="engine">The recommendation engine.</param>
    /// <param name="cache">The recommendation cache.</param>
    /// <param name="taskManager">Jellyfin's scheduled task manager.</param>
    public RecommendationsController(IUserManager userManager, RecommendationEngine engine, RecommendationCache cache, ITaskManager taskManager)
    {
        _userManager = userManager;
        _engine = engine;
        _cache = cache;
        _taskManager = taskManager;
    }

    /// <summary>
    /// Gets the current recommendations for a user. Served from the cache when available
    /// (populated by the scheduled task / playback-triggered refresh); computed on demand and
    /// cached on a miss, e.g. right after install before the first scheduled run.
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

        var cached = _cache.Get(userId);
        if (cached is null)
        {
            var config = Plugin.Instance!.Configuration;
            var snapshot = _engine.GetSnapshot();
            cached = _engine.GenerateForUser(user, snapshot, config);
            _cache.Set(userId, cached);
        }

        return Ok(cached.Select(r => new RecommendedItemDto { Id = r.ItemId, Name = r.Name, ItemType = r.ItemType }).ToArray());
    }

    /// <summary>
    /// Queues a refresh of the recommendation cache for every user. Runs on Jellyfin's
    /// background task queue (visible under Scheduled Tasks) instead of blocking the request.
    /// </summary>
    [HttpPost("Refresh")]
    public ActionResult RefreshAll()
    {
        _taskManager.QueueScheduledTask<RecommendationRefreshTask>();
        return Accepted();
    }
}
