using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PersonalRecommendations.Api;

/// <summary>
/// Serves the home screen widget's client script. Deliberately not <c>[Authorize]</c>: it's
/// loaded via a bare <c>&lt;script src&gt;</c> tag before any user session exists in the page.
/// The data the script goes on to fetch (<see cref="RecommendationsController"/>) is still
/// authenticated as normal.
/// </summary>
[ApiController]
[Route("PersonalRecommendations")]
public sealed class WidgetController : ControllerBase
{
    private const string ScriptResourceName = "Jellyfin.Plugin.PersonalRecommendations.Api.client.js";

    /// <summary>
    /// Gets the widget's client script.
    /// </summary>
    [HttpGet("script")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/javascript")]
    public ActionResult GetScript()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ScriptResourceName);
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, "application/javascript");
    }
}
