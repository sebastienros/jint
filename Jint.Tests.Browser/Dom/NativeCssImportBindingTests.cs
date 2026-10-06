#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Dom;

/// <summary>Bindings over actual native import occurrences and their published child sheets.</summary>
/// <remarks>https://drafts.csswg.org/cssom/#the-cssimportrule-interface</remarks>
public sealed class NativeCssImportBindingTests
{
    [Test]
    public void AnImportHasItsOwnBrandAndInheritedRuleRelationshipsBeforeLoading()
    {
        using var dom = Create("@import 'child.css' screen; a { color:red }");
        dom.Execute("var rule=sheet.cssRules[0], media=rule.media;");
        dom.Bool("""
            rule instanceof CSSImportRule && rule instanceof CSSRule &&
            !(rule instanceof CSSStyleRule) && !(rule instanceof CSSMediaRule) &&
            rule.constructor === CSSImportRule && rule.type === 3 && rule.type === CSSRule.IMPORT_RULE &&
            Object.getPrototypeOf(CSSImportRule.prototype) === CSSRule.prototype &&
            rule.parentRule === null && rule.parentStyleSheet === sheet &&
            rule.styleSheet === null && media instanceof MediaList && media === rule.media
            """).Should().BeTrue();
        dom.Text("rule.href").Should().Be("child.css");
        dom.Text("media.mediaText").Should().Be("screen");
        dom.Bool("""
            ['href','media','styleSheet'].every(name => {
                const descriptor=Object.getOwnPropertyDescriptor(CSSImportRule.prototype,name);
                return descriptor.enumerable && descriptor.configurable && typeof descriptor.get==='function' &&
                    descriptor.get.length===0 &&
                    (name==='media' ? typeof descriptor.set==='function' && descriptor.set.length===1 : descriptor.set===undefined);
            })
            """).Should().BeTrue();
    }

    [Test]
    public void AChildPublicationKeepsOccurrenceSheetAndMediaIdentitiesWithActualParentLinks()
    {
        using var dom = Create("@import 'same.css' screen; @import 'same.css';");
        var sheet = Sheet(dom);
        var first = (CssImportRule) sheet.Rules[0];
        var second = (CssImportRule) sheet.Rules[1];
        var child = CssStyleSheet.Parse("@import 'deep.css'; a { color:red }");
        var other = CssStyleSheet.Parse("b { color:blue }");
        var deep = CssStyleSheet.Parse("c { color:green }");
        dom.Execute("var first=sheet.cssRules[0], second=sheet.cssRules[1], media=first.media;");
        dom.Bool("first.styleSheet===null && second.styleSheet===null").Should().BeTrue();
        var url = new Uri("https://example.test/redirect/child.css");
        first.SetStyleSheet(child, url, url, Work(dom));
        second.SetStyleSheet(other, url, url, Work(dom));
        var deepUrl = new Uri("https://example.test/deep.css");
        ((CssImportRule) child.Rules[0]).SetStyleSheet(deep, deepUrl, deepUrl, Work(dom));
        dom.Execute("var child=first.styleSheet, other=second.styleSheet, nested=child.cssRules[0], deep=nested.styleSheet;");
        dom.Bool("""
            first===sheet.cssRules[0] && second===sheet.cssRules[1] &&
            child instanceof CSSStyleSheet && child instanceof StyleSheet && child===first.styleSheet &&
            child!==other && child.ownerRule===first && child.parentStyleSheet===sheet && child.ownerNode===null &&
            child.media===media && first.media===media &&
            nested instanceof CSSImportRule && nested.parentRule===null && nested.parentStyleSheet===child &&
            deep.ownerRule===nested && deep.parentStyleSheet===child && deep.ownerNode===null &&
            child.cssRules[1].parentRule===null && child.cssRules[1].parentStyleSheet===child
            """).Should().BeTrue();
        // The import exposes the URL specified in its prelude; the fetched sheet holds the final URL.
        dom.Text("first.href + '|' + child.href + '|' + nested.href + '|' + deep.href")
            .Should().Be("same.css|https://example.test/redirect/child.css|deep.css|https://example.test/deep.css");
        dom.Execute("sheet.deleteRule(0)");
        dom.Bool("""
            first.parentRule===null && first.parentStyleSheet===null && first.styleSheet===child &&
            child.ownerRule===first && child.parentStyleSheet===null && child.media===media &&
            second===sheet.cssRules[0] && second.parentStyleSheet===sheet && other.ownerRule===second
            """).Should().BeTrue();
    }

    [Test]
    public void MediaPutForwardsMutatesTheSameNativeListBeforeAndAfterChildPublication()
    {
        using var dom = Create("@import 'child.css' screen;");
        var rule = (CssImportRule) Sheet(dom).Rules[0];
        dom.Execute("var rule=sheet.cssRules[0], media=rule.media, conversions=0; rule.media={toString(){conversions++; return 'print'}};");
        dom.Bool("conversions===1 && rule.media===media && media.mediaText==='print' && rule.styleSheet===null").Should().BeTrue();
        var child = CssStyleSheet.Parse("a { color:red }");
        var url = new Uri("https://example.test/child.css");
        rule.SetStyleSheet(child, url, url, Work(dom));
        dom.Execute("var child=rule.styleSheet; child.media.appendMedium('screen');");
        dom.Bool("child.media===media && rule.media===media && media.length===2 && media.item(0)==='print' && media.item(1)==='screen'").Should().BeTrue();
        dom.Execute("rule.media='all'");
        dom.Bool("child.media===media && rule.media===media && child.media.mediaText==='all'").Should().BeTrue();
        dom.Execute("media.mediaText='screen'");
        dom.Bool("rule.media.mediaText==='screen' && child.media.mediaText==='screen'").Should().BeTrue();
        rule.Media.MediaText.Should().Be("screen");
    }

    [Test]
    public void ImportMembersRejectOtherRulesAndPlainReceiversBeforePutForwardsConversion()
    {
        using var dom = Create("@import 'child.css'; a { color:red } @media screen { b { color:blue } }");
        dom.Execute("var importRule=sheet.cssRules[0], style=sheet.cssRules[1], mediaRule=sheet.cssRules[2];");
        dom.Bool("""
            ['href','media','styleSheet'].every(name => {
                const getter=Object.getOwnPropertyDescriptor(CSSImportRule.prototype,name).get;
                return [style,mediaRule,sheet,{}].every(receiver => {
                    try { getter.call(receiver); return false; } catch (error) { return error instanceof TypeError; }
                });
            })
            """).Should().BeTrue();
        dom.Bool("""
            [style,mediaRule,sheet,{}].every(receiver => {
                let conversions=0;
                try {
                    Object.getOwnPropertyDescriptor(CSSImportRule.prototype,'media').set.call(receiver,
                        {toString(){conversions++; return 'print'}});
                    return false;
                } catch (error) { return error instanceof TypeError && conversions===0; }
            })
            """).Should().BeTrue();
    }

    private static DomTestFixture Create(string css)
    {
        var dom = DomTestFixture.Create("");
        dom.Engine.SetValue("sheet", DomRealm.Of(dom.Engine).Wrap(CssStyleSheet.Parse(css)));
        return dom;
    }

    private static CssStyleSheet Sheet(DomTestFixture dom)
        => (CssStyleSheet) ((IDomWrapper) dom.Evaluate("sheet")).DomTarget;

    private static CssValueWork Work(DomTestFixture dom)
        => NativeCssBindings.Work(DomRealm.Of(dom.Engine));
}
