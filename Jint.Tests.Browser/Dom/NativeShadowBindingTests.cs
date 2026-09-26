using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeShadowBindingTests
{
    [TestCase("mode")]
    [TestCase("slotAssignment")]
    public void InvalidEnumsConvertAnObjectOnlyOnce(string member)
    {
        using var dom = DomTestFixture.Create("<div></div>");
        dom.Execute("var count=0, value={toString(){if(++count>1)throw Error('second conversion');return 'invalid'}}, init={mode:'open'}; init['" + member + "']=value; var error; try{document.createElement('div').attachShadow(init)}catch(e){error=e.name}");
        dom.Text("error").Should().Be("TypeError");
        dom.Number("count").Should().Be(1);
    }

    [Test]
    public void OmittedRegistryReadsTheHostDocumentAfterAllDictionaryGetters()
    {
        using var dom = DomTestFixture.Create("<div id=h></div>");
        var host = ContentDom.ElementById(dom.Document, "h")!;
        dom.Document.SetCustomElementRegistry(new(false));
        var destination = Document.CreateHtml();
        var registry = new CustomElementRegistryIdentity(false);
        destination.SetCustomElementRegistry(registry);
        dom.Engine.SetValue("other", DomBindings.Wrap(dom.Engine, destination));
        dom.Execute("var h=document.getElementById('h'); h.attachShadow({mode:'open',get slotAssignment(){other.adoptNode(h);return 'named'}});");
        host.OwnerDocument.Should().BeSameAs(destination);
        host.AttachedShadowRoot!.CustomElementRegistry.Should().BeSameAs(registry);
    }

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
