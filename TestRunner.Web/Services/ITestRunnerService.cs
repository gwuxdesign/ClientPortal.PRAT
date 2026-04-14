using TestRunner.Web.Models;

namespace TestRunner.Web.Services;

public interface ITestRunnerService
{
    IAsyncEnumerable<string> RunTestsAsync(TestRunRequest request, CancellationToken token);
}
