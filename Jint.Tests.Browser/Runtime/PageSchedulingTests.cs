using Jint.Browser;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The page loop as a host loop: timers and animation frames run because the page's thread pumps the engine,
/// and what a callback got wrong is recorded rather than fatal.
/// </summary>
public sealed class PageSchedulingTests
{
    [Test]
    public async Task ATimerFiresWhenThePageIsPumped()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script>window.fired = false; setTimeout(() => { window.fired = true }, 5)</script>");

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<bool>("window.fired")).Should().BeTrue();
    }

    [Test]
    public async Task AnAnimationFrameFiresWithATimestamp()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.stamp = null;
              window.handle = requestAnimationFrame(t => { window.stamp = t });
            </script>
            """);

        (await page.EvaluateAsync<double>("window.handle")).Should().BeGreaterThan(0);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await page.EvaluateAsync<string>("typeof window.stamp")).Should().Be("number");
        (await page.EvaluateAsync<double>("window.stamp")).Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task ACancelledAnimationFrameDoesNotFire()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.hits = 0;
              const kept = requestAnimationFrame(() => { window.hits++ });
              const dropped = requestAnimationFrame(() => { window.hits += 10 });
              cancelAnimationFrame(dropped);
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<int>("window.hits")).Should().Be(1);
    }

    [Test]
    public async Task AFrameCancelledFromInsideTheFrameDoesNotRun()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.ran = [];
              let second;
              requestAnimationFrame(() => { window.ran.push('first'); cancelAnimationFrame(second) });
              second = requestAnimationFrame(() => window.ran.push('second'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        // The two were requested for the same frame, and the first cancels the second while that frame is
        // running: the specification skips a cancelled id even then.
        (await page.EvaluateAsync<string>("window.ran.join(',')")).Should().Be("first");
    }

    [Test]
    public async Task AnUncaughtErrorInATimerIsRecordedAndThePageSurvives()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            "<script>setTimeout(() => { throw new Error('boom') }, 1); setTimeout(() => { window.after = 'ran' }, 2)</script>");

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        page.Errors.Should().ContainSingle();
        page.Errors[0].Kind.Should().Be(PageErrorKind.UncaughtCallbackError);
        page.Errors[0].Message.Should().Be("Error: boom");
        page.Errors[0].Source.Should().Be("Timer");

        (await page.EvaluateAsync<string>("window.after")).Should().Be("ran");
        (await page.EvaluateAsync<int>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task AnErrorInAnInlineScriptEndsTheScriptAndNotTheParse()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            "<script>throw new Error('early')</script><p id='after'>parsed</p><script>window.later = 'ran'</script>");

        page.Errors.Should().ContainSingle();
        page.Errors[0].Kind.Should().Be(PageErrorKind.ScriptError);
        page.Errors[0].Message.Should().Be("Error: early");

        (await page.EvaluateAsync<string>("document.getElementById('after').textContent")).Should().Be("parsed");
        (await page.EvaluateAsync<string>("window.later")).Should().Be("ran");
    }

    [Test]
    public async Task AnUnhandledRejectionIsRecorded()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script>Promise.reject(new Error('nobody caught this'))</script>");

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        page.Errors.Should().ContainSingle();
        page.Errors[0].Kind.Should().Be(PageErrorKind.UnhandledPromiseRejection);
        page.Errors[0].Message.Should().Be("Error: nobody caught this");
    }

    [Test]
    public async Task ConsoleOutputReachesThePage()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script>console.log('hello', 42); console.warn('careful')</script>");

        page.ConsoleMessages.Should().Equal("hello 42", "careful");
    }

    [Test]
    public async Task APageWithNothingToDoGoesIdle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
    }

    [Test]
    public async Task APageWithARepeatingTimerNeverGoesIdle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script>window.ticks = 0; setInterval(() => { window.ticks++ }, 1)</script>");

        (await page.WaitForIdleAsync(TimeSpan.FromMilliseconds(200))).Should().BeFalse();
        (await page.EvaluateAsync<int>("window.ticks")).Should().BeGreaterThan(0);
    }

    [Test]
    public async Task TheRecordingsAreBounded()
    {
        var options = new BrowserOptions { MaxRecordedEvents = 3 };
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script>for (let i = 0; i < 10; i++) console.log('line ' + i)</script>");

        page.ConsoleMessages.Should().Equal("line 7", "line 8", "line 9");
    }
}
