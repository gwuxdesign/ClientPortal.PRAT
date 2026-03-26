using System;
using System.Threading.Tasks;
using Reqnroll;
using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Support
{
    [Binding]
    public sealed class PlaywrightHooks
    {
        private readonly TestWorld _world;

        public PlaywrightHooks(TestWorld world) => _world = world;

        // Run early so the world is ready before any steps execute
        [BeforeScenario(Order = -100)]
        public async Task BeforeScenario()
        {
            _world.Playwright = await Playwright.CreateAsync();

            var headed = Environment.GetEnvironmentVariable("HEADED");
            bool isHeaded = !string.IsNullOrEmpty(headed) && headed == "1";

            var browserType = Environment.GetEnvironmentVariable("BROWSER")?.ToLower() ?? "chromium";

            IBrowserType selectedBrowserType = browserType switch
            {
                "firefox" => _world.Playwright.Firefox,
                "webkit" => _world.Playwright.Webkit,
                _ => _world.Playwright.Chromium
            };

            _world.Browser = await selectedBrowserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = !isHeaded,
                Args = new[] { "--disable-dev-shm-usage" } // CI stability on hosted agents
            });

            var contextOptions = GetContextOptions();
            _world.Context = await _world.Browser.NewContextAsync(contextOptions);

            // Set a consistent default timeout if you like, keeps flaky waits down
            _world.Context.SetDefaultTimeout(10000);

            _world.Page = await _world.Context.NewPageAsync();

            // Optional: start tracing for each scenario, very helpful for failures
            await _world.Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });

            // Now that Page exists, wire up page objects
            _world.Pages = new PageImports(_world);
        }

        [AfterScenario(Order = 100)]
        public async Task AfterScenario(ScenarioContext scenarioContext)
        {
            var tracePath = $"artifacts/traces/{Sanitise(scenarioContext.ScenarioInfo.Title)}.zip";
            await _world.Context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });

            if (_world.Context != null) await _world.Context.CloseAsync();
            if (_world.Browser != null) await _world.Browser.CloseAsync();
            _world.Playwright?.Dispose();
        }

        private BrowserNewContextOptions GetContextOptions()
        {

            var deviceTypeValue = Environment.GetEnvironmentVariable("DEVICE_TYPE");

            if (!string.IsNullOrEmpty(deviceTypeValue) && deviceTypeValue.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                var resolutionPart = deviceTypeValue.Substring("custom:".Length);
                var parts = resolutionPart.Split('x');

                if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
                {
                    return new BrowserNewContextOptions
                    {
                        ViewportSize = new ViewportSize { Width = width, Height = height },
                        DeviceScaleFactor = 1,
                        IsMobile = false
                    };
                }
                else
                {
                    throw new ArgumentException($"Invalid custom resolution format: {resolutionPart}. Expected format: WIDTHxHEIGHT (e.g., 2560x1440).");
                }
            }

            var deviceType = Enum.TryParse<DeviceType>(deviceTypeValue, true, out var parsedType)
                    ? parsedType
                    : DeviceType.Desktop;

            return deviceType switch
            {
                DeviceType.Desktop => new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    DeviceScaleFactor = 1,
                    IsMobile = false
                },
                DeviceType.Mobile => new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 375, Height = 812 },
                    DeviceScaleFactor = 3,
                    IsMobile = true,
                    HasTouch = true
                },
                DeviceType.TabletVer => new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 768, Height = 1024 },
                    DeviceScaleFactor = 2,
                    IsMobile = true,
                    HasTouch = true
                },
                DeviceType.TabletHor => new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1024, Height = 768 },
                    DeviceScaleFactor = 2,
                    IsMobile = true,
                    HasTouch = true
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static DeviceType GetDeviceType()
        {
            var value = Environment.GetEnvironmentVariable("DEVICE_TYPE");
            return Enum.TryParse<DeviceType>(value, true, out var deviceType)
                ? deviceType
                : DeviceType.Desktop;
        }

        private static string Sanitise(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }

    public enum DeviceType
    {
        Desktop,
        Mobile,
        TabletVer,

        TabletHor
    }
}