using System.Diagnostics;
using System.Xml;
using System.Runtime.CompilerServices;
using TestRunner.Web.Models;

namespace TestRunner.Web.Services;

public class TestRunnerService : ITestRunnerService
{
    public async IAsyncEnumerable<string> RunTestsAsync(
        TestRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // var solutionRoot = Path.GetFullPath(
        //     Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
        // );

        // var nunitConsolePath = Path.Combine(
        //     solutionRoot,
        //     "tools",
        //     "nunit-console",
        //     "bin",
        //     "net6.0",
        //     "nunit3-console.exe"
        // );

        // var workingDirectory = Path.Combine(
        //     solutionRoot,
        //     "tests",
        //     "ClientPortal.PRAT.Acceptance"
        // );

        // var dllPath = Path.Combine(
        //     workingDirectory,
        //     "bin/Debug/net10.0/ClientPortal.PRAT.Acceptance.dll"
        // );

        // var psi = new ProcessStartInfo
        // {
        //     FileName = "mono",
        //     Arguments =
        //         $"\"{nunitConsolePath}\" " +
        //         $"\"{dllPath}\" " +
        //         $"--where \"cat == {request.Suite}\" " +
        //         $"--labels=All " +
        //         $"--trace=Verbose",
        //     WorkingDirectory = workingDirectory,
        //     RedirectStandardOutput = true,
        //     RedirectStandardError = true,
        //     UseShellExecute = false,
        //     CreateNoWindow = true
        // };

        // var psi = new ProcessStartInfo
        // {
        //     FileName = "dotnet",
        //     Arguments =
        //         $"vstest " +
        //         $"\"{dllPath}\" " +
        //         $"/TestCaseFilter:\"TestCategory={request.Suite}\" " +
        //         $"/Logger:Console;Verbosity=detailed ",
        //     WorkingDirectory = workingDirectory,
        //     RedirectStandardOutput = true,
        //     RedirectStandardError = true,
        //     UseShellExecute = false,
        //     CreateNoWindow = true
        // };

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"test --filter \"TestCategory={request.Suite}\" --logger \"console;verbosity=detailed\"",
            // Arguments = $"vstest --filter \"TestCategory={request.Suite}\" --logger \"console;verbosity=detailed\" -- RunConfiguration.Trace=Verbose -- NUnit.NumberOfTestWorkers=1 -- NUnit.InternalTraceLevel=Verbose",
            // Arguments =
            //     $"vstest " +
            //     $"\"{dllPath}\" " +
            //     $"/TestCaseFilter:\"TestCategory={request.Suite}\" " +
            //     $"/Logger:Console;Verbosity=detailed " +
            //     $"/Parallel:None",
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
        process.Start();

        // Stream STDOUT
        while (!process.StandardOutput.EndOfStream)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line is not null)
                yield return line;

            if (cancellationToken.IsCancellationRequested)
            {
                process.Kill(true);
                yield break;
            }
        }

        // Stream STDERR
        while (!process.StandardError.EndOfStream)
        {
            var line = await process.StandardError.ReadLineAsync();
            if (line is not null)
                yield return "[ERR] " + line;
        }

        await process.WaitForExitAsync(cancellationToken);

        yield return "DONE";
    }
}
