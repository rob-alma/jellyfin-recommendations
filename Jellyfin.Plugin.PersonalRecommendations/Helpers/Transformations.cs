using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.PersonalRecommendations.Helpers;

/// <summary>
/// The callback the File Transformation plugin invokes to patch index.html.
/// </summary>
public static class Transformations
{
    /// <summary>
    /// Inserts the widget script tag into the served index.html.
    /// </summary>
    /// <param name="payload">The file's current contents, from the File Transformation plugin.</param>
    public static string IndexTransformation(PatchRequestPayload payload)
    {
        var contents = ScriptMarkup.RemoveExisting(payload.Contents ?? string.Empty);
        var script = ScriptMarkup.Build(WidgetRuntime.BasePath, "FileTransformation=\"true\"");
        var bodyClosing = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);

        return bodyClosing < 0 ? contents : contents.Insert(bodyClosing, script);
    }
}

/// <summary>
/// The shape the File Transformation plugin sends its callback.
/// </summary>
public class PatchRequestPayload
{
    /// <summary>
    /// Gets or sets the file's current contents.
    /// </summary>
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}
