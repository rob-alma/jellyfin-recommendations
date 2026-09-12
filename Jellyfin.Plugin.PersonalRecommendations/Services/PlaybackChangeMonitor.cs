using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Watches for playback-driven user data changes and refreshes that user's recommendations a
/// few minutes after their last change, so a binge session triggers one refresh instead of one
/// per episode.
/// </summary>
public sealed class PlaybackChangeMonitor : IHostedService, IDisposable
{
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly RecommendationEngine _engine;
    private readonly RecommendationCollectionService _collectionService;
    private readonly ILogger<PlaybackChangeMonitor> _logger;
    private readonly ConcurrentDictionary<Guid, Timer> _pendingTimers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackChangeMonitor"/> class.
    /// </summary>
    /// <param name="userDataManager">Jellyfin's user data manager.</param>
    /// <param name="userManager">Jellyfin's user manager.</param>
    /// <param name="engine">The recommendation engine.</param>
    /// <param name="collectionService">Updates the managed collection.</param>
    /// <param name="logger">Logger.</param>
    public PlaybackChangeMonitor(
        IUserDataManager userDataManager,
        IUserManager userManager,
        RecommendationEngine engine,
        RecommendationCollectionService collectionService,
        ILogger<PlaybackChangeMonitor> logger)
    {
        _userDataManager = userDataManager;
        _userManager = userManager;
        _engine = engine;
        _collectionService = collectionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        foreach (var timer in _pendingTimers.Values)
        {
            timer.Dispose();
        }

        _pendingTimers.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var timer in _pendingTimers.Values)
        {
            timer.Dispose();
        }
    }

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled || !config.AutoRefreshAfterPlayback)
        {
            return;
        }

        if (e.UserId == Guid.Empty || e.UserData is null || (!e.UserData.Played && e.UserData.PlayCount <= 0))
        {
            return;
        }

        var delay = TimeSpan.FromMinutes(Math.Max(1, config.AutoRefreshDebounceMinutes));
        _pendingTimers.AddOrUpdate(
            e.UserId,
            _ => new Timer(RunRefresh, e.UserId, delay, Timeout.InfiniteTimeSpan),
            (_, existingTimer) =>
            {
                existingTimer.Change(delay, Timeout.InfiniteTimeSpan);
                return existingTimer;
            });
    }

    private void RunRefresh(object? state)
    {
        var userId = (Guid)state!;
        if (_pendingTimers.TryRemove(userId, out var timer))
        {
            timer.Dispose();
        }

        _ = RunRefreshAsync(userId);
    }

    private async Task RunRefreshAsync(Guid userId)
    {
        try
        {
            var user = _userManager.GetUserById(userId);
            if (user is null)
            {
                return;
            }

            var config = Plugin.Instance!.Configuration;
            var snapshot = _engine.GetSnapshot();
            var recommendations = _engine.GenerateForUser(user, snapshot, config);
            await _collectionService.UpdateCollectionAsync(user, recommendations, config, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh recommendations for user {UserId} after playback.", userId);
        }
    }
}
