using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssClipPathTests
{
    [Test]
    public async Task NamedGenericAndComputedRoutesShareValidatedClipPaths()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<svg><g id=p style='clip-path:url(#p)'><path id=t></path></g></svg>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const el=document.getElementById('t'), s=el.style, computed=getComputedStyle(el);
              if (computed.clipPath!=='none') return false;
              s.clipPath='inherit';
              if (computed.clipPath!=='url("#p")') return false;
              s.clipPath='border-box'; s.clipPath='none border-box';
              if (s.getPropertyValue('clip-path')!=='border-box') return false;
              s.setProperty('clip-path','url(#clip)','important');
              if (s.clipPath!=='url("#clip")' || s.getPropertyPriority('clip-path')!=='important') return false;
              s.setProperty('--clip','url(#a)'); s.clipPath='var(--clip)';
              if (computed.clipPath!=='url("#a")') return false;
              s.setProperty('--clip','none red');
              if (computed.clipPath!=='none') return false;
              if (!CSS.supports('clip-path','url(#p)') || CSS.supports('clip-path','url(#p) none')) return false;
              try { computed.clipPath='none'; return false; }
              catch(e) { if (e.name!=='NoModificationAllowedError') return false; }
              s.clipPath=null;
              return s.clipPath==='' && computed.clipPath==='none';
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ClipReferencesResolveAgainstTheirSourceWithoutFetching()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<base href='/inline/'><link rel=stylesheet href='/css/main.css'><svg><path id=t></path></svg>")
            .Map("/css/main.css", _ => LoopbackResponse.Css("#t{clip-path:src('clips.svg#p')}")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).clipPath"))
            .Should().Be("src(\"" + fixture.Url("/css/clips.svg#p") + "\")");
        await fixture.Page.EvaluateAsync("document.getElementById('t').style.clipPath='url(clips.svg#q)'");
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).clipPath"))
            .Should().Be("url(\"" + fixture.Url("/inline/clips.svg#q") + "\")");
        await fixture.Page.EvaluateAsync("document.querySelector('base').href='/changed/'");
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).clipPath"))
            .Should().Be("url(\"" + fixture.Url("/changed/clips.svg#q") + "\")");
        fixture.Server.Received.Any(request => request.Path.Contains("clips.svg", StringComparison.Ordinal)).Should().BeFalse();
        fixture.Page.Errors.Should().BeEmpty();
    }
}
