using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PersonalRecommendations.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.ScheduledTasks;

/// <summary>
/// Scheduled task that rebuilds every user's taste profile from watch history and updates
/// their "Recommended For You" playlist.
/// </summary>
public sealed class RecommendationRefreshTask : IScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly RecommendationEngine _engine;
    private readonly RecommendationPlaylistService _playlistService;
    private readonly ILogger<RecommendationRefreshTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationRefreshTask"/> class.
    /// </summary>
    /// <param name="userManager">Jellyfin's user manager.</param>
    /// <param name="engine">The recommendation engine.</param>
    /// <param name="playlistService">Updates the managed playlist.</param>
    /// <param name="logger">Logger.</param>
    public RecommendationRefreshTask(
        IUserManager userManager,
        RecommendationEngine engine,
        RecommendationPlaylistService playlistService,
        ILogger<RecommendationRefreshTask> logger)
    {
        _userManager = userManager;
        _engine = engine;
        _playlistService = playlistService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Refresh personal recommendations";

    /// <inheritdoc />
    public string Key => "Jellyfin.Plugin.PersonalRecommendations.Refresh";

    /// <inheritdoc />
    public string Description => "Rebuilds each user's taste profile from watch history and updates their \"Recommended For You\" playlist.";

    /// <inheritdoc />
    public string Category => "Personal Recommendations";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
        if (!config.Enabled)
        {
            _logger.LogInformation("Personal Recommendations is disabled; skipping refresh.");
            return;
        }

        var users = _userManager.GetUsers().ToArray();
        var snapshot = _engine.GetSnapshot();

        for (var i = 0; i < users.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = users[i];

            try
            {
                var recommendations = _engine.GenerateForUser(user, snapshot, config);
                await _playlistService.UpdatePlaylistAsync(user, recommendations, snapshot, config, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh recommendations for user {UserId}.", user.Id);
            }

            progress.Report((i + 1) / (double)Math.Max(1, users.Length) * 100);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(Math.Max(1, config.ScheduledRefreshIntervalHours)).Ticks
            }
        ];
    }
}
