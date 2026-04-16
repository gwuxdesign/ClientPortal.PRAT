using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TestRunner.Web.Models;

namespace TestRunner.Web.Services;

public class TestRunnerService : ITestRunnerService
{
    public async IAsyncEnumerable<string> RunTestsAsync(
        TestRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var deviceType = request.Device == "Custom" && !string.IsNullOrEmpty(request.CustomResolution)
            ? $"custom:{request.CustomResolution}"
            : request.Device;

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"test --filter \"TestCategory={request.Suite}\" --logger \"console;verbosity=detailed\"",
            WorkingDirectory = "/Users/gragdad/Documents/development/GIT/ClientPortal.PRAT/tests/ClientPortal.PRAT.Acceptance",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.Environment["BROWSER"] = request.Browser;
        psi.Environment["HEADED"] = request.Headed ? "1" : "0";
        psi.Environment["DEVICE_TYPE"] = deviceType;
        psi.Environment["ENVIRON"] = request.Environment;

        yield return "SERVICE CALLED";
        yield return $"Working directory: {psi.WorkingDirectory}";
        yield return $"Exists: {Directory.Exists(psi.WorkingDirectory)}";
        yield return $"Command: {psi.FileName} {psi.Arguments}";
        yield return $"Browser: {request.Browser} | Headed: {request.Headed} | Device: {deviceType} | Environment: {request.Environment} | Suite: {request.Suite}";

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var started = process.Start();
        if (!started)
        {
            yield return "[ERROR] Process failed to start";
            yield break;
        }

        var channel = Channel.CreateUnbounded<string>();

        var stdoutTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
                await channel.Writer.WriteAsync(line, CancellationToken.None);
        }, CancellationToken.None);

        var stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
                await channel.Writer.WriteAsync("[ERR] " + line, CancellationToken.None);
        }, CancellationToken.None);

        _ = Task.WhenAll(stdoutTask, stderrTask)
                .ContinueWith(_ => channel.Writer.Complete(), TaskScheduler.Default);

        var wasCancelled = false;

        try
        {
            await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return line;
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                wasCancelled = true;
            }
            await process.WaitForExitAsync(CancellationToken.None);
        }

        if (wasCancelled)
            yield return "--- Test run cancelled ---";

        yield return "DONE";
    }
}
