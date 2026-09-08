#nullable enable

namespace Jint.Tests.Browser.Parsing;

using Browser = global::Jint.Browser.Browser;

/// <summary>The selector states whose answer the page corrects around AngleSharp's defaults.</summary>
public sealed class PagePseudoClassSelectorTests
{
    /// <summary>
    /// Selectors §8.2 permits a user agent with no visited-history model to treat every hyperlink as
    /// unvisited. HTML defines those hyperlinks as <c>a</c> and <c>area</c> elements carrying an
    /// <c>href</c>, including the empty string; a <c>link</c> element is not one of them.
    /// </summary>
    [Test]
    public async Task LinkMatchesEveryHtmlHyperlinkAndVisitedMatchesNone()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><head>
              <link id="link" href="/next">
            </head><body>
              <div id="root">
                <a id="anchor"></a><a id="emptyAnchor" href=""></a><a id="anchorHref" href="/next"></a>
                <map><area id="area"><area id="emptyArea" href=""><area id="areaHref" href="/next"></map>
                <div id="ordinary" href="/next"></div>
              </div>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const states = ['anchor', 'emptyAnchor', 'anchorHref', 'area', 'emptyArea', 'areaHref', 'link', 'ordinary']
                .map(id => {
                  const element = document.getElementById(id);
                  return id + ':' + element.matches(':link') + ':' + element.matches(':visited');
                }).join(',');
              const links = Array.from(document.querySelectorAll('#root :link'), element => element.id).join(',');
              const visited = document.querySelectorAll('#root :visited').length;
              return states + '|' + links + '|' + visited;
            })()
            """)).Should().Be(
            "anchor:false:false,emptyAnchor:true:false,anchorHref:true:false,area:false:false," +
            "emptyArea:true:false,areaHref:true:false,link:false:false,ordinary:false:false|" +
            "emptyAnchor,anchorHref,emptyArea,areaHref|0");
    }

    /// <summary>The link state reads the current presence of <c>href</c>, rather than a parsed URL snapshot.</summary>
    [Test]
    public async Task LinkStateTracksHrefPresence()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<a id='first' href=''></a><a id='second'></a>");

        (await page.EvaluateAsync<string>("""
            (() => {
              const before = Array.from(document.querySelectorAll(':link'), element => element.id).join(',');
              first.removeAttribute('href');
              second.setAttribute('href', '');
              const after = Array.from(document.querySelectorAll(':link'), element => element.id).join(',');
              return before + '|' + after + '|' + second.matches(':link:visited');
            })()
            """)).Should().Be("first|second|false");
    }

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
