using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Aborts the first 'magnitude' matching requests from the point
/// ApplyAsync is called, then allows subsequent requests through
/// normally. Tests whether the app (or PRAT itself) retries a failed
/// request rather than treating a single failure as final.
///
/// Unlike Timing/Latency, magnitude here is a count, not a duration: the
/// number of consecutive attempts to fail before allowing success.
/// magnitude=1 tests whether any retry exists at all; higher values test
/// resilience under repeated failures. Stateful across calls within one
/// registration, so a fresh instance's closure state only applies for
/// the scenario it was registered against (one IPage per scenario).
///
/// Must be applied BEFORE the interaction that triggers the request it's
/// meant to affect (see ResetSteps.cs, applied before the reset-form
/// submission, not before the later confirmation assertion).
/// </summary>
public class RetryFaultProfile : IFaultProfile
{
    public string Name => "Retry";

    public async Task ApplyAsync(IPage page, int magnitude, CancellationToken cancellationToken = default)
    {
        if (magnitude <= 0)
        {
            return;
        }

        var attemptsToFail = magnitude;
        var failedSoFar = 0;

        await page.RouteAsync("**/*", async route =>
        {
            if (failedSoFar < attemptsToFail)
            {
                failedSoFar++;
                await route.AbortAsync();
                return;
            }

            await route.ContinueAsync();
        });
    }
}