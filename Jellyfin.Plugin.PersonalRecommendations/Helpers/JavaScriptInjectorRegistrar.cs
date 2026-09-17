using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.PersonalRecommendations.Helpers;

/// <summary>
/// Registers a tiny loader script with the community "JavaScript Injector" plugin, if it's
/// installed. The loader just adds a &lt;script src&gt; pointing at our own script endpoint, so
/// updates to the widget itself don't require re-registering anything here.
/// </summary>
public static class JavaScriptInjectorRegistrar
{
    private const string AssemblyName = "Jellyfin.Plugin.JavaScriptInjector";
    private const string PluginInterfaceTypeName = "Jellyfin.Plugin.JavaScriptInjector.PluginInterface";
    private const string ScriptId = "f88bf934-e2bb-4aa6-989a-3727516fe493-personal-recommendations";

    /// <summary>
    /// Attempts registration. Returns false (without error) if JavaScript Injector isn't installed.
    /// </summary>
    /// <param name="basePath">The server's reverse-proxy base path, or "".</param>
    /// <param name="logger">Logger.</param>
    public static bool TryRegister(string basePath, ILogger logger)
    {
        try
        {
            var plugin = Plugin.Instance;
            if (plugin is null)
            {
                return false;
            }

            var assembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(candidate => candidate.FullName?.Contains(AssemblyName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (assembly is null)
            {
                logger.LogDebug("JavaScript Injector plugin was not found.");
                return false;
            }

            var pluginInterfaceType = assembly.GetType(PluginInterfaceTypeName);
            var registerMethod = pluginInterfaceType?.GetMethod("RegisterScript");
            if (registerMethod is null)
            {
                logger.LogWarning("JavaScript Injector registration interface was not found.");
                return false;
            }

            var payload = new JObject
            {
                { "id", ScriptId },
                { "name", "Personal Recommendations loader" },
                { "script", BuildLoaderScript(basePath) },
                { "enabled", true },
                { "requiresAuthentication", false },
                { "pluginId", plugin.Id.ToString() },
                { "pluginName", plugin.Name },
                { "pluginVersion", typeof(JavaScriptInjectorRegistrar).Assembly.GetName().Version?.ToString() ?? string.Empty }
            };

            var result = registerMethod.Invoke(null, [payload]);
            if (result is not true)
            {
                logger.LogWarning("JavaScript Injector rejected the Personal Recommendations registration.");
                return false;
            }

            logger.LogInformation("Personal Recommendations widget registered with JavaScript Injector.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register the Personal Recommendations widget with JavaScript Injector.");
            return false;
        }
    }

    private static string BuildLoaderScript(string basePath)
    {
        var scriptUrl = JsonConvert.SerializeObject($"{basePath}/PersonalRecommendations/script");

        return $$"""
            (() => {
                'use strict';
                if (document.querySelector('script[plugin="PersonalRecommendations"]')) {
                    return;
                }
                const script = document.createElement('script');
                script.defer = true;
                script.dataset.plugin = 'PersonalRecommendations';
                script.src = {{scriptUrl}};
                (document.head || document.documentElement).appendChild(script);
            })();
            """;
    }
}
