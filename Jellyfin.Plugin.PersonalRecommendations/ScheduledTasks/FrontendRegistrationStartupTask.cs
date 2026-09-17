using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PersonalRecommendations.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PersonalRecommendations.ScheduledTasks;

/// <summary>
/// Registers the home screen widget's frontend script on every server start, so the injection
/// survives Jellyfin updates that overwrite index.html (when using direct injection).
/// </summary>
public sealed class FrontendRegistrationStartupTask : IScheduledTask
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly ILogger<FrontendRegistrationStartupTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrontendRegistrationStartupTask"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin's application paths.</param>
    /// <param name="configurationManager">Jellyfin's server configuration manager.</param>
    /// <param name="logger">Logger.</param>
    public FrontendRegistrationStartupTask(
        IApplicationPaths applicationPaths,
        IServerConfigurationManager configurationManager,
        ILogger<FrontendRegistrationStartupTask> logger)
    {
        _applicationPaths = applicationPaths;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Register Personal Recommendations widget";

    /// <inheritdoc />
    public string Key => "Jellyfin.Plugin.PersonalRecommendations.RegisterWidget";

    /// <inheritdoc />
    public string Description => "Registers the home screen widget's frontend script.";

    /// <inheritdoc />
    public string Category => "Personal Recommendations";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.HomeScreenWidgetEnabled)
        {
            _logger.LogInformation("Personal Recommendations home screen widget is disabled; skipping frontend registration.");
            return Task.CompletedTask;
        }

        var basePath = GetBasePath();
        WidgetRuntime.BasePath = basePath;

        if (!FrontendRegistration.TryRegisterConfigured(config, _applicationPaths, basePath, _logger))
        {
            _logger.LogWarning("Could not register the Personal Recommendations widget frontend script.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger };
    }

    private string GetBasePath()
    {
        try
        {
            var networkConfiguration = _configurationManager.GetConfiguration<NetworkConfiguration>("network");
            if (!string.IsNullOrWhiteSpace(networkConfiguration.BaseUrl))
            {
                return $"/{networkConfiguration.BaseUrl.Trim().Trim('/')}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get the Jellyfin base path; using '/'.");
        }

        return string.Empty;
    }
}
