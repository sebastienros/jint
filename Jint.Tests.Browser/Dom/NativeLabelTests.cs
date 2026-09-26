#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeLabelTests
{
    [Test]
    public void LabelsFollowDuplicateIdsAndImplicitAssociationOnTheNativeTree()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var first = document.CreateElement("input");
        first.SetAttribute("id", "control");
        root.AppendChild(first);
        var second = document.CreateElement("input");
        second.SetAttribute("id", "control");
        root.AppendChild(second);
        var explicitLabel = document.CreateElement("label");
        explicitLabel.SetAttribute("for", "control");
        root.AppendChild(explicitLabel);
        var implicitLabel = document.CreateElement("label");
        root.AppendChild(implicitLabel);
        implicitLabel.AppendChild(first);

        HtmlLabelAssociation.LabelsFor(second).Should().Equal(explicitLabel);
        HtmlLabelAssociation.LabelsFor(first).Should().Equal(implicitLabel);
        root.InsertBefore(first, second);
        HtmlLabelAssociation.LabelsFor(first).Should().Equal(explicitLabel);
        first.SetAttribute("type", "HiDdEn");
        HtmlLabelAssociation.LabelsFor(first).Should().BeEmpty();
        HtmlLabelAssociation.ControlFor(explicitLabel).Should().BeNull();
    }

    [Test]
    public void LabelLookupChecksWhileScanningAnInputsAttributes()
    {
        var document = Document.CreateHtml();
        var label = document.CreateElement("label");
        var input = document.CreateElement("input");
        for (var i = 0; i < 4096; i++) input.SetAttribute("data-" + i, "x");
        label.AppendChild(input);
        var checks = 0;
        var failure = Caught.Exception(() => HtmlLabelAssociation.ControlFor(label, _ =>
        {
            if (++checks == 3) throw new ReadStopped();
        }));
        failure.Should().BeOfType<ReadStopped>();
        checks.Should().Be(3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LabelListReadsCarryTheInvocationCheckpoint(bool indexed)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var input = document.CreateElement("input");
        input.SetAttribute("id", new string('a', 8192));
        root.AppendChild(input);
        var label = document.CreateElement("label");
        label.SetAttribute("for", new string('a', 8192));
        root.AppendChild(label);
        var list = new DomLabelNodeList(input);
        var checks = 0;
        void Check(int _) { if (++checks == 3) throw new ReadStopped(); }
        var failure = Caught.Exception(() =>
        {
            if (indexed) _ = list.ReadItem(0, Check, default);
            else _ = list.ReadLength(Check, default);
        });
        failure.Should().BeOfType<ReadStopped>();
        checks.Should().Be(3);
    }

    private sealed class ReadStopped : Exception { }
}
