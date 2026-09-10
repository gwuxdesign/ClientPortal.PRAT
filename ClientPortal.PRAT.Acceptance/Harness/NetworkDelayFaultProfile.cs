using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Base for fault profiles that simulate degraded network conditions by
/// intercepting every request from the point ApplyAsync is called and
/// delaying each response by 'magnitude' ms before letting it continue.
///
/// Delaying the actual response — rather than delaying before a check
/// starts — genuinely competes with Playwright's action/assertion
/// timeout windows. A flat pre-assertion delay cannot induce failures,
/// since web-first assertions poll for their own timeout window
/// regardless of when they're called; see docs/harness-design.md for
/// the finding that led to this shared base.
///
/// Must be applied BEFORE the interaction that triggers the request it's
/// meant to affect, not before the later assertion.
/// </summary>
public abstract class NetworkDelayFaultProfile : IFaultProfile
{
    public abstract string Name { get; }

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