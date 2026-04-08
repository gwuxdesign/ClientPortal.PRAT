using System;
using System.IO;
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
            bool isHeaded = headed == "1";

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
                Args = new[] { "--disable-dev-shm-usage" }
            });

            var contextOptions = GetContextOptions();

            // Add video recording for debugging failures
            contextOptions.RecordVideoDir = "artifacts/videos";

            _world.Context = await _world.Browser.NewContextAsync(contextOptions);
            _world.Context.SetDefaultTimeout(10000);

            _world.Page = await _world.Context.NewPageAsync();

            // Capture browser console logs
            _world.Page.Console += (_, msg) =>
            {
                Console.WriteLine($"[BrowserConsole] {msg.Type}: {msg.Text}");
            };

            // Capture failed network requests
            _world.Page.RequestFailed += (_, req) =>
            {
                Console.WriteLine($"[RequestFailed] {req.Method} {req.Url} — {req.Failure}");
            };

            // Start tracing
            await _world.Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });

            // Wire up page objects
            _world.Pages = new PageImports(_world);
        }

        [AfterScenario(Order = 100)]
        public async Task AfterScenario(ScenarioContext scenarioContext)
        {
            var safeName = Sanitise(scenarioContext.ScenarioInfo.Title);

            // Stop tracing safely
            try
            {
                if (_world.Context != null)
                {
                    Directory.CreateDirectory("artifacts/traces");
                    var tracePath = $"artifacts/traces/{safeName}.zip";

                    await _world.Context.Tracing.StopAsync(new TracingStopOptions
                    {
                        Path = tracePath
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TracingError] {ex.Message}");
            }

            // Save video only if scenario failed
            try
            {
                if (scenarioContext.TestError != null && _world.Page?.Video != null)
                {
                    Directory.CreateDirectory("artifacts/videos");
                    var videoPath = await _world.Page.Video.PathAsync();
                    File.Move(videoPath, $"artifacts/videos/{safeName}.webm", true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoError] {ex.Message}");
            }

            // Cleanup
            try { if (_world.Context != null) await _world.Context.CloseAsync(); } catch { }
            try { if (_world.Browser != null) await _world.Browser.CloseAsync(); } catch { }
            try { _world.Playwright?.Dispose(); } catch { }
        }

        private BrowserNewContextOptions GetContextOptions()
        {
            var deviceTypeValue = Environment.GetEnvironmentVariable("DEVICE_TYPE");

            // Custom resolution: DEVICE_TYPE=custom:2560x1440
            if (!string.IsNullOrEmpty(deviceTypeValue) &&
                deviceTypeValue.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                var resolutionPart = deviceTypeValue.Substring("custom:".Length);
                var parts = resolutionPart.Split('x');

                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var width) &&
                    int.TryParse(parts[1], out var height))
                {
                    return new BrowserNewContextOptions
                    {
                        ViewportSize = new ViewportSize { Width = width, Height = height },
                        DeviceScaleFactor = 1,
                        IsMobile = false
                    };
                }

                throw new ArgumentException(
                    $"Invalid custom resolution format: {resolutionPart}. Expected WIDTHxHEIGHT (e.g., 2560x1440).");
            }

            // Standard device profiles
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

        private static string Sanitise(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
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
