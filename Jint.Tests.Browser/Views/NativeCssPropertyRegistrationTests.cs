namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssPropertyRegistrationTests
{
    [Test]
    public async Task PropertyRulesExposeReadonlyDescriptorsAndLiveRegistrationEffects()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>
            @property --size {syntax:"<length>";inherits:false;initial-value:4px}
            @property --empty {syntax:"*";inherits:false}
            section {--size:12px} div {display:inline;width:var(--size)}
            </style><section><div id=t></div></section>
            """);
        (await page.EvaluateAsync<string>("""
            (() => {
              const sheet=document.styleSheets[0], rule=sheet.cssRules[0], t=document.getElementById('t');
              const computed=getComputedStyle(t);
              if (!(rule instanceof CSSPropertyRule) || !(rule instanceof CSSRule) ||
                  rule instanceof CSSGroupingRule || rule.type!==0) return 'brand';
              if (Object.prototype.toString.call(rule)!=='[object CSSPropertyRule]' ||
                  rule.name!=='--size' || rule.syntax!=='<length>' || rule.inherits!==false ||
                  rule.initialValue!=='4px' || rule.parentStyleSheet!==sheet || rule.parentRule!==null) return 'metadata';
              if (sheet.cssRules[1].initialValue!==null || 'setProperty' in rule || 'style' in rule) return 'surface';
              for (const name of ['name','syntax','inherits','initialValue']) {
                const descriptor=Object.getOwnPropertyDescriptor(CSSPropertyRule.prototype,name);
                if (!descriptor.enumerable || descriptor.set!==undefined) return 'descriptor:'+name;
                let refused=false;
                try {descriptor.get.call({})} catch(e) {refused=e instanceof TypeError}
                if (!refused) return 'receiver:'+name;
              }
              if (computed.width!=='4px' || computed.getPropertyValue('--size')!=='4px')
                return 'initial:'+computed.width+'|'+computed.getPropertyValue('--size');
              const names=Array.from(computed);
              if (!names.includes('--size') || names.includes('--empty')) return 'enumeration';
              t.style.setProperty('--size','wrong');
              if (t.style.getPropertyValue('--size')!=='wrong' || computed.width!=='4px' ||
                  !CSS.supports('--size','wrong')) return 'invalid value';
              t.style.removeProperty('--size');
              sheet.deleteRule(0);
              if (rule.parentStyleSheet!==null || computed.width!=='12px') return 'removal';
              sheet.insertRule('@property --size {syntax:"<length>";inherits:false;initial-value:8px}',0);
              return computed.width==='8px' && sheet.cssRules[0]!==rule &&
                rule.cssText==='@property --size { syntax: "<length>"; inherits: false; initial-value: 4px; }'
                ? 'ok' : 'replacement';
            })()
            """)).Should().Be("ok");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RegistrationsAreDocumentGlobalAcrossShadowTreesAndDoNotFetchValues()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=host></div><div id=t style='display:inline;width:var(--size)'></div>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const root=document.getElementById('host').attachShadow({mode:'open'});
              root.innerHTML='<style>@property --size {syntax:"<length>";inherits:false;initial-value:7px}</style>';
              const computed=getComputedStyle(document.getElementById('t'));
              const before=computed.getPropertyValue('--size')+'|'+computed.width;
              root.innerHTML='';
              return before+'|'+computed.getPropertyValue('--size');
            })()
            """)).Should().Be("7px|7px|");
        page.Requests.Should().BeEmpty();
        page.Errors.Should().BeEmpty();
    }
}
