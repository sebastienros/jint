using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeInputListBindingTests
{
    [Test]
    public void AssociationUsesTheFirstIdAndCurrentTypeWithoutCreatingValueState()
    {
        using var dom = DomTestFixture.Create("<input id=i list=choices><div id=choices></div><datalist id=choices></datalist>");
        var input = ContentDom.ElementById(dom.Document, "i")!;
        dom.Execute("var i=document.getElementById('i'), first=document.getElementById('choices'), choices=document.querySelector('datalist');");
        dom.Bool("i.list===null").Should().BeTrue();
        dom.Execute("first.remove();");
        dom.Bool("i.list===choices").Should().BeTrue();
        input.ExistingInputValueState.Should().BeNull();
        dom.Execute("i.type='password';");
        dom.Bool("i.list===null").Should().BeTrue();
        dom.Execute("i.type='range';");
        dom.Bool("i.list===choices").Should().BeTrue();
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void AssociationIsScopedToTheOrdinaryShadowTreeAndIncludesItsRoot()
    {
        var document = Document.CreateHtml();
        var datalist = document.CreateElement("datalist");
        datalist.SetAttribute("id", "choices");
        var input = document.CreateElement("input");
        input.SetAttribute("list", "choices");
        datalist.AppendChild(input);
        DomInputMembers.List(input, null, default).Should().BeSameAs(datalist);
        var host = document.CreateElement("div");
        var shadow = ShadowTree.Attach(host, new(ShadowRootMode.Open), default);
        shadow.AppendChild(input);
        DomInputMembers.List(input, null, default).Should().BeNull();
        shadow.AppendChild(datalist);
        DomInputMembers.List(input, null, default).Should().BeSameAs(datalist);
    }

    [Test]
    public void AssociationCancelsDuringTheNativeTreeScan()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var input = document.CreateElement("input");
        input.SetAttribute("list", "missing");
        root.AppendChild(input);
        for (var index = 0; index < 1024; index++) root.AppendChild(document.CreateElement("span"));
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        var cancellationUnits = 0;
        Assert.Throws<OperationCanceledException>(() => DomInputMembers.List(input,
            units =>
            {
                if (++checkpoints != 2) return;
                cancellationUnits = units;
                cancellation.Cancel();
            }, cancellation.Token));
        checkpoints.Should().Be(2);
        cancellationUnits.Should().BeGreaterThan(0);
        input.ExistingInputValueState.Should().BeNull();
    }
}
