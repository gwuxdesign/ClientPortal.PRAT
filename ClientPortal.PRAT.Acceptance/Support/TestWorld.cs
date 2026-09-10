using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Harness;

namespace ClientPortal.PRAT.Acceptance.Support
{
    // One instance per scenario, shared across hooks and step classes via DI
    public class TestWorld
    {
        // Playwright lifecycle members
        public IPlaywright Playwright { get; set; } = default!;
        public IBrowser Browser { get; set; } = default!;
        public IBrowserContext Context { get; set; } = default!;
        public IPage Page { get; set; } = default!;

        // Page aggregator
        public PageImports Pages { get; set; } = default!;

        // Configuration
        public IConfiguration Configuration { get; }
        public string EnvironmentName { get; }
        public string BaseUrl { get; }
        public string ScenarioSafeName { get; set; } = "";

        // Harness fault injection — resolved once per scenario from
        // FAULT_PROFILE/FAULT_MAGNITUDE, following the same environment
        // variable convention as ENVIRON. FaultProfile is null when no
        // fault profile is configured, meaning the scenario runs unmodified.
        public IFaultProfile? FaultProfile { get; }
        public int FaultMagnitude { get; }

        public TestWorld()
        {
            // Build configuration
            var baseConfig = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            EnvironmentName = Environment.GetEnvironmentVariable("ENVIRON")
                               ?? baseConfig["DefaultEnvironment"]
                               ?? "REL";

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddConfiguration(baseConfig)
                .AddJsonFile($"appsettings.{EnvironmentName}.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            Configuration = config;
            BaseUrl = config[$"Environments:{EnvironmentName}:BaseUrl"]
                        ?? throw new InvalidOperationException($"BaseUrl not configured for {EnvironmentName}");

            FaultProfile = FaultProfileRegistry.Resolve(Environment.GetEnvironmentVariable("FAULT_PROFILE"));
            FaultMagnitude = int.TryParse(Environment.GetEnvironmentVariable("FAULT_MAGNITUDE"), out var magnitude)
                ? magnitude
                : 0;
        }
    }
}