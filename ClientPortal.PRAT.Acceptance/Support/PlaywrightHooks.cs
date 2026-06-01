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

        [BeforeScenario(Order = -100)]
        public async Task BeforeScenario(ScenarioContext scenarioContext)
        {
            _world.Playwright = await Playwright.CreateAsync();

            var headed       = Environment.GetEnvironmentVariable("HEADED");
            var browserType  = Environment.GetEnvironmentVariable("BROWSER")?.ToLower() ?? "chromium";
            var recordVideo  = Environment.GetEnvironmentVariable("RECORD_VIDEO")  == "1";
            var recordTraces = Environment.GetEnvironmentVariable("RECORD_TRACES") == "1";

            bool isHeaded = headed == "1";

            IBrowserType selectedBrowserType = browserType switch
            {
                "firefox" => _world.Playwright.Firefox,
                "webkit"  => _world.Playwright.Webkit,
                _         => _world.Playwright.Chromium
            };

            _world.Browser = await selectedBrowserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = !isHeaded,
                Args     = new[] { "--disable-dev-shm-usage" }
            });

            var contextOptions = GetContextOptions(browserType);

            // Always capture video so footage is available on failure even when toggle is off
            // AfterScenario decides whether to keep or discard it
            var artifactsBase = Environment.GetEnvironmentVariable("ARTIFACTS_BASE")
                ?? AppContext.BaseDirectory;
            var videoDir = Path.Combine(artifactsBase, "artifacts", "videos");
            Directory.CreateDirectory(videoDir);
            contextOptions.RecordVideoDir = videoDir;

            _world.Context = await _world.Browser.NewContextAsync(contextOptions);
            _world.Context.SetDefaultTimeout(10000);

            _world.Page = await _world.Context.NewPageAsync();

            _world.Page.Console += (_, msg) =>
            {
                Console.WriteLine($"[BrowserConsole] {msg.Type}: {msg.Text}");
                Console.Out.Flush();
            };

            _world.Page.RequestFailed += (_, req) =>
            {
                Console.WriteLine($"[RequestFailed] {req.Method} {req.Url} — {req.Failure}");
                Console.Out.Flush();
            };

            // Always start tracing so data is available on failure even when toggle is off
            // AfterScenario decides whether to keep or discard it
            await _world.Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots   = true,
                Sources     = true
            });

            _world.Pages = new PageImports(_world);

            _world.ScenarioSafeName = GetScenarioSafeName(scenarioContext);

            Console.WriteLine($"SCENARIO FILE: {_world.ScenarioSafeName}");
            Console.WriteLine($"SCENARIO NAME: {scenarioContext.ScenarioInfo.Title}");
            Console.WriteLine($"SCENARIO START: {scenarioContext.ScenarioInfo.Title}");
            Console.Out.Flush();
        }

        [BeforeStep]
        public void BeforeStep(ScenarioContext scenarioContext)
        {
            Console.WriteLine($"STEP START: {scenarioContext.StepContext.StepInfo.Text}");
            Console.Out.Flush();
        }

        [AfterStep]
        public void AfterStep(ScenarioContext scenarioContext)
        {
            Console.WriteLine($"STEP END: {scenarioContext.StepContext.StepInfo.Text}");
            Console.Out.Flush();
        }

        [AfterScenario(Order = 100)]
        public async Task AfterScenario(ScenarioContext scenarioContext)
        {
            var safeName     = _world.ScenarioSafeName;
            var recordVideo  = Environment.GetEnvironmentVariable("RECORD_VIDEO")  == "1";
            var recordTraces = Environment.GetEnvironmentVariable("RECORD_TRACES") == "1";
            var hasFailed    = scenarioContext.TestError != null;

            var artifactsBase = Environment.GetEnvironmentVariable("ARTIFACTS_BASE")
                ?? AppContext.BaseDirectory;

            // Stop tracing — always stop to finalise the file, then decide whether to keep it
            string? rawTracePath = null;
            try
            {
                if (_world.Context != null)
                {
                    var tracesDir = Path.Combine(artifactsBase, "artifacts", "traces");
                    Directory.CreateDirectory(tracesDir);
                    rawTracePath = Path.Combine(tracesDir, $"{safeName}.zip");

                    await _world.Context.Tracing.StopAsync(new TracingStopOptions
                    {
                        Path = rawTracePath
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TracingError] {ex.Message}");
                rawTracePath = null;
            }

            // Get video path before closing context
            string? rawVideoPath = null;
            try
            {
                if (_world.Page?.Video != null)
                    rawVideoPath = await _world.Page.Video.PathAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoError] Could not get video path: {ex.Message}");
            }

            // Decide on trace before closing — file is still valid at this point
            var keepTrace = recordTraces || hasFailed;
            if (rawTracePath != null && File.Exists(rawTracePath) && !keepTrace)
            {
                try { File.Delete(rawTracePath); } catch { }
                rawTracePath = null;
            }

            // Now safe to close browser
            try { if (_world.Context != null) await _world.Context.CloseAsync(); } catch { }
            try { if (_world.Browser != null) await _world.Browser.CloseAsync(); } catch { }
            try { _world.Playwright?.Dispose(); } catch { }

            // Handle video after browser is closed
            if (rawVideoPath != null && File.Exists(rawVideoPath))
            {
                if (recordVideo || hasFailed)
                {
                    try
                    {
                        var videosDir = Path.Combine(artifactsBase, "artifacts", "videos");
                        Directory.CreateDirectory(videosDir);
                        var dest = Path.Combine(videosDir, $"{safeName}.webm");
                        File.Move(rawVideoPath, dest, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[VideoError] Could not save video: {ex.Message}");
                    }
                }
                else
                {
                    try { File.Delete(rawVideoPath); } catch { }
                }
            }

            if (hasFailed)
            {
                Console.WriteLine($"SCENARIO ERROR: {scenarioContext.TestError!.Message}");
                Console.Out.Flush();
            }

            Console.WriteLine($"SCENARIO END: {scenarioContext.ScenarioInfo.Title}");
            Console.Out.Flush();
        }

        private static BrowserNewContextOptions GetContextOptions(string browserType)
        {
            var ignoreHttpsErrors = browserType == "firefox";
            var deviceTypeValue   = Environment.GetEnvironmentVariable("DEVICE_TYPE");

            // Firefox does not support isMobile — fall back to Desktop silently
            // The UI should prevent this combination but this guards against it at runtime
            var isMobileDevice = deviceTypeValue is "Mobile" or "TabletVer" or "TabletHor";
            if (browserType == "firefox" && isMobileDevice)
            {
                Console.WriteLine("[WARN] Firefox does not support mobile/tablet emulation — falling back to Desktop context.");
                Console.Out.Flush();
                deviceTypeValue = "Desktop";
            }

            // Custom resolution: DEVICE_TYPE=custom:2560x1440
            if (!string.IsNullOrEmpty(deviceTypeValue) &&
                deviceTypeValue.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                var resolutionPart = deviceTypeValue["custom:".Length..];
                var parts = resolutionPart.Split('x');

                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var width) &&
                    int.TryParse(parts[1], out var height))
                {
                    return new BrowserNewContextOptions
                    {
                        ViewportSize      = new ViewportSize { Width = width, Height = height },
                        DeviceScaleFactor = 1,
                        IsMobile          = false,
                        IgnoreHTTPSErrors = ignoreHttpsErrors
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
                    ViewportSize      = new ViewportSize { Width = 1920, Height = 1080 },
                    DeviceScaleFactor = 1,
                    IsMobile          = false,
                    IgnoreHTTPSErrors = ignoreHttpsErrors
                },
                DeviceType.Mobile => new BrowserNewContextOptions
                {
                    ViewportSize      = new ViewportSize { Width = 375, Height = 812 },
                    DeviceScaleFactor = 3,
                    IsMobile          = true,
                    HasTouch          = true,
                    IgnoreHTTPSErrors = ignoreHttpsErrors
                },
                DeviceType.TabletVer => new BrowserNewContextOptions
                {
                    ViewportSize      = new ViewportSize { Width = 768, Height = 1024 },
                    DeviceScaleFactor = 2,
                    IsMobile          = true,
                    HasTouch          = true,
                    IgnoreHTTPSErrors = ignoreHttpsErrors
                },
                DeviceType.TabletHor => new BrowserNewContextOptions
                {
                    ViewportSize      = new ViewportSize { Width = 1024, Height = 768 },
                    DeviceScaleFactor = 2,
                    IsMobile          = true,
                    HasTouch          = true,
                    IgnoreHTTPSErrors = ignoreHttpsErrors
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string GetScenarioSafeName(ScenarioContext scenarioContext)
        {
            var title     = scenarioContext.ScenarioInfo.Title;
            var arguments = scenarioContext.ScenarioInfo.Arguments;

            if (arguments != null && arguments.Count > 0)
            {
                var parts = new List<string>();
                foreach (var value in arguments.Values)
                    parts.Add(value?.ToString() ?? "null");

                title = $"{title}_{string.Join("_", parts)}";
            }

            return Sanitise(title);
        }

        private static string Sanitise(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            name = name.Replace('"', '_')
                       .Replace('\'', '_')
                       .Replace(',', '_')
                       .Replace('(', '_')
                       .Replace(')', '_')
                       .Replace(' ', '_');

            while (name.Contains("__"))
                name = name.Replace("__", "_");

            name = name.Trim('_');

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