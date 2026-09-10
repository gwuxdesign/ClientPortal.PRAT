using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Intercepts every network request the page makes from the point
/// ApplyAsync is called, delaying each response by 'magnitude' ms before
/// letting it continue. Unlike a flat pre-assertion delay (see
/// TimingFaultProfile — being reworked to follow this same pattern), this
/// genuinely slows down the app's real state change, so it competes
/// against Playwright's own action-timeout window rather than just
/// shifting when a check starts.
///
/// Must be applied BEFORE the interaction that triggers the request it's
/// meant to affect (see MenuSteps.cs, applied before the menu-item click,
/// not before the later navigation assertion).
/// </summary>
public class LatencyFaultProfile : IFaultProfile
{
    public string Name => "Latency";

    public async Task ApplyAsync(IPage page, int magnitude, CancellationToken cancellationToken = default)
    {
        if (magnitude <= 0)
        {
            return;
        }

        await page.RouteAsync("**/*", async route =>
        {
            await Task.Delay(magnitude, cancellationToken);
            await route.ContinueAsync();
        });
    }
}