using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// A single, configurable fault condition that can be applied to a scenario
/// run to test PRAT's resilience under conditions known to cause flaky
/// test failures. Each profile is applied via a Reqnroll hook or directly
/// within the step definition it targets.
/// </summary>
public interface IFaultProfile
{
    /// <summary>
    /// The profile's identifier, matched against the FAULT_PROFILE
    /// configuration parameter (e.g. "Timing", "Concurrency", "Load",
    /// "Latency", "Retry").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Applies the fault condition. 'magnitude' is profile-specific
    /// (e.g. a delay in milliseconds for Timing, a document count for
    /// Load) and is supplied via the FAULT_MAGNITUDE configuration
    /// parameter. A magnitude of 0 should leave the scenario unaffected,
    /// so a profile can be registered but left dormant for runs that
    /// don't target it.
    /// </summary>
    Task ApplyAsync(IPage page, int magnitude, CancellationToken cancellationToken = default);
}
