using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.PersonalRecommendations.Configuration;
using Jellyfin.Plugin.PersonalRecommendations.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.PersonalRecommendations;

/// <summary>
/// Main entry point for the Personal Recommendations plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The stable plugin identifier.
    /// </summary>
    public static readonly Guid PluginGuid = Guid.Parse("9985ba03-cbb5-4b44-a997-3a141a1232ab");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Personal Recommendations";

    /// <inheritdoc />
    public override string Description => "A \"Recommended For You\" home screen widget built from each user's watch history.";

    /// <inheritdoc />
    public override Guid Id => PluginGuid;

    /// <summary>
    /// Gets the active plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "Personal Recommendations",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace),
                EnableInMainMenu = true,
                MenuSection = "plugins",
                MenuIcon = "auto_awesome"
            }
        ];
    }

    /// <inheritdoc />
    public override void OnUninstalling()
    {
        // Best-effort: if the widget script was patched directly into index.html, try to
        // remove it so uninstalling the plugin doesn't leave a dead <script> tag behind.
        var method = FrontendInjectionMethods.Normalize(Configuration.FrontendInjectionMethod);
        if (method is FrontendInjectionMethods.Direct or FrontendInjectionMethods.Automatic)
        {
            DirectScriptInjector.TryRemove(ApplicationPaths, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        }

        base.OnUninstalling();
    }
}
