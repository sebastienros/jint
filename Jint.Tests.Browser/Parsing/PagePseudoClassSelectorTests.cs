#nullable enable

namespace Jint.Tests.Browser.Parsing;

using Browser = global::Jint.Browser.Browser;

/// <summary>The selector states whose answer the page corrects around AngleSharp's defaults.</summary>
public sealed class PagePseudoClassSelectorTests
{
    /// <summary>
    /// Selectors §13.1 limits <c>:enabled</c> to elements which have a disabled state. Links have no such
    /// state, with or without an <c>href</c>; form controls retain AngleSharp's enabled/disabled behavior.
    /// </summary>
    [Test]
    public async Task EnabledMatchesControlsButNeverLinks()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><head>
              <link id="link" rel="alternate" href="/next">
            </head><body>
              <a id="anchor"></a><a id="anchorHref" href="/next"></a>
              <map><area id="area"><area id="areaHref" href="/next"></map>
              <button id="button"></button><button id="disabledButton" disabled></button>
              <input id="input"><input id="disabledInput" disabled>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const links = ['anchor', 'anchorHref', 'area', 'areaHref', 'link']
                .map(id => id + ':' + document.getElementById(id).matches(':enabled')).join(',');
              const enabled = Array.from(document.querySelectorAll(':enabled'), element => element.id).join(',');
              const disabled = Array.from(document.querySelectorAll(':disabled'), element => element.id).join(',');
              return links + '|' + enabled + '|' + disabled;
            })()
            """)).Should().Be(
            "anchor:false,anchorHref:false,area:false,areaHref:false,link:false|button,input|disabledButton,disabledInput");
    }
}
