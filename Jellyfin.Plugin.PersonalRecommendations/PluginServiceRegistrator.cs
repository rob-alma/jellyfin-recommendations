using Jellyfin.Plugin.PersonalRecommendations.ScheduledTasks;
using Jellyfin.Plugin.PersonalRecommendations.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.PersonalRecommendations;

/// <summary>
/// Registers plugin services with Jellyfin's dependency injection container.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ItemFeatureExtractor>();
        serviceCollection.AddSingleton<LibrarySnapshotProvider>();
        serviceCollection.AddSingleton<UserDataLookup>();
        serviceCollection.AddSingleton<WatchHistoryReader>();
        serviceCollection.AddSingleton<CandidateProvider>();
        serviceCollection.AddSingleton<RecommendationEngine>();
        serviceCollection.AddSingleton<RecommendationCache>();
        serviceCollection.AddSingleton<ComputeGate>();
        serviceCollection.AddHostedService<PlaybackChangeMonitor>();
        serviceCollection.AddHostedService<LegacyPlaylistCleanupService>();
        serviceCollection.AddSingleton<IScheduledTask, RecommendationRefreshTask>();
        serviceCollection.AddSingleton<IScheduledTask, FrontendRegistrationStartupTask>();
    }
}
