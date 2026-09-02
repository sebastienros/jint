namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>getComputedStyle</c>: AngleSharp.Css's cascade, read-only, with no layout behind it.
/// </summary>
public sealed class ComputedStyleTests
{
    [Test]
    public async Task AStyleElementRuleAndAnInlineStyleBothReachTheComputedStyle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <style>
              .tinted { color: rgb(0, 128, 0) }
              #by-id { font-weight: bold }
            </style>
            <p id="by-id" class="tinted" style="text-align: center">text</p>
            """);

        var computed = "getComputedStyle(document.getElementById('by-id'))";

        (await page.EvaluateAsync<string>(computed + ".getPropertyValue('font-weight')")).Should().Be("bold");
        (await page.EvaluateAsync<string>(computed + ".getPropertyValue('text-align')")).Should().Be("center");
        (await page.EvaluateAsync<string>(computed + ".color")).Should().Contain("0, 128, 0");
        (await page.EvaluateAsync<bool>(computed + " instanceof CSSStyleDeclaration")).Should().BeTrue();
    }

    [Test]
    public async Task DisplayComesFromTheUserAgentStylesheetWithoutAnyLayout()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='block'>a</div><span id='inline'>b</span>");

        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('block')).display")).Should().Be("block");

        // A <span> answers the empty string where a browser answers "inline": AngleSharp's cascade reports
        // the values something declared, and `display: inline` is CSS's initial value, so its user-agent
        // stylesheet does not declare it. A declared one — the <div>'s — resolves. Recorded as a divergence
        // in Jint.Browser/AGENTS.md rather than papered over with an initial-value table of our own.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('inline')).display")).Should().BeEmpty();
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('inline')).getPropertyValue('display')")).Should().BeEmpty();

        // Width and height are used values, and a used value needs a box. There is none, so they stay empty
        // rather than pretending to a number.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('block')).getPropertyValue('width')")).Should().BeEmpty();
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('block')).getPropertyValue('height')")).Should().BeEmpty();
    }

    [Test]
    public async Task TheComputedStyleRefusesEveryWrite()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p id='p' style='color: rgb(1, 2, 3)'>text</p>");

        var refusal =
            """
            (member => {
              const style = getComputedStyle(document.getElementById('p'));
              try {
                if (member === 'setProperty') { style.setProperty('color', 'blue') }
                else if (member === 'removeProperty') { style.removeProperty('color') }
                else if (member === 'cssText') { style.cssText = 'color: blue' }
                else { style.color = 'blue' }
                return 'no throw';
              } catch (e) { return e.name }
            })
            """;

        foreach (var member in new[] { "setProperty", "removeProperty", "cssText", "color" })
        {
            (await page.EvaluateAsync<string>("(" + refusal + ")('" + member + "')"))
                .Should().Be("NoModificationAllowedError", "writing {0} on a computed style is refused", member);
        }

        // And the element's own style is untouched, which is the point of refusing rather than accepting a
        // write into a detached copy.
        (await page.EvaluateAsync<string>("document.getElementById('p').style.color")).Should().Contain("1, 2, 3");
    }

    [Test]
    public async Task TheInlineStyleIsStillWritable()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p id='p'>text</p>");

        await page.EvaluateAsync("document.getElementById('p').style.setProperty('font-weight', 'bold')");

        (await page.EvaluateAsync<string>("document.getElementById('p').getAttribute('style')")).Should().Contain("font-weight");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p')).getPropertyValue('font-weight')")).Should().Be("bold");
    }

    [Test]
    public async Task ThePseudoElementArgumentIsAcceptedAndIgnored()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<style>#p::before { content: 'x' }</style><p id='p' style='color: rgb(9, 9, 9)'>text</p>");

        // Documented divergence: the pseudo-element selector is ignored, so the answer is the element's own
        // computed style rather than the pseudo-element's.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p'), '::before').color"))
            .Should().Contain("9, 9, 9");
    }

    [Test]
    public async Task GetComputedStyleNeedsAnElement()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>(
            "(() => { try { getComputedStyle({}); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError");
    }
}
