using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.PersonalRecommendations.Services;

/// <summary>
/// Ensures the plugin's expensive work (scanning the library and scoring candidates) never
/// runs on more than one thread at a time, regardless of which of the scheduled task, the
/// playback-triggered refresh, or an API cache-miss warm-up triggered it. Keeps CPU usage
/// predictable on servers that also transcode.
/// </summary>
public sealed class ComputeGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Runs <paramref name="action"/>, waiting for any other gated work to finish first.
    /// </summary>
    /// <param name="action">The work to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunAsync(Action action, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            action();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> only if no other gated work is currently running;
    /// otherwise does nothing. Use for opportunistic refreshes where skipping is fine because
    /// something else will cover it later (the scheduled task, or a future retry).
    /// </summary>
    /// <param name="action">The work to run.</param>
    /// <returns><c>true</c> if it ran, <c>false</c> if it was skipped because the gate was busy.</returns>
    public bool TryRun(Action action)
    {
        if (!_semaphore.Wait(0))
        {
            return false;
        }

        try
        {
            action();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
