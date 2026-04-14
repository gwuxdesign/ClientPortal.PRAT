namespace TestRunner.Web.Models;

public record TestRunRequest(
    string Browser,
    string Device,
    string Environment,
    string Suite,
    bool Headed
);
