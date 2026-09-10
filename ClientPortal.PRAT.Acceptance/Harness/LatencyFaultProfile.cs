namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Simulates degraded network conditions on the Navigation flow: delays
/// every request from the point ApplyAsync is called. Applied in
/// MenuSteps, before the menu-item click that triggers navigation.
/// </summary>
public class LatencyFaultProfile : NetworkDelayFaultProfile
{
    public override string Name => "Latency";
}