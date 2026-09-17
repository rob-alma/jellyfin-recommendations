using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PersonalRecommendations.Api.Dto;
using Jellyfin.Plugin.PersonalRecommendations.ScheduledTasks;
using Jellyfin.Plugin.PersonalRecommendations.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    private readonly ComputeGate _gate;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<RecommendationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationsController"/> class.
    /// </summary>
    /// <param name="userManager">Jellyfin's user manager.</param>
    /// <param name="engine">The recommendation engine.</param>
    /// <param name="cache">The recommendation cache.</param>
    /// <param name="gate">Ensures only one refresh runs at a time across the whole plugin.</param>
    /// <param name="taskManager">Jellyfin's scheduled task manager.</param>
    /// <param name="logger">Logger.</param>
    public RecommendationsController(
        IUserManager userManager,
        RecommendationEngine engine,
        RecommendationCache cache,
        ComputeGate gate,
        ITaskManager taskManager,
        ILogger<RecommendationsController> logger)
    {
        _userManager = userManager;
        _engine = engine;
        _cache = cache;
        _gate = gate;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current recommendations for a user, served from the cache. On a cache miss
    /// (e.g. right after install/restart, before the first scheduled run finishes) this
    /// returns an empty list immediately and warms the cache in the background, rather than
    /// computing inline - scoring the whole library synchronously inside the request was slow
    /// enough on a real library to trip a reverse proxy's timeout.
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
            _ = Task.Run(() => WarmCache(user));
            return Ok(Array.Empty<RecommendedItemDto>());
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

    private void WarmCache(User user)
    {
        try
        {
            // Opportunistic: if the plugin's one compute slot (see ComputeGate) is already in
            // use, skip - the widget's own client-side retry will ask again shortly, and by
            // then either this or another refresh will likely have populated the cache.
            _gate.TryRun(() =>
            {
                var config = Plugin.Instance!.Configuration;
                var snapshot = _engine.GetSnapshot();
                var recommendations = _engine.GenerateForUser(user, snapshot, config);
                _cache.Set(user.Id, recommendations);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm the recommendation cache for user {UserId}.", user.Id);
        }
    }
}
