namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

// CSS Cascade 6 §3.5: https://drafts.csswg.org/css-cascade-6/#scoped-styles
public sealed class NativeCssScopeTests
{
    [Test]
    public async Task ScopedRulesApplyWithAndWithoutALimit()
    {
        // sebastienros/jint#4591
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <style>
              @scope (.r) { .a { display: flex } }
              @scope (.r) to (.stop) { .b { display: flex } }
            </style>
            <div class=r><span class=a>a</span><span class=b>b</span><div class=stop><span class=b id=limited>c</span></div></div>
            <span class=a id=outside>d</span>
            """);
        (await page.EvaluateAsync<string>("""
            ['.a', '.b', '#limited', '#outside'].map(s => getComputedStyle(document.querySelector(s)).display).join()
            """)).Should().Be("flex,flex,inline,inline");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ScopedPseudoElementsFollowTheirOriginatingElement()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@scope (.r) to (.stop) { .a::before { content:'x'; display:flex } }</style>" +
            "<div class=r><span class=a id=in></span><div class=stop><span class=a id=limited></span></div></div>");
        (await page.EvaluateAsync<string>("""
            ['#in', '#limited'].map(s => getComputedStyle(document.querySelector(s), '::before').display).join()
            """)).Should().Be("flex,inline");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ImplicitRootIsTheStyleElementsParent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <div id=host><style>@scope { p { display: flex } :scope { display: grid } }</style><p id=inside></p></div>
            <p id=outside></p>
            """);
        (await page.EvaluateAsync<string>("""
            ['#host', '#inside', '#outside'].map(s => getComputedStyle(document.querySelector(s)).display).join()
            """)).Should().Be("grid,flex,block");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ScopeRuleCssomExposesBoundariesAndLiveChildren()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@scope (.r) to (.stop) { .a { display:flex } } @scope { }</style>" +
            "<div class=r><span class=a id=t></span></div>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const rules = document.styleSheets[0].cssRules, scope = rules[0], bare = rules[1];
              const t = document.getElementById('t'), c = getComputedStyle(t);
              const before = c.display;
              scope.deleteRule(0);
              const removed = c.display;
              scope.insertRule('.a { display:grid }', 0);
              return [scope instanceof CSSScopeRule, scope instanceof CSSGroupingRule, scope.type,
                Object.prototype.toString.call(scope), scope.start, scope.end, bare.start, bare.end,
                scope.cssRules[0].parentRule === scope, before, removed, c.display,
                scope.cssText.startsWith('@scope (.r) to (.stop) {')].join();
            })()
            """)).Should().Be("true,true,0,[object CSSScopeRule],.r,.stop,,,true,flex,inline,grid,true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ShadowTreeScopesMatchInsideTheirTree()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=host></div><span class=a id=light></span>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const root = document.getElementById('host').attachShadow({ mode: 'open' });
              root.innerHTML = '<style>@scope (.r) to (.stop) { .a { display:flex } } @scope { .b { display:grid } :scope > i { display:flex } }</style>' +
                '<div class=r><span class=a id=in></span><div class=stop><span class=a id=limited></span></div></div>' +
                '<span class=b id=implicit></span><i id=child></i><p><i id=grandchild></i></p>';
              return [root.getElementById('in'), root.getElementById('limited'), root.getElementById('implicit'),
                root.getElementById('child'), root.getElementById('grandchild'), document.getElementById('light')].map(e => getComputedStyle(e).display).join();
            })()
            """)).Should().Be("flex,inline,grid,flex,inline,inline");
        page.Errors.Should().BeEmpty();
    }
}
