#nullable enable
using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCssKeyframesBindingTests
{
    [Test]
    public void RealBrandsOrderedIndexedListAndReadonlyDescriptorsFollowCssom()
    {
        using var dom = Create("@keyframes fade { from {opacity:.5} to {} }");
        dom.Execute("var root=sheet.cssRules[0], frame=root.cssRules[0], rules=root.cssRules;");
        dom.Bool("root instanceof CSSKeyframesRule && root instanceof CSSRule && !(root instanceof CSSStyleRule) && frame instanceof CSSKeyframeRule && frame instanceof CSSRule").Should().BeTrue();
        dom.Bool("Object.getPrototypeOf(CSSKeyframesRule.prototype)===CSSRule.prototype && Object.getPrototypeOf(CSSKeyframeRule.prototype)===CSSRule.prototype").Should().BeTrue();
        dom.Text("Object.prototype.toString.call(root)").Should().Be("[object CSSKeyframesRule]");
        dom.Text("Object.prototype.toString.call(frame)").Should().Be("[object CSSKeyframeRule]");
        dom.Bool("root.type===7 && root.type===CSSRule.KEYFRAMES_RULE && frame.type===8 && frame.type===CSSRule.KEYFRAME_RULE").Should().BeTrue();
        dom.Bool("root.cssRules===root.rules && root.cssRules===rules && rules instanceof CSSRuleList && root.length===2 && root[0]===frame && root[2]===undefined").Should().BeTrue();
        dom.Bool("frame.style===frame.style && frame.style instanceof CSSStyleDeclaration && frame.style.parentRule===frame && frame.parentRule===root && frame.parentStyleSheet===sheet").Should().BeTrue();
        dom.Bool("!('insertRule' in root) && !('selectorText' in frame) && !('cssRules' in frame)").Should().BeTrue();
        dom.Bool("['cssRules','rules','length'].every(k=>{let d=Object.getOwnPropertyDescriptor(CSSKeyframesRule.prototype,k);return d.enumerable && d.configurable && d.set===undefined})").Should().BeTrue();
        dom.Bool("['appendRule','findRule','deleteRule'].every(k=>{let d=Object.getOwnPropertyDescriptor(CSSKeyframesRule.prototype,k);return d.enumerable && d.configurable && d.writable && d.value.length===1})").Should().BeTrue();
        dom.Bool("Object.getOwnPropertyDescriptor(root,'0').writable===false && Object.keys(root).includes('0')").Should().BeTrue();
        dom.Execute("root.length=9; root.cssRules=[]; root[0]=null;");
        dom.Bool("root.length===2 && root.cssRules===rules && root[0]===frame").Should().BeTrue();
    }

    [Test]
    public void MutationsNormalizeSelectLastExactMatchAndKeepStyleIdentity()
    {
        using var dom = Create("@keyframes fade { from,50% {} from,50% {} }");
        dom.Execute("var root=sheet.cssRules[0], first=root[0], last=root[1], rules=root.cssRules, style=first.style;");
        dom.Bool("root.findRule('0%,5e1%')===last && root.findRule('from')===null && root.findRule('50%,0%')===null").Should().BeTrue();
        dom.Execute("root.deleteRule('from,50%'); root.appendRule('from,50% {}'); root.appendRule('0 {}'); root.appendRule('from {} to {}');");
        dom.Bool("root.length===2 && rules.length===2 && rules===root.cssRules && root.findRule('0%,50%')===root[1] && last.parentRule===null && last.parentStyleSheet===null").Should().BeTrue();
        dom.Execute("first.keyText='to,to'; first.style='opacity:.25; border-color:red !important'; root.name='inherit';");
        dom.Bool("first.keyText==='100%, 100%' && first.style===style && first.style.opacity==='0.25' && !first.style.cssText.includes('important') && root.name==='inherit'").Should().BeTrue();
        dom.Text("(()=>{try{first.keyText='100.0000000000000000000001%'}catch(e){return e.name}})()").Should().Be("SyntaxError");
        dom.Bool("first.keyText==='100%, 100%' && root.findRule('to,to')===first && root.cssText.includes('@keyframes \"inherit\"')").Should().BeTrue();
        dom.Execute("sheet.deleteRule(0); first.style.opacity='.5'; root.appendRule('from {}');");
        dom.Bool("root.parentRule===null && root.parentStyleSheet===null && first.parentRule===root && first.parentStyleSheet===sheet && sheet.cssRules.length===0").Should().BeTrue();
    }

    [Test]
    public void BorrowedMembersRejectBeforeConvertingArguments()
    {
        using var dom = Create("a {} @media screen {} @keyframes x {from {}}");
        dom.Execute("var root=sheet.cssRules[2], frame=root[0], foreign=[sheet.cssRules[0],sheet.cssRules[1],frame,{}];");
        dom.Bool("foreign.every(r=>['appendRule','deleteRule','findRule'].every(k=>{let converted=false;try{CSSKeyframesRule.prototype[k].call(r,{toString(){converted=true;return 'from'}});return false}catch(e){return e instanceof TypeError && !converted}}))").Should().BeTrue();
        dom.Bool("[root,{}].every(r=>{let converted=false;try{Object.getOwnPropertyDescriptor(CSSKeyframeRule.prototype,'keyText').set.call(r,{toString(){converted=true;return 'to'}});return false}catch(e){return e instanceof TypeError && !converted}})").Should().BeTrue();
        dom.Text("(()=>{try{new CSSKeyframesRule()}catch(e){return e.name}})()").Should().Be("TypeError");
        dom.Text("(()=>{try{new CSSKeyframeRule()}catch(e){return e.name}})()").Should().Be("TypeError");
    }

    [Test]
    public async Task UnusedKeyframesLeaveOrdinaryGeometryAndComputedStyleWorking()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@keyframes fade-out {from {opacity:1} to {opacity:0}} #target {width:40px;height:10px}</style><div id='target'></div>");
        (await page.EvaluateAsync<bool>("document.styleSheets[0].cssRules[0] instanceof CSSKeyframesRule")).Should().BeTrue();
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('target')).width")).Should().Be("40px");
        (await page.EvaluateAsync<bool>("document.getElementById('target').getClientRects().length===1")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    private static DomTestFixture Create(string css)
    {
        var dom = DomTestFixture.Create("");
        dom.Engine.SetValue("sheet", DomRealm.Of(dom.Engine).Wrap(CssStyleSheet.Parse(css)));
        return dom;
    }
}
