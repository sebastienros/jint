namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssClipTests
{
    [Test]
    public async Task NamedGenericRuleAndLiveComputedRectanglesAgree()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>html{font-size:10px}#box{clip:rect(1em auto -2rem 0)}</style>" +
            "<div id=box style='font-size:20px'></div>");
        (await page.EvaluateAsync<string>("""
            var box=document.getElementById('box'), computed=getComputedStyle(box);
            var rule=document.styleSheets[0].cssRules[1].style;
            [rule.clip, computed.clip, computed.getPropertyValue('clip')].join('|')
            """)).Should().Be("rect(1em, auto, -2rem, 0px)|rect(20px, auto, -20px, 0px)|rect(20px, auto, -20px, 0px)");
        (await page.EvaluateAsync<string>("""
            box.style.clip='rect(0,0,0,0)';
            box.style.setProperty('clip','rect(auto, -2em, calc(1rem + 1em), 0)','important');
            [box.style.clip, box.style.getPropertyPriority('clip'), computed.clip].join('|')
            """)).Should().Be("rect(auto, -2em, calc(1em + 1rem), 0px)|important|rect(auto, -40px, 30px, 0px)");
        (await page.EvaluateAsync<string>("""
            box.style.fontSize='30px'; document.documentElement.style.fontSize='12px';
            computed.clip
            """)).Should().Be("rect(auto, -60px, 42px, 0px)");
        (await page.EvaluateAsync<string>("""
            box.style.clip=null;
            rule.clip='rect(-1em, auto, 2rem, 0)';
            computed.clip
            """)).Should().Be("rect(-30px, auto, 24px, 0px)");
        (await page.EvaluateAsync<string>("""
            document.styleSheets[0].deleteRule(1);
            computed.clip
            """)).Should().Be("auto");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SubstitutionsInheritanceAndResetInvalidateWarmRectangles()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=parent style='font-size:10px;clip:rect(1em auto -2em 0)'>" +
            "<div id=box style='font-size:20px'></div></div>");
        (await page.EvaluateAsync<string>("""
            var box=document.getElementById('box'), computed=getComputedStyle(box);
            var values=[computed.clip];
            box.style.clip='inherit'; values.push(computed.clip);
            box.style.clip='unset'; values.push(computed.clip);
            box.style.setProperty('--edges','2em, auto, -3px, 0');
            box.style.clip='rect(var(--edges))'; values.push(computed.clip);
            box.style.setProperty('--edges','2em, 10%, 0, 0'); values.push(computed.clip);
            box.style.setProperty('--edges','0 0 0 0'); values.push(computed.clip);
            box.style.all='initial'; values.push(computed.clip);
            values.join('|')
            """)).Should().Be("auto|rect(10px, auto, -20px, 0px)|auto|rect(40px, auto, -3px, 0px)|auto|rect(0px, 0px, 0px, 0px)|auto");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AccessorsSupportsAndAtomicInvalidWritesUseTheSameGrammar()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box{}</style><div id=box></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const box=document.getElementById('box'), computed=getComputedStyle(box);
              const d=Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype,'clip');
              if (!d.enumerable || !d.configurable || d.get.length!==0 || d.set.length!==1) return false;
              let conversions=0;
              try { d.set.call({}, {toString(){conversions++;return 'auto'}}); return false; }
              catch(e) { if (!(e instanceof TypeError) || conversions!==0) return false; }
              try { d.get.call({}); return false; }
              catch(e) { if (!(e instanceof TypeError)) return false; }
              try { computed.clip='auto'; return false; }
              catch(e) { if (e.name!=='NoModificationAllowedError') return false; }
              try { computed.setProperty('clip','auto'); return false; }
              catch(e) { if (e.name!=='NoModificationAllowedError') return false; }
              for (const style of [box.style,document.styleSheets[0].cssRules[0].style]) {
                style.setProperty('clip','rect(0 0 0 0)','important');
                const before=style.cssText;
                for (const invalid of ['rect(1,0,0,0)','rect(0%,0,0,0)','rect(0,0 0,0)','rect(0,0,0)','none']) {
                  style.clip=invalid;
                  style.setProperty('clip',invalid);
                  if (style.cssText!==before || CSS.supports('clip',invalid)) return false;
                }
                style.clip=undefined;
                if (style.cssText!==before || Object.hasOwn(style,'clip')) return false;
                style.clip=null;
                if (style.clip!=='' || style.getPropertyPriority('clip')!=='') return false;
              }
              return CSS.supports('clip','rect(0,0,0,0)') &&
                CSS.supports('clip','rect(auto -1em 2rem 0)') &&
                CSS.supports('clip:rect(0,0,0,0)') && CSS.supports('clip','var(--clip)');
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}
