namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssBorderTests
{
    [Test]
    public async Task NamedGenericAndStylesheetAccessorsShareAllBorderAndOutlineStorage()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box{}</style><div id=box></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const box=document.getElementById('box'), computed=getComputedStyle(box);
              const names=['border','border-width','border-style','border-color','border-radius',
                'outline','outline-width','outline-style','outline-color','outline-offset'];
              for (const side of ['top','right','bottom','left','block-start','block-end','inline-start','inline-end']) {
                names.push('border-'+side);
                for (const kind of ['width','style','color']) names.push('border-'+side+'-'+kind);
              }
              for (const axis of ['block','inline']) {
                names.push('border-'+axis);
                for (const kind of ['width','style','color']) names.push('border-'+axis+'-'+kind);
              }
              for (const corner of ['top-left','top-right','bottom-left','bottom-right','start-start','start-end','end-start','end-end'])
                names.push('border-'+corner+'-radius');
              for (const name of names) {
                const camel=name.replace(/-([a-z])/g,(_,c)=>c.toUpperCase());
                const d=Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype,camel);
                if (!d || !d.enumerable || !d.configurable || d.get.length!==0 || d.set.length!==1) return false;
                let conversions=0;
                try { d.set.call({}, {toString(){conversions++;return 'initial'}}); return false; }
                catch(e) { if (!(e instanceof TypeError) || conversions!==0) return false; }
                try { d.get.call({}); return false; }
                catch(e) { if (!(e instanceof TypeError)) return false; }
                try { computed[camel]='initial'; return false; }
                catch(e) { if (e.name!=='NoModificationAllowedError') return false; }
                try { computed.setProperty(name,'initial'); return false; }
                catch(e) { if (e.name!=='NoModificationAllowedError') return false; }
                for (const style of [box.style,document.styleSheets[0].cssRules[0].style]) {
                  style.setProperty(name,'initial','important');
                  if (style[camel]!=='initial' || style.getPropertyPriority(name)!=='important') return false;
                  const before=style.cssText;
                  style[camel]='not-a-valid-border';
                  style[camel]=undefined;
                  if (style.cssText!==before || Object.hasOwn(style,camel)) return false;
                  style[camel]=null;
                  if (style[camel]!=='' || style.getPropertyPriority(name)!=='') return false;
                  style[camel]='var(--value)';
                  if (style[camel]!==style.getPropertyValue(name)) return false;
                  style[camel]=null;
                }
                if (!CSS.supports(name,'initial') || !CSS.supports(name,'var(--value)') ||
                  CSS.supports(name,'not-a-valid-border')) return false;
              }
              return CSS.supports('border','2px solid red') && CSS.supports('border-radius','2em / 50%') &&
                CSS.supports('outline','auto') && !CSS.supports('outline-style','hidden') &&
                !CSS.supports('border-width','10%');
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task WarmComputedValuesFollowFontDirectionRulesAndSubstitution()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>html{font-size:10px}#box{border:1em solid currentcolor;" +
            "border-radius:1rem / 20%;outline:auto}</style><div id=box style='font-size:20px;color:red'></div>");
        (await page.EvaluateAsync<string>("""
            var box=document.getElementById('box'), computed=getComputedStyle(box), sheet=document.styleSheets[0];
            [computed.border,computed.borderRadius,computed.outline].join('|')
            """)).Should().Be("20px solid rgb(255, 0, 0)|10px / 20%|3px auto auto");
        (await page.EvaluateAsync<string>("""
            box.style.fontSize='30px'; box.style.color='blue'; document.documentElement.style.fontSize='12px';
            box.style.borderInlineStart='2em dashed red'; box.style.direction='rtl';
            [computed.borderLeft,computed.borderRight,computed.borderInlineStart,computed.borderRadius].join('|')
            """)).Should().Be("30px solid rgb(0, 0, 255)|60px dashed rgb(255, 0, 0)|60px dashed rgb(255, 0, 0)|12px / 20%");
        (await page.EvaluateAsync<string>("""
            box.style.writingMode='vertical-rl';
            [computed.borderRight,computed.borderBottom,computed.borderInlineStart].join('|')
            """)).Should().Be("30px solid rgb(0, 0, 255)|60px dashed rgb(255, 0, 0)|60px dashed rgb(255, 0, 0)");
        (await page.EvaluateAsync<string>("""
            box.style.borderInlineStart=null;
            box.style.setProperty('--b','2px dotted green'); box.style.border='var(--b)';
            var first=computed.border;
            box.style.setProperty('--b','2px solid blue bad');
            [first,computed.borderWidth,computed.borderStyle].join('|')
            """)).Should().Be("2px dotted rgb(0, 128, 0)|0px|none");
        (await page.EvaluateAsync<string>("""
            box.style.border=null;
            sheet.cssRules[1].style.border='3px double red';
            var first=computed.border;
            sheet.deleteRule(1);
            [first,computed.borderWidth,computed.borderRadius].join('|')
            """)).Should().Be("3px double rgb(255, 0, 0)|0px|0px");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task LogicalPhysicalOrderSurvivesStyleSerializationAndLayerRollback()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@layer base,theme;" +
            "@layer base{#box{border-left:1px solid red}}@layer theme{#box{border-inline-start:revert-layer}}</style>" +
            "<div id=box></div>");
        (await page.EvaluateAsync<string>("""
            var box=document.getElementById('box'), computed=getComputedStyle(box);
            computed.borderLeft
            """)).Should().Be("1px solid rgb(255, 0, 0)");
        (await page.EvaluateAsync<string>("""
            box.style.cssText='border-left:2px solid green;border-inline-start:3px dashed blue';
            const before=computed.borderLeft;
            box.style.cssText=box.style.cssText;
            const after=computed.borderLeft;
            box.style.borderLeft='4px dotted red';
            [before,after,computed.borderLeft].join('|')
            """)).Should().Be("3px dashed rgb(0, 0, 255)|3px dashed rgb(0, 0, 255)|4px dotted rgb(255, 0, 0)");
        (await page.EvaluateAsync<string>("""
            box.style.all='initial';
            [computed.borderWidth,computed.borderRadius,computed.outlineWidth,computed.outlineOffset].join('|')
            """)).Should().Be("0px|0px|0px|0px");
        page.Errors.Should().BeEmpty();
    }
}
