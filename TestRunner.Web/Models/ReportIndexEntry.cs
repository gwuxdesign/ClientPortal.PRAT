namespace TestRunner.Web.Models;

public class ReportIndexEntry
{
    public string Id { get; set; } = "";
    public string RunId { get; set; } = "";
    public DateTime RunAt { get; set; }
    public string RunBy { get; set; } = "";
    public string Browser { get; set; } = "";
    public string Device { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Suite { get; set; } = "";
    public int TotalPassed { get; set; }
    public int TotalFailed { get; set; }
    public int TotalSkipped { get; set; }
    public string DurationDisplay { get; set; } = "";
    public string ReportFileName { get; set; } = "";
    public string RunFolder { get; set; } = "";
}
