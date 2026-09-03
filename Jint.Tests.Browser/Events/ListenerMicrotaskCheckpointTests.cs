namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The microtask checkpoint a callback returns to, on a page —
/// https://html.spec.whatwg.org/multipage/webappapis.html#clean-up-after-running-script.
/// </summary>
/// <remarks>
/// A page is where the distinction is visible from both sides at once. A click a protocol client drives
/// arrives from the page loop with nothing on the JavaScript execution context stack, so a promise reaction
/// the first listener queued runs before the second listener starts; <c>el.click()</c> from a script has
/// that script on the stack, so it does not. <c>Jint.Tests.Runtime.WebApi.ListenerMicrotaskCheckpointTests</c>
/// is the engine-level statement of the same rule.
/// </remarks>
public sealed class ListenerMicrotaskCheckpointTests
{
    private const string TwoListeners = """
        <!doctype html>
        <button id="go">go</button>
        <script>
          window.log = [];
          const go = document.getElementById('go');
          go.addEventListener('click', () => {
            log.push('first');
            Promise.resolve().then(() => log.push('microtask'));
          });
          go.addEventListener('click', () => log.push('second'));
        </script>
        """;

    [Test]
    public async Task AClickTheClientDrivesCheckpointsBetweenTwoListeners()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(TwoListeners);

        (await page.ClickAsync("#go")).Should().BeTrue();

        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("first|microtask|second");
    }

    [Test]
    public async Task AClickAScriptMakesIsNotCheckpointed()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(TwoListeners);

        await page.EvaluateAsync("document.getElementById('go').click()");

        // The script that called click() was on the stack, so both listeners ran before the reaction.
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("first|second|microtask");
    }

    /// <summary>
    /// The <c>load</c> event the parse ends with is fired from the page loop, so it is checkpointed too.
    /// </summary>
    [Test]
    public async Task TheLoadEventTheParseFiresIsCheckpointed()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("""
            <!doctype html>
            <script>
              window.log = [];
              window.addEventListener('load', () => {
                log.push('first');
                Promise.resolve().then(() => log.push('microtask'));
              });
              window.addEventListener('load', () => log.push('second'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("first|microtask|second");
    }

    /// <summary>
    /// HTML's <i>run the animation frame callbacks</i> invokes each callback the way it invokes a listener, so
    /// the whole batch being one job does not make the frame one uninterrupted turn of script.
    /// </summary>
    [Test]
    public async Task EachAnimationFrameCallbackReturnsToACheckpoint()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("""
            <!doctype html>
            <script>
              window.log = [];
              requestAnimationFrame(() => {
                log.push('first');
                Promise.resolve().then(() => log.push('microtask'));
              });
              requestAnimationFrame(() => log.push('second'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("first|microtask|second");
    }
}
