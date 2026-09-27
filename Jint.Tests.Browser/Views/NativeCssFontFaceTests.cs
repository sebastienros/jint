#nullable enable
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssFontFaceTests
{
    // Exact static descriptor rule in the pinned Monaco 0.52.2 stylesheet. The vendor fixture stays untouched.
    private const string MonacoFontFace = "@font-face{font-display:block;font-family:codicon;"
        + "src:url(../base/browser/ui/codicons/codicon/codicon.ttf) format(\"truetype\")}";

    [Test]
    public async Task LoadCaptureCanReadTheRealSheetAndFontFaceBeforeTheTargetListenerCompletes()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/monaco.css", _ => LoopbackResponse.Css(MonacoFontFace))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.events = [];
                  document.addEventListener('load', event => {
                    if (event.target.tagName !== 'LINK') return;
                    const sheet = event.target.sheet;
                    const rule = sheet.cssRules[0];
                    events.push('capture:' + !!sheet + ':' + rule.type + ':' + rule.style.fontFamily);
                  }, true);
                </script>
                <link rel="stylesheet" href="/monaco.css" onload="events.push('target')">
                </head><body></body></html>
                """));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        (await loopback.Page.EvaluateAsync<string>("events.join('|')")).Should().Be("capture:true:5:codicon|target");
        loopback.Page.Errors.Should().BeEmpty();
        loopback.Server.Received.Should().OnlyContain(request => request.Path == "/" || request.Path == "/monaco.css");
    }

    [Test]
    public async Task FontFaceStyleSharesOneStoreAndHasTheDescriptorAndDeclarationBrands()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>" + MonacoFontFace + "</style>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const sheet = document.styleSheets[0], rule = sheet.cssRules[0], style = rule.style;
              return rule instanceof CSSFontFaceRule && rule instanceof CSSRule &&
                rule.type === CSSRule.FONT_FACE_RULE && style === rule.style &&
                style instanceof CSSFontFaceDescriptors && style instanceof CSSStyleDeclaration &&
                Object.prototype.toString.call(style) === '[object CSSFontFaceDescriptors]' &&
                style.parentRule === rule && rule.parentStyleSheet === sheet &&
                style.fontFamily === 'codicon' && style['font-family'] === style.fontFamily &&
                style.fontDisplay === 'block' && style[0] === 'font-display';
            })()
            """)).Should().BeTrue();
        (await page.EvaluateAsync<bool>("""
            (() => {
              const sheet = document.styleSheets[0], rule = sheet.cssRules[0], style = rule.style;
              style.fontFamily = 'MiXeD';
              if (rule.family !== 'MiXeD' || style.getPropertyValue('font-family') !== 'MiXeD') return false;
              style.setProperty('font-display', 'swap', 'important');
              if (style.getPropertyPriority('font-display') !== 'important' || !rule.cssText.includes('font-display: swap !important;')) return false;
              rule.style = 'font-family:Replaced;src:local("Local Font");font-display:optional';
              if (style !== rule.style || style.fontFamily !== 'Replaced' || style.fontDisplay !== 'optional') return false;
              style.fontFamily = null;
              if (style.fontFamily !== '') return false;
              sheet.deleteRule(0);
              if (rule.parentStyleSheet !== null || style.parentRule !== rule) return false;
              style.fontWeight = '100 900';
              return style.fontWeight === '100 900' && sheet.cssRules.length === 0;
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task MonacoDynamicRuleRetainsItsFiveDescriptorsWithoutLoadingAFont()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server.MapHtml("/", "<html><head></head><body></body></html>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        // The pinned Monaco bundle emits this descriptor shape for dynamic icon fonts.
        (await loopback.Page.EvaluateAsync<string>("""
            (() => {
              const style = document.createElement('style');
              style.textContent = "@font-face { src: url('/icons.woff2') format('woff2'); font-family: icons; font-weight: normal; font-style: normal; font-display: block; }";
              document.head.appendChild(style);
              const descriptors = style.sheet.cssRules[0].style;
              return [descriptors.src,descriptors.fontFamily,descriptors.fontWeight,descriptors.fontStyle,descriptors.fontDisplay].join('|');
            })()
            """)).Should().Be("url(\"/icons.woff2\") format(\"woff2\")|icons|normal|normal|block");
        await loopback.Page.WaitForIdleAsync(TestBudgets.WedgeCeiling);
        loopback.Server.Received.Should().OnlyContain(request => request.Path == "/");
        loopback.Page.Errors.Should().BeEmpty();
    }


    [Test]
    public async Task SheetRuleAndStyleObjectReadsDoNotDemandPendingDescriptorValues()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@font-face {font-family:Known;font-width:condensed}</style>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const sheet = document.styleSheets[0], rule = sheet.cssRules[0], style = rule.style;
              if (style.fontFamily !== 'Known' || style !== rule.style) return 'bad-target';
              style.fontFamily = 'Changed';
              try { style.fontWidth; return 'missing-refusal'; }
              catch (error) { return style.fontFamily + ':' + error.name + ':' + error.message.includes('R4:font-face:font-width'); }
            })()
            """)).Should().Be("Changed:NotSupportedError:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SeparateImportsOwnSeparateFontFaceRulesAndDescriptorWrappers()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/font.css", _ => LoopbackResponse.Css(MonacoFontFace))
            .MapHtml("/", "<html><head><style>@import url('/font.css');@import url('/font.css');</style></head><body></body></html>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(TestBudgets.WedgeCeiling);
        (await loopback.Page.EvaluateAsync<bool>("""
            (() => {
              const imports = document.styleSheets[0].cssRules;
              const a = imports[0].styleSheet, b = imports[1].styleSheet;
              const first = a.cssRules[0], second = b.cssRules[0];
              return a !== b && first !== second && first.style !== second.style &&
                a.ownerRule === imports[0] && b.ownerRule === imports[1] &&
                first.parentStyleSheet === a && second.parentStyleSheet === b &&
                first.style.parentRule === first && second.style.parentRule === second;
            })()
            """)).Should().BeTrue();
        loopback.Server.Received.Should().NotContain(request => request.Path.EndsWith(".ttf", StringComparison.Ordinal));
        loopback.Page.Errors.Should().BeEmpty();
    }
}
