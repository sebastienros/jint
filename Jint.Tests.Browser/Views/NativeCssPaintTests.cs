using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssPaintTests
{
    [Test]
    public async Task PaintUsesLiveColorAndInheritanceWithoutOverwritingSpecifiedValues()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <svg><g id="parent" style="fill:currentcolor;stroke:url(#p) currentcolor;color:red">
              <path id="child" style="color:blue"></path>
            </g><path id="initial"></path></svg>
            """);
        const string read = """
            (() => {
              const p = getComputedStyle(document.getElementById('parent'));
              const c = getComputedStyle(document.getElementById('child'));
              const i = getComputedStyle(document.getElementById('initial'));
              return [p.fill,p.stroke,c.fill,c.stroke,i.fill,i.stroke].join('|');
            })()
            """;
        (await page.EvaluateAsync<string>(read)).Should().Be(
            "rgb(255, 0, 0)|url(\"#p\") rgb(255, 0, 0)|rgb(0, 0, 255)|url(\"#p\") rgb(0, 0, 255)|rgb(0, 0, 0)|none");
        await page.EvaluateAsync("document.getElementById('child').style.color='green'");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).stroke"))
            .Should().Be("url(\"#p\") rgb(0, 128, 0)");
        (await page.EvaluateAsync<string>("document.getElementById('parent').style.fill")).Should().Be("currentcolor");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NamedGenericAndSupportsRoutesSharePaintDeclarations()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<svg><path id=t></path></svg>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const el=document.getElementById('t'), s=el.style;
              s.fill='red';
              s.fill='none blue';
              if (s.getPropertyValue('fill')!=='red') return false;
              s.setProperty('stroke','context-stroke','important');
              if (s.stroke!=='context-stroke' || s.getPropertyPriority('stroke')!=='important') return false;
              s.setProperty('--paint','blue'); s.fill='var(--paint)';
              if (getComputedStyle(el).fill!=='rgb(0, 0, 255)') return false;
              s.setProperty('--paint','none red');
              if (getComputedStyle(el).fill!=='rgb(0, 0, 0)') return false;
              if (!CSS.supports('fill','url(#p) red') || CSS.supports('stroke','url(#p) context-fill')) return false;
              try { getComputedStyle(el).fill='blue'; return false; }
              catch (e) { if (e.name!=='NoModificationAllowedError') return false; }
              return s.removeProperty('stroke')==='context-stroke' && s.stroke==='';
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task UrlsUseTheDeclaringSheetAndRemainStableThroughInheritance()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <base href="/inline/"><link rel=stylesheet href="/css/main.css">
                <svg><g id=parent><path id=child></path></g></svg>
                """)
            .Map("/css/main.css", _ => LoopbackResponse.Css("@import 'nested/child.css';"))
            .Map("/css/nested/child.css", _ => LoopbackResponse.Css("#parent{fill:url(paint.svg#p) red}")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).fill"))
            .Should().Be("url(\"" + fixture.Url("/css/nested/paint.svg#p") + "\") rgb(255, 0, 0)");
        await fixture.Page.EvaluateAsync("document.getElementById('child').style.fill='url(paint.svg#p) none'");
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).fill"))
            .Should().Be("url(\"" + fixture.Url("/inline/paint.svg#p") + "\") none");
        await fixture.Page.EvaluateAsync("document.querySelector('base').href='/changed/'");
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).fill"))
            .Should().Be("url(\"" + fixture.Url("/changed/paint.svg#p") + "\") none");
        fixture.Server.Received.Any(request => request.Path.Contains("paint.svg", StringComparison.Ordinal)).Should().BeFalse();
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("url(#p) none", "url(\"#p\") none")]
    [TestCase("url('')", "url(\"\")")]
    [TestCase("src('')", "src(\"\")")]
    [TestCase("url('http://[invalid')", "url(\"http://[invalid\")")]
    public async Task LocalEmptyAndUnresolvableUrlsKeepTheirCssValues(string input, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<svg><path id=t></path></svg>");
        await page.EvaluateAsync("document.getElementById('t').style.fill=" + System.Text.Json.JsonSerializer.Serialize(input));
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).fill")).Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }
}
