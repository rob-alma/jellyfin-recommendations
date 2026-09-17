namespace Jellyfin.Plugin.PersonalRecommendations.Helpers;

/// <summary>
/// Holds the server's reverse-proxy base path, computed once at startup by
/// <see cref="ScheduledTasks.FrontendRegistrationStartupTask"/>. Exists because the File
/// Transformation plugin invokes <see cref="Transformations.IndexTransformation"/> as a bare
/// static callback with no way to pass it fresh context, so the value has to be readable from
/// somewhere static.
/// </summary>
internal static class WidgetRuntime
{
    /// <summary>
    /// Gets or sets the server's base path (e.g. "/jellyfin"), or "" if none is configured.
    /// </summary>
    public static string BasePath { get; set; } = string.Empty;
}
