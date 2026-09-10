using System;
using System.Collections.Generic;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Resolves a fault profile by name, matched against the FAULT_PROFILE
/// configuration parameter. New profiles (Latency, Retry, Concurrency,
/// Load) are added here as each is implemented — see
/// docs/harness-design.md for the full mapping.
/// </summary>
public static class FaultProfileRegistry
{
    private static readonly Dictionary<string, IFaultProfile> _profiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Timing", new TimingFaultProfile() }
        };

    /// <summary>
    /// Returns the matching profile, or null if 'name' is null/empty
    /// (meaning no fault profile is configured — the scenario should run
    /// unmodified). Throws for an unrecognised name, so a typo in
    /// configuration fails loudly rather than silently running clean.
    /// </summary>
    public static IFaultProfile? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (_profiles.TryGetValue(name, out var profile))
        {
            return profile;
        }

        throw new ArgumentException(
            $"Unknown fault profile '{name}'. Registered profiles: {string.Join(", ", _profiles.Keys)}");
    }
}