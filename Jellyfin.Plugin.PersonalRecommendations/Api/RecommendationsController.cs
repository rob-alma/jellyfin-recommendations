using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private readonly RecommendationPlaylistService _playlistService;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<RecommendationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationsController"/> class.
    /// </summary>
    /// <param name="userManager">Jellyfin's user manager.</param>
    /// <param name="engine">The recommendation engine.</param>
    /// <param name="playlistService">Updates the managed playlist.</param>
    /// <param name="taskManager">Jellyfin's scheduled task manager.</param>
    /// <param name="logger">Logger.</param>
    public RecommendationsController(
        IUserManager userManager,
        RecommendationEngine engine,
        RecommendationPlaylistService playlistService,
        ITaskManager taskManager,
        ILogger<RecommendationsController> logger)
    {
        _userManager = userManager;
        _engine = engine;
        _playlistService = playlistService;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current recommendations for a user, computed on demand (does not touch the
    /// managed playlist).
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
    /// Queues a refresh of recommendations, and the managed playlist, for every user. Runs on
    /// Jellyfin's background task queue (visible under Scheduled Tasks) instead of blocking the
    /// request, since scoring the whole library for every user can take a while on large setups.
    /// </summary>
    [HttpPost("Refresh")]
    public ActionResult RefreshAll()
    {
        _taskManager.QueueScheduledTask<RecommendationRefreshTask>();
        return Accepted();
    }

    /// <summary>
    /// Refreshes recommendations, and the managed playlist, for one user. Runs in the
    /// background and returns immediately.
    /// </summary>
    /// <param name="userId">The user id.</param>
    [HttpPost("Refresh/{userId}")]
    public ActionResult RefreshUser([FromRoute] Guid userId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound();
        }

        _ = RefreshUserInBackgroundAsync(user);
        return Accepted();
    }

    private async Task RefreshUserInBackgroundAsync(User user)
    {
        try
        {
            var config = Plugin.Instance!.Configuration;
            var snapshot = _engine.GetSnapshot();
            var recommendations = _engine.GenerateForUser(user, snapshot, config);
            await _playlistService.UpdatePlaylistAsync(user, recommendations, snapshot, config, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh recommendations for user {UserId} via API.", user.Id);
        }
    }
}
