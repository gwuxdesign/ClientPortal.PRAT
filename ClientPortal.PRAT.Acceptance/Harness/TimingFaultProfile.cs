namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Simulates a timing/race condition on the Login flow: delays the
/// login request so the app has less time to settle before PRAT checks
/// the outcome. Applied in LoginSteps, before the login form is
/// submitted, not before the outcome assertion — the original
/// placement there was a flat pre-assertion delay that couldn't
/// actually cause failures (see docs/harness-design.md).
/// </summary>
public class TimingFaultProfile : NetworkDelayFaultProfile
{
    public override string Name => "Timing";
}