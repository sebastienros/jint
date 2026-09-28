#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Accessibility;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.Runtime;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssContainerLayoutTests
{
    [Test]
    public async Task NamedNestedRangesTrackMeasuredWidthsAndLiveCssomChanges()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>
              @container panel (100px <= width < 400px) {
                @container outer (width >= 500px) { #child { opacity:.5 } }
              }
            </style>
            <div style="display:flex">
              <div style="container:outer / inline-size;display:flex;width:600px;flex-shrink:0">
                <div id=container style="container:panel / inline-size;width:380px;flex-shrink:0">
                  <div id=child></div>
                </div>
              </div>
            </div>
            """);
        (await page.EvaluateAsync<string>("""
            (() => {
              const sheet=document.styleSheets[0], rule=sheet.cssRules[0], nested=rule.cssRules[0],
                declaration=nested.cssRules[0].style, live=getComputedStyle(child);
              const values=[];
              function read() {
                values.push(container.getBoundingClientRect().width, live.opacity,
                  live.getPropertyValue('opacity'));
              }
              read();
              container.style.width='400px'; read();
              container.style.width='100px'; read();
              declaration.opacity='.75'; read();
              container.style.setProperty('container-name','other'); read();
              container.style.setProperty('container-name','panel'); read();
              sheet.deleteRule(0); read();
              declaration.opacity='.25'; read();
              return values.join('|');
            })()
            """)).Should().Be("380|0.5|0.5|400|1|1|100|0.5|0.5|100|0.75|0.75|100|1|1|100|0.75|0.75|100|1|1|100|1|1");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RangeRulesExposeReadonlyMetadataBrandsAndAtomicInsertion()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style></style><div></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              'use strict';
              const sheet=document.styleSheets[0];
              sheet.insertRule('@container panel (100px <= width < 400px) { div { opacity:.5 } }',0);
              const rule=sheet.cssRules[0], children=rule.cssRules;
              if (!(rule instanceof CSSContainerRule) || !(rule instanceof CSSGroupingRule) ||
                  Object.prototype.toString.call(rule)!=='[object CSSContainerRule]' ||
                  rule.parentStyleSheet!==sheet || children[0].parentRule!==rule ||
                  rule.containerName!=='panel' || rule.containerQuery!=='(100px <= width < 400px)' ||
                  rule.conditionText!=='panel (100px <= width < 400px)' ||
                  rule.cssText!=='@container panel (100px <= width < 400px) {\ndiv { opacity: 0.5; }\n}') return false;
              for (const name of ['containerName','containerQuery','conditionText']) {
                try { rule[name]='changed'; return false; } catch(e) { if (!(e instanceof TypeError)) throw e; }
              }
              const getter=Object.getOwnPropertyDescriptor(CSSContainerRule.prototype,'containerQuery').get;
              try { getter.call({}); return false; } catch(e) { if (!(e instanceof TypeError)) throw e; }
              try { rule.insertRule('@import "no.css";',0); return false; }
              catch(e) { if (e.name!=='HierarchyRequestError') throw e; }
              if (rule.cssRules!==children || children.length!==1) return false;
              rule.insertRule('span { opacity:.75 }',1);
              if (children.length!==2 || children[1].parentRule!==rule) return false;
              rule.deleteRule(1);
              sheet.deleteRule(0);
              return rule.parentStyleSheet===null && rule.parentRule===null && children.length===1;
            })()
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [TestCase("(min-width:1px)")]
    [TestCase("not (min-width:1px)")]
    [TestCase("(width >= 1px)")]
    [TestCase("not (width >= 1px)")]
    public async Task DisplayContentsHasNoPrincipalContainerBoxEvenUnderNot(string condition)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container panel " + condition + "{#child{opacity:.5}}</style>"
            + "<div style='display:contents;container:panel / inline-size'><div id=child></div></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var traversal = CssCascade.Traversal.For(runtime.Document)!;
            var context = traversal.ReadContext!;
            context.HasSizeQuery.Should().BeFalse();
            traversal.Of(ContentDom.ElementById(runtime.Document!, "child")!).GetPropertyValue("opacity").Should().Be("1");
            context.HasSizeQuery.Should().BeFalse();
            return true;
        });
    }
    [Test]
    public async Task ConditionsReadActualFlexBoxesAndTrackSubsequentWidths()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container panel (max-width:550px){#child{opacity:.5;flex-direction:column}}"
            + "</style><div style='display:flex'><div id=container style='container:panel / inline-size;width:500px;flex-shrink:0'>"
            + "<div id=child></div></div><div style='flex:1'></div></div>");
        const string read = "[container.getBoundingClientRect().width,getComputedStyle(container).width,"
            + "getComputedStyle(child).opacity,getComputedStyle(child).flexDirection].join('|')";
        (await page.EvaluateAsync<string>(read)).Should().Be("500|500px|0.5|column");
        await page.EvaluateAsync("container.style.width='600px'");
        (await page.EvaluateAsync<string>(read)).Should().Be("600|600px|1|row");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AuthoredBlockWidthNeverSubstitutesForTheSyntheticContainerBox()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container panel (max-width:550px){#child{opacity:.5}}</style>"
            + "<div id=container style='container:panel / inline-size;width:10px'><div id=child></div></div>");
        (await page.EvaluateAsync<string>("[container.getBoundingClientRect().width,getComputedStyle(child).opacity].join('|')"))
            .Should().Be("1280|1");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with { Viewport = new global::Jint.Browser.Viewport(500, 720) });
            return true;
        });
        (await page.EvaluateAsync<string>("[container.getBoundingClientRect().width,getComputedStyle(child).opacity].join('|')"))
            .Should().Be("500|0.5");
    }

    [Test]
    public async Task SharedContextKeepsIrrelevantPropertyReadsGeometryFree()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container (width:1px){#child{font-size:12px}}</style>"
            + "<div style='container-type:inline-size'><div id=child></div></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var diagnostics = new NativeCssQueryDiagnostics();
            var traversal = CssCascade.Traversal.For(runtime.Document, diagnostics: diagnostics)!;
            var child = ContentDom.ElementById(runtime.Document!, "child")!;
            traversal.Of(child).GetPropertyValue("opacity").Should().Be("1");
            diagnostics.Queries.Should().HaveCount(1);
            diagnostics.Queries[0].ComputedPublications.Should().NotContainKey("container-type");
            diagnostics.Queries[0].ComputedPublications.Should().NotContainKey("font-size");
            return true;
        });
    }

    [Test]
    public async Task ShadowFlatTreeGeometryAndVerticalMetricsRemainNamedDependencies()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=host style='container:panel / inline-size'></div>");
        await page.EvaluateAsync("host.attachShadow({mode:'open'}).innerHTML="
            + "'<style>@container panel (width:1px){#child{opacity:.5}}</style><div id=child></div>'");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var child = DomBindings.Bind<Element>(engine.Evaluate("host.shadowRoot.getElementById('child')"),
                "container metric test").Target;
            var style = CssCascade.Traversal.For(runtime.Document)!.Of(child);
            Assert.Throws<CssIncompleteGrammarException>(() => style.GetPropertyValue("opacity"))!
                .Blocker.Should().Be("C6:container-flat-tree-metric");
            Assert.Throws<JavaScriptException>(() => engine.Evaluate(
                "getComputedStyle(host.shadowRoot.getElementById('child')).opacity"))!
                .Message.Should().Be("Failed to execute 'CSSStyleDeclaration.opacity': Unimplemented CSS grammar: C6:container-flat-tree-metric");
            return true;
        });
    }

    [Test]
    public async Task CssOmBrandAndConditionMetadataUseTheNativeContainerModel()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container panel (max-width:550px){div{opacity:.5}}</style><div></div>");
        (await page.EvaluateAsync<string>("(() => { const r=document.styleSheets[0].cssRules[0];"
            + "return [Object.prototype.toString.call(r),r.containerName,r.containerQuery,r.conditionText,r.cssRules.length].join('|') })()"))
            .Should().Be("[object CSSContainerRule]|panel|(max-width:550px)|panel (max-width:550px)|1");
    }
}
