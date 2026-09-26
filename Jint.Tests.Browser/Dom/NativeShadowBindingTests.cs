namespace Jint.Tests.Browser.Dom;

public sealed class NativeShadowBindingTests
{
    [Test]
    public void AttachmentConvertsDictionaryInOrderAndPreservesRootIdentity()
    {
        using var dom = DomTestFixture.Create("<div id=h></div>");
        dom.Execute("var h=document.getElementById('h'), seen=[], init={}; ['clonable','customElementRegistry','delegatesFocus','mode','serializable','slotAssignment'].forEach(k=>Object.defineProperty(init,k,{get(){seen.push(k);return k==='mode'?'open':undefined}})); var root=h.attachShadow(init);");
        dom.Text("seen.join(',')").Should().Be("clonable,customElementRegistry,delegatesFocus,mode,serializable,slotAssignment");
        dom.Bool("root===h.shadowRoot && root instanceof ShadowRoot && root.host===h && root.mode==='open'").Should().BeTrue();
        dom.Text("(()=>{try{h.attachShadow({mode:'open'})}catch(e){return e.name}})()").Should().Be("NotSupportedError");
    }

    [Test]
    public void ClosedRootAndDictionaryFailuresUseNativeStateAndWebIdlErrors()
    {
        using var dom = DomTestFixture.Create("<div id=h></div>");
        dom.Execute("var h=document.getElementById('h'), root=h.attachShadow({mode:'closed',customElementRegistry:null,slotAssignment:'manual'});");
        dom.Bool("h.shadowRoot===null && root.mode==='closed' && root.host===h").Should().BeTrue();
        foreach (var argument in new[] { "undefined", "null", "1", "{}", "{mode:'invalid'}", "{mode:'open',slotAssignment:'invalid'}", "{mode:'open',customElementRegistry:{}}" })
            dom.Text("(()=>{try{document.createElement('div').attachShadow(" + argument + ")}catch(e){return e.name}})()").Should().Be("TypeError");
    }
}
