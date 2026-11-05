using TestRunner.Web.Services.RootCause;

namespace TestRunner.Web.Tests;

/// <summary>
/// Extraction of the evidence from a test's raw standard output, on a small
/// hand made sample in the shape the harness reads (SCENARIO/STEP markers,
/// Gherkin lines, "-> done/error" lines with durations, [RequestFailed] lines).
/// </summary>
public class EvidenceExtractorTests
{
    private const string StdOut = """
        SCENARIO START: Reset password
        Given the user is on the page
        STEP START: the user is on the page
        -> done: Steps.GivenPage() (2.1s)
        STEP END: the user is on the page
        When the user submits the form
        STEP START: the user submits the form
        -> error: Timeout 10000ms exceeded. (13.4s)
        STEP END: the user submits the form
        [RequestFailed] POST https://example.test/api/forgotten — net::ERR_FAILED
        """;

    [Fact]
    public void Step_seconds_are_read_in_order_with_their_keyword()
    {
        Assert.Equal("Given=2.1;When=13.4", RootCauseEvidence.StepSecondsOf(StdOut));
    }

    [Fact]
    public void Failed_step_is_the_gherkin_line_of_the_errored_step()
    {
        Assert.Equal("When the user submits the form", RootCauseEvidence.FailedStepOf(StdOut));
    }

    [Fact]
    public void Network_events_are_read_and_hosts_are_masked()
    {
        var e = RootCauseEvidence.FromStdOut(StdOut, "System.TimeoutException : Timeout 10000ms exceeded.");
        Assert.Equal("POST ***/api/forgotten — net::ERR_FAILED", e.NetworkEvents);
    }

    [Fact]
    public void Only_the_first_three_network_events_are_kept()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 5).Select(n => $"[RequestFailed] GET /a{n} — net::ERR_ABORTED"));
        Assert.Equal(3, RootCauseEvidence.NetworkEventsOf(lines).Split(" | ").Length);
    }

    [Fact]
    public void Clean_is_idempotent_masks_emails_and_truncates()
    {
        var once = RootCauseEvidence.Clean("a\n  b  user@example.com https://host.example/x", 400);
        Assert.Equal("a b <email> ***/x", once);
        Assert.Equal(once, RootCauseEvidence.Clean(once, 400));
        Assert.Equal(5, RootCauseEvidence.Clean("abcdefghij", 5).Length);
    }

    [Fact]
    public void A_failure_without_timeout_or_failed_request_evidence_is_not_classified()
    {
        var clf = new RootCauseClassifier(RootCauseModel.Load(ModelPath()));
        var e = new RootCauseEvidence("Xunit.Sdk.EqualException : Assert.Equal() Failure", "Then the heading is shown", "Given=1.0;Then=0.2", "");
        var result = clf.Classify(e);
        Assert.False(result.Classified);
        Assert.Null(result.Label);
    }

    [Fact]
    public void An_overload_prediction_carries_the_ambiguity_note()
    {
        var clf = new RootCauseClassifier(RootCauseModel.Load(ModelPath()));
        var e = new RootCauseEvidence(
            "System.TimeoutException : Timeout 10000ms exceeded.", "When the user opens the documents page",
            "Given=1.0;And=0.5;When=1.2;When=13.4", "");
        var result = clf.Classify(e);
        Assert.True(result.Classified);
        Assert.Equal("overload", result.Label);
        Assert.Equal(RootCauseClassifier.OverloadNote, result.Note);
    }

    private static string ModelPath()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "ClientPortal.PRAT.sln")))
            d = d.Parent;
        return Path.Combine(d!.FullName, "data", "root-cause-frozen-model.json");
    }
}
