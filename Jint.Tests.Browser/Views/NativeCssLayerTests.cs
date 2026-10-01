namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssLayerTests
{
    [Test]
    public async Task LayerCssomUsesRealBrandsFrozenNamesAndLiveChildren()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@layer a,b; @layer a{div{display:none}} @layer b{div{display:block}}</style><div id=t></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const sheet=document.styleSheets[0], order=sheet.cssRules[0], layer=sheet.cssRules[1];
              if (!(order instanceof CSSLayerStatementRule) || !(order instanceof CSSRule) ||
                  order instanceof CSSGroupingRule || order.type!==0) return false;
              if (!Object.isFrozen(order.nameList) || order.nameList!==order.nameList ||
                  order.nameList.join('|')!=='a|b') return false;
              if (!(layer instanceof CSSLayerBlockRule) || !(layer instanceof CSSGroupingRule) ||
                  layer.name!=='a' || layer.type!==0) return false;
              if (Object.prototype.toString.call(layer)!=='[object CSSLayerBlockRule]') return false;
              if (layer.parentStyleSheet!==sheet || layer.cssRules[0].parentRule!==layer) return false;
              const t=document.getElementById('t'), c=getComputedStyle(t), children=layer.cssRules;
              if (c.display!=='block') return false;
              if (layer.insertRule.length!==1 || layer.insertRule('@layer inserted {}')!==0) return false;
              if (children[0].name!=='inserted') return false;
              layer.deleteRule(0);
              layer.insertRule('div{display:flex!important}', 1);
              if (children.length!==2 || c.display!=='flex') return false;
              layer.deleteRule(1);
              if (c.display!=='block') return false;
              sheet.deleteRule(0);
              sheet.insertRule('@layer b,a;',0);
              if (c.display!=='none') return false;
              sheet.deleteRule(1);
              if (layer.parentStyleSheet!==null || layer.parentRule!==null || c.display!=='block') return false;
              layer.insertRule('div{display:none!important}',1);
              return c.display==='block';
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ShadowLayerOrderIsIndependentAndNamesRemainCaseSensitive()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@layer b,a;</style><div id=host></div>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const root=document.getElementById('host').attachShadow({mode:'open'});
              root.innerHTML='<style>@layer a,b; @layer a{span{display:none}} @layer b{span{display:block}}</style><span></span>';
              return getComputedStyle(root.querySelector('span')).display;
            })()
            """)).Should().Be("block");
        page.Errors.Should().BeEmpty();
    }
}
