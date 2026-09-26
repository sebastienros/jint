using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCssBindingTests
{
    [Test]
    public void StylesheetDisabledIsTheActualNativeFlag()
    {
        using var dom = Create("a { color:red }");
        dom.Bool("!sheet.disabled").Should().BeTrue();
        dom.Execute("sheet.disabled=true;");
        dom.Bool("sheet.disabled").Should().BeTrue();
        dom.Execute("sheet.disabled=false;");
        dom.Bool("!sheet.disabled && sheet.cssRules.length===1").Should().BeTrue();
    }

    [Test]
    public void StyleRuleListsAreEmptyStableAliasesWithDistinctOwners()
    {
        using var dom = Create("a { color:red } b { color:blue }");
        dom.Execute("var a=sheet.cssRules[0], b=sheet.cssRules[1];");
        dom.Bool("a instanceof CSSStyleRule && a instanceof CSSRule && a.cssRules===a.rules && a.cssRules===a.cssRules && a.cssRules!==b.cssRules").Should().BeTrue();
        dom.Bool("a.cssRules instanceof CSSRuleList && a.cssRules.length===0 && a.cssRules.item(0)===null && a.cssRules[0]===undefined").Should().BeTrue();
        dom.Bool("a.style===a.style && a.style instanceof CSSStyleDeclaration && a.style.parentRule===a && a.parentStyleSheet===sheet").Should().BeTrue();
    }

    [Test]
    public void UnsupportedGrammarFailsAtomicallyWithoutProducingDormantBrands()
    {
        using var dom = Create("a { color:red }");
        dom.Execute("var before=sheet.cssRules[0];");
        foreach (var source in new[] { "@import 'other.css';", "@keyframes move { from { opacity:0 } }", "a { @media screen { color:red } }" })
        {
            dom.Engine.SetValue("source", source);
            dom.Text("(()=>{try{sheet.insertRule(source,0)}catch(e){return e.name}})()").Should().Be("NotSupportedError");
            dom.Bool("sheet.cssRules.length===1 && sheet.cssRules[0]===before && before.parentStyleSheet===sheet").Should().BeTrue();
        }
    }

    [Test]
    public void DormantOwnMembersRejectRealRulesAndPlainReceiversBeforeConversions()
    {
        using var dom = Create("a { color:red } @media screen { b { color:blue } }");
        dom.Execute("var style=sheet.cssRules[0], media=sheet.cssRules[1];");
        dom.Bool("media instanceof CSSMediaRule && !(media instanceof CSSStyleRule) && !(media instanceof CSSImportRule)").Should().BeTrue();
        dom.Execute("var href=Object.getOwnPropertyDescriptor(CSSImportRule.prototype,'href').get; var enc=Object.getOwnPropertyDescriptor(CSSCharsetRule.prototype,'encoding').set;");
        dom.Bool("[style,media,{}].every(r=>{try{href.call(r);return false}catch(e){return e instanceof TypeError && /Illegal invocation/.test(e.message)}})").Should().BeTrue();
        dom.Bool("[style,media,{}].every(r=>{let converted=false;try{enc.call(r,{toString(){converted=true;return 'utf-8'}});return false}catch(e){return e instanceof TypeError && !converted}})").Should().BeTrue();
    }

    [Test]
    public void MediaRuleMutationsUseActualLiveModelsAndParentLinks()
    {
        using var dom = Create("@media screen { a { color:red } }");
        dom.Execute("var media=sheet.cssRules[0], rules=media.cssRules; media.insertRule('b { color:blue }',1);");
        dom.Bool("rules===media.rules && rules.length===2 && rules[1].parentRule===media && rules[1].parentStyleSheet===sheet").Should().BeTrue();
        dom.Execute("var removed=rules[0]; media.deleteRule(0); media.media.appendMedium('print');");
        dom.Bool("rules.length===1 && removed.parentRule===null && removed.parentStyleSheet===null && media.media.length===2 && media.media.item(1)==='print'").Should().BeTrue();
        dom.Execute("media.media.removeMedium('screen'); media.conditionText='all';");
        dom.Text("media.media.mediaText").Should().Be("all");
    }

    private static DomTestFixture Create(string css)
    {
        var dom = DomTestFixture.Create("");
        dom.Engine.SetValue("sheet", DomRealm.Of(dom.Engine).Wrap(CssStyleSheet.Parse(css)));
        return dom;
    }
}
