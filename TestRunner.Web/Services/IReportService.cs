using TestRunner.Web.Models;

namespace TestRunner.Web.Services;

public interface IReportService
{
    Task<string> GenerateReportAsync(TestRunReport report, string runId);
    Task<List<ReportIndexEntry>> GetReportIndexAsync();
    string GetReportsDirectory();
}