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

        yield return "SERVICE CALLED";
        yield return $"Working directory: {psi.WorkingDirectory}";
        yield return $"Exists: {Directory.Exists(psi.WorkingDirectory)}";
        yield return $"Command: {psi.FileName} {psi.Arguments}";

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
            var reader = process.StandardOutput;
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
                await channel.Writer.WriteAsync(line, cancellationToken);
        }, cancellationToken);

        var stderrTask = Task.Run(async () =>
        {
            var reader = process.StandardError;
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
                await channel.Writer.WriteAsync("[ERR] " + line, cancellationToken);
        }, cancellationToken);

        _ = Task.WhenAll(stdoutTask, stderrTask)
                .ContinueWith(_ => channel.Writer.Complete(), TaskScheduler.Default);

        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                yield break;
            }
            yield return line;
        }

        await process.WaitForExitAsync(cancellationToken);

        yield return "DONE";
    }
}
