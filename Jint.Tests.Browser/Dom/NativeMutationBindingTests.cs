using Jint.Browser.Dom;
using Jint.Browser.Accessibility;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeMutationBindingTests
{
    [Test]
    public void NativeRecordListsKeepSnapshotMembershipAndSameObjectIdentity()
    {
        using var dom = DomTestFixture.Create("<div id=h><span id=c></span></div>");
        var host = ContentDom.ElementById(dom.Document, "h")!;
        var child = host.FirstChild!;
        var record = new MutationRecord(MutationRecordKind.ChildList, host,
            addedNodes: Array.AsReadOnly(new[] { child }));
        dom.Engine.SetValue("r", DomBindings.Wrap(dom.Engine, record));
        dom.Execute("var h=document.getElementById('h'), c=document.getElementById('c'), added=r.addedNodes; h.removeChild(c);");
        dom.Bool("r instanceof MutationRecord && r.type==='childList' && r.target===h && added===r.addedNodes && added instanceof NodeList && added.length===1 && added[0]===c && r.removedNodes===r.removedNodes && r.removedNodes.length===0").Should().BeTrue();
    }

    [Test]
    public void AttributeAndCharacterDataFieldsComeFromTheirImmutableNativeRecord()
    {
        using var dom = DomTestFixture.Create("<div id=h></div>");
        var target = ContentDom.ElementById(dom.Document, "h")!;
        var record = new MutationRecord(MutationRecordKind.Attributes, target,
            attributeName: "x", attributeNamespace: "urn:test", oldValue: "before");
        dom.Engine.SetValue("r", DomBindings.Wrap(dom.Engine, record));
        dom.Bool("r.type==='attributes' && r.attributeName==='x' && r.attributeNamespace==='urn:test' && r.oldValue==='before' && r.previousSibling===null && r.nextSibling===null").Should().BeTrue();
        dom.Engine.SetValue("r", DomBindings.Wrap(dom.Engine,
            new MutationRecord(MutationRecordKind.CharacterData, dom.Document.CreateTextNode("after"), oldValue: "before")));
        dom.Bool("r.type==='characterData' && r.oldValue==='before' && r.attributeName===null && r.attributeNamespace===null").Should().BeTrue();
    }
}
