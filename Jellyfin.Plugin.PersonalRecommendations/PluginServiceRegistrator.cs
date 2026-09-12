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
        serviceCollection.AddSingleton<WatchHistoryReader>();
        serviceCollection.AddSingleton<CandidateProvider>();
        serviceCollection.AddSingleton<RecommendationEngine>();
        serviceCollection.AddSingleton<RecommendationPlaylistService>();
        serviceCollection.AddHostedService<PlaybackChangeMonitor>();
        serviceCollection.AddSingleton<IScheduledTask, RecommendationRefreshTask>();
    }
}
