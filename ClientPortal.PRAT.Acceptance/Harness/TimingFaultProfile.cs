using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Injects a configurable delay before an assertion runs, to test whether
/// PRAT's checks are genuinely waiting on application state or are timing
/// dependent. Applied to the Login scenario's outcome check
/// (see LoginSteps.cs).
/// </summary>
public class TimingFaultProfile : IFaultProfile
{
    public string Name => "Timing";

    public async Task ApplyAsync(IPage page, int magnitude, CancellationToken cancellationToken = default)
    {
        if (magnitude > 0)
        {
            await Task.Delay(magnitude, cancellationToken);
        }
    }
}
