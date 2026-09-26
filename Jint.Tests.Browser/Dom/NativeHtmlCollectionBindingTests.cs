using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeHtmlCollectionBindingTests
{
    [Test]
    public void DatalistAndMapCollectionsPreserveIdentityAndTrackNativeDescendants()
    {
        using var dom = DomTestFixture.Create("<datalist id=d><option id=a></option><div><option id=b></option></div></datalist><map id=m><area id=c></map>");
        dom.Execute("var d=document.getElementById('d'), m=document.getElementById('m'), options=d.options, areas=m.areas, b=document.getElementById('b');");
        dom.Bool("options===d.options && options instanceof HTMLCollection && options.length===2 && options[1]===b && areas===m.areas && areas.length===1").Should().BeTrue();
        ContentDom.ElementById(dom.Document, "a")!.ExistingOptionState.Should().BeNull();
        ContentDom.ElementById(dom.Document, "b")!.ExistingOptionState.Should().BeNull();
        dom.Execute("d.appendChild(document.createElement('option')); b.remove(); m.appendChild(document.createElement('area'));");
        dom.Bool("options.length===2 && options[1]!==b && areas.length===2").Should().BeTrue();
    }

    [Test]
    public void FieldsetCollectionIncludesListedDescendantsIndependentlyOfFormOwner()
    {
        using var dom = DomTestFixture.Create("<form id=f></form><fieldset id=s><input id=i type=image form=f><div><output id=o></output></div><fieldset id=n></fieldset><img></fieldset>");
        dom.Execute("var s=document.getElementById('s'), elements=s.elements, i=document.getElementById('i'), o=document.getElementById('o'), n=document.getElementById('n');");
        dom.Bool("elements===s.elements && elements instanceof HTMLCollection && elements.length===3 && elements[0]===i && elements[1]===o && elements[2]===n && s.type==='fieldset' && o.type==='output'").Should().BeTrue();
        dom.Execute("n.appendChild(document.createElement('textarea')); i.remove();");
        dom.Bool("elements.length===3 && elements[0]===o && elements[2].localName==='textarea'").Should().BeTrue();
    }
}
