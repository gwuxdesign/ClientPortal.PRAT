using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Simulates concurrent access to a shared account: launches 'magnitude'
/// parallel browser sessions logging in with the same credentials as the
/// scenario's own login attempt, testing whether session/state handling
/// holds up under simultaneous use of one account.
///
/// Unlike the other profiles, this doesn't intercept the scenario's own
/// page — it spins up independent sessions via the shared IBrowser
/// (IPage.Context.Browser), launched WITHOUT being awaited here, so they
/// run genuinely alongside the scenario's real login click rather than
/// completing beforehand. IFaultProfile's existing single-IPage shape
/// still holds; no interface change was needed to support this.
///
/// Uses the single configured 'goodLogin' account for every session —
/// there is no separate account pool configured, and this is arguably
/// the more relevant test anyway: shared-session contention on one
/// account, not parallel load spread across many.
///
/// KNOWN RISK, not yet confirmed either way: PlaywrightHooks.AfterScenario
/// closes the shared IBrowser once the scenario ends. If the scenario's
/// own login resolves before these background sessions do, they may be
/// killed mid-flight rather than completing naturally. Worth confirming
/// via a real run before relying on this profile's results.
///
/// SAFETY: only run this against REL once REL's security/fraud
/// monitoring owner has had a heads-up — see docs/harness-design.md.
/// The GitHub Actions workflow deliberately excludes Concurrency from
/// its fault_profile choices until that's happened; don't set
/// FAULT_PROFILE=Concurrency locally against REL before then.
/// </summary>
public class ConcurrencyFaultProfile : IFaultProfile
{
    public string Name => "Concurrency";

    public Task ApplyAsync(IPage page, int magnitude, CancellationToken cancellationToken = default)
    {
        if (magnitude <= 0)
        {
            return Task.CompletedTask;
        }

        var browser = page.Context.Browser
            ?? throw new InvalidOperationException("Concurrency fault requires a browser-backed context.");
        var url = page.Url;
        var creds = CredentialReader.Get("goodLogin");

        // Fire-and-forget: launched, not awaited, so the scenario's own
        // login click proceeds concurrently rather than waiting for
        // these sessions to finish first.
        _ = Task.Run(async () =>
        {
            var sessions = Enumerable.Range(0, magnitude).Select(async _ =>
            {
                IBrowserContext? context = null;
                try
                {
                    context = await browser.NewContextAsync();
                    var concurrentPage = await context.NewPageAsync();
                    await concurrentPage.GotoAsync(url);
                    await concurrentPage.Locator("#email").FillAsync(creds.Email ?? string.Empty);
                    await concurrentPage.Locator("#password").FillAsync(creds.Password ?? string.Empty);
                    await concurrentPage.Locator("button:has-text('Log in')").ClickAsync();
                }
                catch
                {
                    // Background load only — a failure here isn't a
                    // scenario assertion, so it's swallowed rather than
                    // surfaced. The scenario's own assertion is the thing
                    // under test, not these sessions' individual outcomes.
                }
                finally
                {
                    if (context != null)
                    {
                        try { await context.CloseAsync(); } catch { }
                    }
                }
            });

            await Task.WhenAll(sessions);
        }, cancellationToken);

        return Task.CompletedTask;
    }
}