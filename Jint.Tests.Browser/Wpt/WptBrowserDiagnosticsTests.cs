namespace Jint.Tests.Browser.Wpt;

public sealed class WptBrowserDiagnosticsTests
{
    [Test]
    public void ATimeoutRetainsItsVerdictAndReportsPartialProgress()
    {
        var collector = new WptBrowserCollector();
        collector.Add("""{"kind":"result","name":"finished assertion","status":0,"message":""}""", "loading", scriptActive: true);
        collector.Add("""{"kind":"completion","status":2,"message":"","count":1}""", "loading", scriptActive: true);

        var outcome = collector.Outcome(budgetFailure: null);
        outcome.HarnessError.Should().Be("the harness reported TIMEOUT: ");
        outcome.Results.Should().BeEmpty("a partial report must not become a successful census measurement");
        var progress = collector.DescribeProgress();
        progress.Should().Contain("results=1;")
            .And.Contain("lastReportReadyState=loading;")
            .And.Contain("lastReportScriptActive=True")
            .And.NotContain("unreported");
    }

    [Test]
    public void NoReportIsDistinguishedFromACompletedEmptyReport()
    {
        var collector = new WptBrowserCollector();
        collector.DescribeProgress().Should().Contain("firstResultMs=unreported;")
            .And.Contain("completionMs=unreported;");

        collector.Add("""{"kind":"completion","status":0,"message":"","count":0}""", "complete");

        collector.Outcome(budgetFailure: null).HarnessError.Should().BeNull();
        collector.DescribeProgress().Should().Contain("firstResultMs=unreported;")
            .And.NotContain("completionMs=unreported;")
            .And.Contain("lastReportReadyState=complete;")
            .And.Contain("lastReportScriptActive=False");
    }
}
