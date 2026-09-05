using System.Text.Json;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The domains a front end sends at a page that an automation client never does — and the two page-level
/// commands that were the last of the recorded surface left unanswered.
/// </summary>
/// <remarks>
/// Each of these asks one of two questions: does the command answer at all (because a client that gets
/// <c>-32601</c> while attaching stops there), and — where the answer is real rather than a no-op — is what
/// it says true of the page. <c>CSS.getComputedStyleForNode</c> and <c>Performance.getMetrics</c> are the two
/// that are real, so both are checked against something else that already knows the answer.
/// </remarks>
[NonParallelizable]
public class FrontEndDomainTests
{
    [Test]
    public async Task SecurityAndOverlayAnswerWhatAFrontEndSendsWhileAttaching()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync("Security.enable", null, attachment);
        await session.ResultAsync("Security.setIgnoreCertificateErrors", """{"ignore":true}""", attachment);
        await session.ResultAsync("Security.disable", null, attachment);

        await session.ResultAsync("Overlay.enable", null, attachment);
        await session.ResultAsync("Overlay.setShowViewportSizeOnResize", """{"show":true}""", attachment);
        await session.ResultAsync("Overlay.setPausedInDebuggerMessage", """{"message":"Paused"}""", attachment);
        await session.ResultAsync("Overlay.hideHighlight", null, attachment);
        await session.ResultAsync("Overlay.disable", null, attachment);
    }

    [Test]
    public async Task DrawingCommandsStayAbsentBecauseThereIsNothingToDrawOn()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        foreach (var method in (string[]) ["Overlay.highlightNode", "Overlay.highlightRect", "Overlay.setInspectMode"])
        {
            var error = await session.ErrorAsync(method, "{}", attachment);
            error.GetProperty("code").GetInt32().Should().Be(-32601, "a client feature-detecting {0} is told the truth", method);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TheComputedStyleTheCssDomainReportsIsTheOneThePageReads(bool cyclicVariables)
    {
        await using var session = await PageSession.CreateAsync();
        var style = cyclicVariables
            ? "--a:var(--b);--b:var(--a);display:inline;color:var(--a,rgb(1,2,3))"
            : "display:inline;color:rgb(1,2,3)";
        var attachment = await OpenAsync(session, $"<html><body><div id='box' style='{style}'>x</div></body></html>");

        await session.ResultAsync("CSS.enable", null, attachment);

        var nodeId = await NodeIdAsync(session, attachment, "#box");
        var computed = (await session.ResultAsync("CSS.getComputedStyleForNode", $$"""{"nodeId":{{nodeId}}}""", attachment))
            .GetProperty("computedStyle")
            .EnumerateArray()
            .ToArray();

        computed.Should().NotBeEmpty();

        var display = computed.Single(p => p.GetProperty("name").GetString() == "display").GetProperty("value").GetString();
        display.Should().Be("inline");
        computed.Single(p => p.GetProperty("name").GetString() == "color").GetProperty("value").GetString()
            .Should().Be("rgba(1, 2, 3, 1)");

        // The same declaration window.getComputedStyle hands the page, so a front end and a script agree.
        var fromScript = (await session.EvaluateAsync(
            "getComputedStyle(document.getElementById('box')).display",
            attachment)).GetProperty("value").GetString();

        display.Should().Be(fromScript);

        var inline = (await session.ResultAsync("CSS.getInlineStylesForNode", $$"""{"nodeId":{{nodeId}}}""", attachment))
            .GetProperty("inlineStyle");

        inline.GetProperty("cssText").GetString().Should().Contain("display");
        inline.GetProperty("cssProperties").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .Should().Contain("display");

        await session.ResultAsync("CSS.disable", null, attachment);
    }

    [Test]
    public async Task EditingAStyleSheetStaysAbsent()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        foreach (var method in (string[]) ["CSS.getMatchedStylesForNode", "CSS.setStyleTexts", "CSS.addRule", "CSS.createStyleSheet"])
        {
            var error = await session.ErrorAsync(method, "{}", attachment);
            error.GetProperty("code").GetInt32().Should().Be(-32601);
        }
    }

    [Test]
    public async Task PerformanceReportsOnlyTheCountersThatMeanSomethingHere()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session, "<html><body><p>one</p><p>two</p></body></html>");

        await session.ResultAsync("Performance.enable", null, attachment);

        var metrics = (await session.ResultAsync("Performance.getMetrics", null, attachment))
            .GetProperty("metrics")
            .EnumerateArray()
            .ToDictionary(m => m.GetProperty("name").GetString()!, m => m.GetProperty("value").GetDouble());

        metrics.Should().ContainKey("Timestamp");
        metrics["Timestamp"].Should().BeGreaterThan(0);
        metrics.Should().ContainKey("Documents");
        metrics["Documents"].Should().Be(1);

        // Every node of the document, which is a number the page itself can count.
        var counted = (await session.EvaluateAsync(
            "document.getElementsByTagName('*').length",
            attachment)).GetProperty("value").GetDouble();

        metrics.Should().ContainKey("Nodes");
        metrics["Nodes"].Should().BeGreaterThan(counted, "the count is every node, not only the elements");

        metrics.Should().NotContainKey("JSHeapUsedSize", "there is no JavaScript heap to size, and Runtime.getHeapUsage says so at length");
        metrics.Should().NotContainKey("LayoutCount", "nothing is laid out, and a zero would read as a page that laid out instantly");
    }

    [Test]
    public async Task InterceptingAFileChooserIsAcceptedAndNoChooserIsEverOpened()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync("Page.setInterceptFileChooserDialog", """{"enabled":true}""", attachment);
        await session.ResultAsync("Page.setInterceptFileChooserDialog", """{"enabled":false}""", attachment);

        session.EventsOf("Page.fileChooserOpened", attachment).Should().BeEmpty();
    }

    private static async Task<string> OpenAsync(PageSession session, string html)
    {
        var attachment = await session.OpenPageAsync().ConfigureAwait(false);
        await session.EnablePageAsync(attachment).ConfigureAwait(false);
        await session.ResultAsync("DOM.enable", null, attachment).ConfigureAwait(false);

        var tree = await session.ResultAsync("Page.getFrameTree", null, attachment).ConfigureAwait(false);
        var frameId = tree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString()!;

        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["frameId"] = frameId,
            ["html"] = html,
        });

        await session.ResultAsync("Page.setDocumentContent", payload, attachment).ConfigureAwait(false);
        return attachment;
    }

    private static async Task<int> NodeIdAsync(PageSession session, string attachment, string selector)
    {
        var document = await session.ResultAsync("DOM.getDocument", """{"depth":0}""", attachment).ConfigureAwait(false);
        var rootId = document.GetProperty("root").GetProperty("nodeId").GetInt32();

        var found = await session.ResultAsync(
            "DOM.querySelector",
            $$"""{"nodeId":{{rootId}},"selector":"{{selector}}"}""",
            attachment).ConfigureAwait(false);

        return found.GetProperty("nodeId").GetInt32();
    }
}
