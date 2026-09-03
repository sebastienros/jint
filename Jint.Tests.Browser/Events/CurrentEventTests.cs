namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>window.event</c> — https://dom.spec.whatwg.org/#window-current-event, the legacy global that is the
/// event whose listener is running.
/// </summary>
/// <remarks>
/// The engine keeps the slot (it is <i>inner invoke</i> that sets and restores it) and the page installs the
/// property that reads it. What is checked here is the page's half and the two properties the web-platform
/// corpus does not pin from a page: that the property is an <b>own</b> property of the global, as WebIDL's
/// <c>[Global]</c> requires, and that a listener dispatching an event of its own gets its own event back
/// afterwards rather than the nested one.
/// </remarks>
public sealed class CurrentEventTests
{
    [Test]
    public async Task TheCurrentEventIsAnOwnPropertyOfTheGlobalAndIsUndefinedOutsideADispatch()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='d'></div>");

        (await page.EvaluateAsync<string>(
            """
            [
              Object.prototype.hasOwnProperty.call(window, 'event'),
              window.event === undefined,
              Object.getOwnPropertyDescriptor(Window.prototype, 'event') === undefined
            ].join(',')
            """))
            .Should().Be("true,true,true");
    }

    /// <summary>
    /// The slot is saved and restored per listener invocation, so a listener that dispatches an event of its
    /// own — at another target, and re-entrantly at the same one — finds its own event again when that
    /// returns.
    /// </summary>
    [Test]
    public async Task ANestedDispatchRestoresTheOuterEventWhenItReturns()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='outer'><span id='inner'></span></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const outer = document.getElementById('outer');
              const inner = document.getElementById('inner');
              const seen = [];

              const outerEvent = new Event('outer', { bubbles: true });
              const innerEvent = new Event('inner');

              inner.addEventListener('inner', () => seen.push('inner:' + (window.event === innerEvent)));
              document.addEventListener('outer', () => {
                seen.push('document:' + (window.event === outerEvent));
                inner.dispatchEvent(innerEvent);
                seen.push('after:' + (window.event === outerEvent));
              });

              outer.dispatchEvent(outerEvent);
              seen.push('done:' + (window.event === undefined));
              return seen.join(',');
            })()
            """))
            .Should().Be("document:true,inner:true,after:true,done:true");
    }

    /// <summary>
    /// A listener that throws still leaves the slot as it found it, which is what stops one failing handler
    /// from making <c>window.event</c> lie for the rest of the turn.
    /// </summary>
    [Test]
    public async Task AThrowingListenerRestoresTheSlot()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='d'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const d = document.getElementById('d');
              d.addEventListener('ping', () => { throw new Error('boom'); });
              d.dispatchEvent(new Event('ping'));
              return String(window.event === undefined);
            })()
            """))
            .Should().Be("true");
    }
}
