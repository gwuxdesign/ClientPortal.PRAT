using TestRunner.Web.Components;
using TestRunner.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(30);
    options.KeepAliveInterval     = TimeSpan.FromMinutes(15);
    options.HandshakeTimeout      = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IReportService, ReportService>();
builder.Services.AddSingleton<ITestRunnerService, TestRunnerService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Serve artifacts (videos, traces) from per-run folders
app.MapGet("/reports/artifact/{runId}/{fileName}",
    async (string runId, string fileName, IReportService reportService, HttpContext ctx) =>
    {
        // Guard against path traversal
        if (runId.Contains('/') || runId.Contains('\\') ||
            fileName.Contains('/') || fileName.Contains('\\'))
            return Results.BadRequest();

        var path = Path.Combine(reportService.GetReportsDirectory(), runId, fileName);
        if (!File.Exists(path)) return Results.NotFound();

        var ext = Path.GetExtension(fileName).ToLower();
        var contentType = ext switch
        {
            ".webm" => "video/webm",
            ".zip"  => "application/zip",
            _       => "application/octet-stream"
        };

        return Results.File(path, contentType, enableRangeProcessing: true);
    });

app.Run();
