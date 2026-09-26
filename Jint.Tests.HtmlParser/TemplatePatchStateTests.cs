#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class TemplatePatchStateTests
{
    [Test]
    public void TemplatePatchIdentitySurvivesAdoptionButIsNotClonedOrImported()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var target = source.CreateElement("div");
        var start = source.CreateProcessingInstruction("start", "name='part'");
        var end = source.CreateProcessingInstruction("end", "");
        target.AppendChild(start);
        target.AppendChild(end);
        var template = source.CreateElement("template");
        HtmlTemplatePatchState.Install(template, target, start, end);
        var state = template.TemplatePatchState;
        state!.InsertionTarget.Should().BeSameAs(target);
        state.StartMarker.Should().BeSameAs(start);
        state.EndMarker.Should().BeSameAs(end);
        ((Element) template.CloneNode()).TemplatePatchState.Should().BeNull();
        ((Element) destination.ImportNode(template)).TemplatePatchState.Should().BeNull();
        destination.AdoptNode(template);
        template.TemplatePatchState.Should().BeSameAs(state);
        state.StartMarker.OwnerDocument.Should().BeSameAs(source);
        target.RemoveChild(end);
        state.EndMarker.Should().BeSameAs(end);
        state.EndMarker!.ParentNode.Should().BeNull();
    }

    [Test]
    public void SingletonAndOpenEndedTargetsUseTheActualFragmentIdentity()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var target = document.CreateDocumentFragment();
        var marker = document.CreateProcessingInstruction("marker", "name='part'");
        target.AppendChild(marker);
        HtmlTemplatePatchState.Install(template, target, marker, marker);
        template.TemplatePatchState!.InsertionTarget.Should().BeSameAs(target);
        template.TemplatePatchState.EndMarker.Should().BeSameAs(marker);
        HtmlTemplatePatchState.Install(template, target, marker, null);
        template.TemplatePatchState.EndMarker.Should().BeNull();
    }

    [Test]
    public void NonTemplatesAndMarkersOutsideTargetAreRejectedBeforeChangingSlots()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var target = document.CreateElement("div");
        var marker = document.CreateProcessingInstruction("marker", "name='part'");
        Assert.Throws<InvalidOperationException>(() => HtmlTemplatePatchState.Install(template, target, marker, null)).Should().BeOfType<InvalidOperationException>();
        template.TemplatePatchState.Should().BeNull();
        target.AppendChild(marker);
        Assert.Throws<InvalidOperationException>(() => HtmlTemplatePatchState.Install(target, target, marker, null)).Should().BeOfType<InvalidOperationException>();
        target.TemplatePatchState.Should().BeNull();
    }
}
