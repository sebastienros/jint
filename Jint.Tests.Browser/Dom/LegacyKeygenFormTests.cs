#nullable enable
using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class LegacyKeygenFormTests
{
    [Test]
    public void AnExplicitFormUsesTheFirstIdAndRequiresAnHtmlForm()
    {
        using var dom = DomTestFixture.Create("<div id=f></div><form id=f></form><form id=ancestor><keygen id=k form=f></form>");
        var keygen = ContentDom.ElementById(dom.Document, "k")!;
        var first = ContentDom.ElementById(dom.Document, "f")!;
        var form = first.NextSibling!;
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeNull();
        first.ParentNode!.RemoveChild(first);
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeSameAs(form);
        keygen.SetAttribute("form", "missing");
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeNull();
        keygen.SetAttribute("form", "");
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeNull();
        keygen.RemoveAttribute("form");
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeSameAs(keygen.ParentNode);
    }

    [Test]
    public void DetachedAndAdoptedKeygenUsesTheCurrentTreeWithoutNativeFormState()
    {
        var document = Document.CreateHtml();
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        var keygen = document.CreateElement("keygen");
        form.AppendChild(keygen);
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeSameAs(form);
        keygen.SetAttribute("form", "f");
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeNull();
        document.AppendChild(form);
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeSameAs(form);

        var destination = Document.CreateHtml();
        destination.AdoptNode(keygen);
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeNull();
        var other = destination.CreateElement("form");
        other.SetAttribute("id", "f");
        destination.AppendChild(other);
        other.AppendChild(keygen);
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeSameAs(other);
        HtmlFormOwner.FormIdlOf(keygen).Should().BeSameAs(other);
        HtmlFormOwner.IsFormAssociated(keygen).Should().BeFalse();
        HtmlFormOwner.IsListed(keygen).Should().BeFalse();
    }

    [Test]
    public void ForeignAttributesAndForeignFormElementsCannotSupplyAnOwner()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var foreign = document.CreateElementNS("urn:foreign", "form");
        foreign.SetAttribute("id", "f");
        root.AppendChild(foreign);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var keygen = document.CreateElement("keygen");
        keygen.SetAttribute("form", "f");
        form.AppendChild(keygen);
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeNull();
        foreign.RemoveAttribute("id");
        foreign.SetAttributeNS("urn:foreign", "id", "f");
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeSameAs(form);
        keygen.RemoveAttribute("form");
        keygen.SetAttributeNS("urn:foreign", "form", "missing");
        DomLegacyKeygenForm.Of(keygen, null, default).Should().BeSameAs(form);
        var foreignKeygen = document.CreateElementNS("urn:foreign", "keygen");
        form.AppendChild(foreignKeygen);
        DomLegacyKeygenForm.Of(foreignKeygen, null, default).Should().BeNull();
    }

    [Test]
    public void AnExplicitFormLookupChecksWorkAndCancellation()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        for (var i = 0; i < 2048; i++) root.AppendChild(document.CreateElement("div"));
        var keygen = document.CreateElement("keygen");
        keygen.SetAttribute("form", "missing");
        root.AppendChild(keygen);
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        Caught.Exception(() => DomLegacyKeygenForm.Of(keygen, _ =>
        {
            if (++checks == 2) cancellation.Cancel();
        }, cancellation.Token)).Should().BeOfType<OperationCanceledException>();
        checks.Should().Be(2);
    }
}
