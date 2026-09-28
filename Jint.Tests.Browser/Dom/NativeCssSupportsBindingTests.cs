#nullable enable
using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCssSupportsBindingTests
{
    [Test]
    public void SupportsHasActualBrandInheritedListsAndReadonlyConditionAndMatches()
    {
        using var dom = Create("@supports (color:red) { a {} } @supports (unknown-property:none) { b {} } @media screen {}");
        dom.Execute("var supported=sheet.cssRules[0], unsupported=sheet.cssRules[1], media=sheet.cssRules[2];");
        dom.Bool("supported instanceof CSSSupportsRule && supported instanceof CSSRule && !(supported instanceof CSSMediaRule) && !(supported instanceof CSSStyleRule)").Should().BeTrue();
        dom.Text("Object.prototype.toString.call(supported)").Should().Be("[object CSSSupportsRule]");
        dom.Bool("Object.getPrototypeOf(CSSSupportsRule.prototype)===Object.getPrototypeOf(CSSMediaRule.prototype)").Should().BeTrue();
        dom.Bool("supported.cssRules===supported.rules && supported.cssRules===supported.cssRules && supported.cssRules instanceof CSSRuleList").Should().BeTrue();
        dom.Bool("supported.type===CSSRule.SUPPORTS_RULE && supported.type===12 && supported.matches && !unsupported.matches").Should().BeTrue();
        dom.Text("unsupported.conditionText").Should().Be("(unknown-property:none)");
        dom.Bool("Object.getOwnPropertyDescriptor(Object.getPrototypeOf(CSSSupportsRule.prototype),'conditionText').set===undefined").Should().BeTrue();
        dom.Bool("Object.getOwnPropertyDescriptor(CSSSupportsRule.prototype,'matches').set===undefined").Should().BeTrue();
        dom.Execute("supported.conditionText='(color:blue)'; supported.matches=false; media.media.mediaText='print';");
        dom.Bool("supported.conditionText==='(color:red)' && supported.matches && media.conditionText==='print'").Should().BeTrue();
        dom.Text("(()=>{'use strict';try{supported.conditionText='x'}catch(e){return e.name}})()").Should().Be("TypeError");
    }

    [Test]
    public void InheritedGroupOperationsKeepIdentityLinksAndValidateReceiversBeforeConversions()
    {
        using var dom = Create("@supports (color:red) { @media screen { a {} } }");
        dom.Execute("var root=sheet.cssRules[0], nested=root.cssRules[0], child=nested.cssRules[0], rules=root.cssRules; root.insertRule('b {}',1);");
        dom.Bool("rules.length===2 && rules[1].parentRule===root && rules[1].parentStyleSheet===sheet").Should().BeTrue();
        dom.Execute("sheet.deleteRule(0);");
        dom.Bool("root.parentRule===null && root.parentStyleSheet===null && child.parentRule===nested && child.parentStyleSheet===sheet").Should().BeTrue();
        dom.Execute("root.insertRule('c {}',2); root.deleteRule(1);");
        dom.Bool("rules===root.cssRules && rules.length===2 && sheet.cssRules.length===0").Should().BeTrue();
        dom.Execute("var groupPrototype=Object.getPrototypeOf(Object.getPrototypeOf(CSSSupportsRule.prototype));");
        dom.Bool("[child,{}].every(r=>{let converted=false;try{groupPrototype.insertRule.call(r,{toString(){converted=true;return 'a {}'}},0);return false}catch(e){return e instanceof TypeError && !converted}})").Should().BeTrue();
        dom.Bool("[nested,child,{}].every(r=>{try{Object.getOwnPropertyDescriptor(CSSSupportsRule.prototype,'matches').get.call(r);return false}catch(e){return e instanceof TypeError}})").Should().BeTrue();
        dom.Bool("[root,{}].every(r=>{try{Object.getOwnPropertyDescriptor(CSSMediaRule.prototype,'media').get.call(r);return false}catch(e){return e instanceof TypeError}})").Should().BeTrue();
        dom.Text("(()=>{try{root.insertRule('@supports color:red {}',0)}catch(e){return e.name}})()").Should().Be("SyntaxError");
        dom.Text("(()=>{try{root.insertRule('@container unknown {}',-1)}catch(e){return e.name}})()").Should().Be("IndexSizeError");
        dom.Bool("rules.length===2").Should().BeTrue();
    }

    private static DomTestFixture Create(string css)
    {
        var dom = DomTestFixture.Create("");
        dom.Engine.SetValue("sheet", DomRealm.Of(dom.Engine).Wrap(CssStyleSheet.Parse(css)));
        return dom;
    }
}
