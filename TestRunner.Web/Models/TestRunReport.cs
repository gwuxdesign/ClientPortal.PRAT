namespace TestRunner.Web.Models;

public class TestRunReport
{
    public string Id { get; set; } = "";
    public DateTime RunAt { get; set; }
    public string RunBy { get; set; } = "";
    public string Browser { get; set; } = "";
    public string Device { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Suite { get; set; } = "";
    public bool Headed { get; set; }
    public bool RecordVideo { get; set; }
    public List<TestScenarioResult> Scenarios { get; set; } = new();
    public string ReportFileName { get; set; } = "";
    public string TrxFileName { get; set; } = "";
    public TimeSpan TotalDuration { get; set; }
    public int TotalPassed { get; set; }
    public int TotalFailed { get; set; }
    public int TotalSkipped { get; set; }
}

public class TestScenarioResult
{
    public string Name { get; set; } = "";
    public string Outcome { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? TraceFile { get; set; }
    public string? VideoFile { get; set; }
    public List<string> Steps { get; set; } = new();
}