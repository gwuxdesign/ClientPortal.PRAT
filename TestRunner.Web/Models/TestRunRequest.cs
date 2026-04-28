namespace TestRunner.Web.Models;

public record TestRunRequest(
    string Browser,
    string Device,
    string Environment,
    string Suite,
    bool Headed,
    bool RecordVideo,
    bool RecordTraces,
    string RunnerName,
    string? CustomResolution = null,
    string? CustomTag = null
);
