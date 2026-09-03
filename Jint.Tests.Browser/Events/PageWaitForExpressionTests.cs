namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>Page.WaitForAsync(expression, timeout)</c>: the general form of the selector and text waits.
/// </summary>
/// <remarks>
/// It exists for the conditions neither of those can state — a URL a router moved, a list that reached a
/// length, a flag a framework set — and the obstacle course waits on exactly those. What these hold is the
/// two things the other two waits have no equivalent of: that a chain of timers and microtasks is seen at
/// all, and that an expression which throws is <i>not yet true</i> rather than a failure.
/// </remarks>
public sealed class PageWaitForExpressionTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Test]
    public async Task ItSeesAConditionAChainOfTimersMakesTrue()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <p id="state">waiting</p>
            <script>
              // Two chained timers with a microtask between them, so nothing but a page that is really
              // running gets there.
              setTimeout(() => {
                Promise.resolve().then(() => setTimeout(() => {
                  document.getElementById('state').textContent = 'ready';
                }, 30));
              }, 30);
            </script>
            """);

        (await page.WaitForAsync("document.getElementById('state').textContent === 'ready'", Patience))
            .Should().BeTrue();

        (await page.EvaluateAsync<string>("document.getElementById('state').textContent")).Should().Be("ready");
    }

    [Test]
    public async Task AConditionThatNeverHoldsIsATimeoutRatherThanAHang()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p id='state'>waiting</p>");

        (await page.WaitForAsync("document.getElementById('state').textContent === 'ready'", TimeSpan.FromMilliseconds(200)))
            .Should().BeFalse();
    }

    [Test]
    public async Task AnExpressionThatThrowsIsNotYetTrueAndSaysSoAtTheCeiling()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="root"></div>
            <script>
              setTimeout(() => {
                document.getElementById('root').innerHTML = '<span id="late">here</span>';
              }, 30);
            </script>
            """);

        // #late does not exist yet, so the condition throws until the timer has run.
        (await page.WaitForAsync("document.getElementById('late').textContent === 'here'", Patience))
            .Should().BeTrue();

        var typo = async () => await page.WaitForAsync("thisIsNotDefined()", TimeSpan.FromMilliseconds(200));

        await typo.Should().ThrowAsync<Exception>("a wait that never came true because of a typo says why");
    }

    [Test]
    public async Task ItRefusesAClosedPage()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p>done</p>");
        await page.CloseAsync();

        var wait = async () => await page.WaitForAsync("true", Patience);

        await wait.Should().ThrowAsync<ObjectDisposedException>();
    }
}
