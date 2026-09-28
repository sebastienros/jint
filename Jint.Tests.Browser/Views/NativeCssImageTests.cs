using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssImageTests
{
    [Test]
    public async Task ImageLayersShareNamedGenericAndComputedRoutes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=t></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const el=document.getElementById('t'), s=el.style, c=getComputedStyle(el);
              if (c.backgroundImage!=='none') return false;
              s.backgroundImage='none, url(#p)';
              if (c.backgroundImage!=='none, url("#p")') return false;
              s.backgroundImage='red';
              if (s.getPropertyValue('background-image')!=='none, url("#p")') return false;
              s.setProperty('--images','url(#q),none'); s.backgroundImage='var(--images)';
              if (c.backgroundImage!=='url("#q"), none') return false;
              s.setProperty('--images','red');
              if (c.backgroundImage!=='none') return false;
              if (!CSS.supports('background-image','none,url(#a)') || CSS.supports('background-image','url(a) none')) return false;
              try { c.backgroundImage='none'; return false; }
              catch(e) { if (e.name!=='NoModificationAllowedError') return false; }
              s.backgroundImage=null;
              return s.backgroundImage==='' && c.backgroundImage==='none';
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task EachImageUsesItsDeclaringSheetWithoutFetching()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<base href='/inline/'><link rel=stylesheet href='/css/main.css'><div id=t></div>")
            .Map("/css/main.css", _ => LoopbackResponse.Css("#t{background-image:url(a.png),none,src('b.png')}")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).backgroundImage"))
            .Should().Be("url(\"" + fixture.Url("/css/a.png") + "\"), none, src(\"" + fixture.Url("/css/b.png") + "\")");
        await fixture.Page.EvaluateAsync("document.getElementById('t').style.backgroundImage='url(c.png)'");
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).backgroundImage"))
            .Should().Be("url(\"" + fixture.Url("/inline/c.png") + "\")");
        fixture.Server.Received.Any(request => request.Path.EndsWith(".png", StringComparison.Ordinal)).Should().BeFalse();
        fixture.Page.Errors.Should().BeEmpty();
    }
}
