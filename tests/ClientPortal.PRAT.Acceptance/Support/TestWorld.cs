using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Support
{
    // One instance per scenario, shared across hooks and step classes via DI
    public class TestWorld
    {
        // Playwright lifecycle members
        public IPlaywright     Playwright { get; set; } = default!;
        public IBrowser        Browser    { get; set; } = default!;
        public IBrowserContext Context    { get; set; } = default!;
        public IPage           Page       { get; set; } = default!;

        // Page aggregator
        public PageImports     Pages      { get; set; } = default!;

        // Configuration
        public IConfiguration  Configuration { get; }
        public string          EnvironmentName { get; }
        public string          PortalUrl { get; }

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
                .AddEnvironmentVariables()
                .Build();

            Configuration = config;
            PortalUrl = config[$"Environments:{EnvironmentName}:PortalUrl"]
                        ?? throw new InvalidOperationException($"PortalUrl not configured for {EnvironmentName}");
        }
    }
}