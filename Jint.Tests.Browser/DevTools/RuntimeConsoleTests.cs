using Jint.Browser;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// A page's <c>console</c> calls reach an attached client as <c>Runtime.consoleAPICalled</c>, on every
/// document the page loads (#4589).
/// </summary>
/// <remarks>
/// A page's engine is built by the browser rather than by <c>UseDevTools</c>, so it never had the sink a
/// <c>TargetRuntime</c> binds to and every console call was silent on the protocol — which is what left
/// Playwright's <c>page.on('console')</c> unfired and <c>page.setContent</c> waiting forever.
/// </remarks>
public class RuntimeConsoleTests
{
    [Test]
    public async Task ConsoleCallsInTheCurrentDocumentAreReported()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        await session.EvaluateAsync("console.log('hello'); console.debug('tag'); 1", attachment);

        var log = await session.EventAsync("Runtime.consoleAPICalled", sessionId: attachment);
        log.GetProperty("type").GetString().Should().Be("log");
        log.GetProperty("args")[0].GetProperty("value").GetString().Should().Be("hello");

        // A bound target asks the engine for frames, which is where a front end draws the source anchor from.
        log.TryGetProperty("stackTrace", out var stack).Should().BeTrue("a client attached, so the sink asks for a stack trace");
        stack.GetProperty("callFrames").GetArrayLength().Should().BeGreaterThan(0);

        var debug = await session.EventAsync("Runtime.consoleAPICalled", index: 1, sessionId: attachment);
        debug.GetProperty("type").GetString().Should().Be("debug");
        debug.GetProperty("args")[0].GetProperty("value").GetString().Should().Be("tag");
    }

    [Test]
    public async Task ConsoleCallsInANavigatedDocumentAreReportedAndStillRecordedByThePage()
    {
        await using var session = await PageSession.CreateAsync();
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await session.ResultAsync(
            "Page.navigate",
            """{"url":"data:text/html,<p>x</p><script>console.error('from the parse')</script>"}""",
            attachment);

        var parsed = await session.EventAsync("Runtime.consoleAPICalled", sessionId: attachment);
        parsed.GetProperty("type").GetString().Should().Be("error");
        parsed.GetProperty("args")[0].GetProperty("value").GetString().Should().Be("from the parse");

        await session.EvaluateAsync("console.log('after the commit')", attachment);
        var evaluated = await session.EventAsync("Runtime.consoleAPICalled", index: 1, sessionId: attachment);
        evaluated.GetProperty("args")[0].GetProperty("value").GetString().Should().Be("after the commit");

        // The page's own record is what it was before a client could hear anything.
        page.ConsoleMessages.Should().Contain(["from the parse", "after the commit"]);
    }
}
