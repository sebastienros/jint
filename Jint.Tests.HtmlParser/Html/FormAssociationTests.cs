#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class FormAssociationTests
{
    [Test]
    public void CategoriesAndStableViewUseExactHtmlElementType()
    {
        var document = Document.CreateHtml();
        foreach (var name in new[] { "button", "fieldset", "input", "object", "output", "select", "textarea" })
        {
            var element = document.CreateElement(name);
            HtmlFormState.IsFormAssociated(element).Should().BeTrue();
            HtmlFormState.IsListed(element).Should().BeTrue();
            element.GetHtmlState().Should().BeSameAs(element.GetHtmlState());
            element.GetHtmlState()!.FormOwner.Should().BeNull();
        }

        var image = document.CreateElement("img");
        HtmlFormState.IsFormAssociated(image).Should().BeTrue();
        HtmlFormState.IsListed(image).Should().BeFalse();
        foreach (var name in new[] { "option", "optgroup", "label", "legend", "keygen", "x-widget", "div" })
        {
            var element = document.CreateElement(name);
            HtmlFormState.IsFormAssociated(element).Should().BeFalse();
            HtmlFormState.IsListed(element).Should().BeFalse();
            HtmlFormState.GetOwner(element).Should().BeNull();
        }

        var svg = document.CreateElementNS(Namespaces.Svg, "input");
        svg.GetHtmlState().Should().BeNull();
        HtmlFormState.IsFormAssociated(svg).Should().BeFalse();
        var xml = Document.CreateXml().CreateElementNS(Namespaces.Html, "Input");
        HtmlFormState.IsFormAssociated(xml).Should().BeFalse();
        Document.CreateXml().CreateElementNS(Namespaces.Html, "input").GetHtmlState().Should().NotBeNull();
        Assert.Throws<InvalidOperationException>(() => HtmlFormState.ResetOwner(document.CreateElement("div")));
        Assert.Throws<ArgumentNullException>(() => HtmlFormState.GetOwner(null!));
    }

    [Test]
    public void AncestorsAndExplicitIdsUseFirstMatchInOrdinaryTree()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var outer = document.CreateElement("form");
        outer.SetAttribute("id", "target");
        root.AppendChild(outer);
        var inner = document.CreateElement("form");
        outer.AppendChild(inner);
        var control = document.CreateElement("input");
        inner.AppendChild(control);
        HtmlFormState.GetOwner(control).Should().BeSameAs(inner);

        control.SetAttribute("form", "target");
        HtmlFormState.GetOwner(control).Should().BeSameAs(outer);
        var blocker = document.CreateElement("div");
        blocker.SetAttribute("id", "target");
        root.InsertBefore(blocker, outer);
        HtmlFormState.GetOwner(control).Should().BeNull();
        root.RemoveChild(blocker);
        HtmlFormState.GetOwner(control).Should().BeSameAs(outer);
        control.SetAttribute("form", "");
        HtmlFormState.GetOwner(control).Should().BeNull();
        control.RemoveAttribute("form");
        HtmlFormState.GetOwner(control).Should().BeSameAs(inner);

        var image = document.CreateElement("img");
        image.SetAttribute("form", "target");
        inner.AppendChild(image);
        HtmlFormState.GetOwner(image).Should().BeSameAs(inner);
    }

    [Test]
    public void AttachedAttributeAndIdReplacementUpdateExternalControlSynchronously()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "a");
        root.AppendChild(form);
        var control = document.CreateElement("input");
        root.AppendChild(control);
        var reference = document.CreateAttribute("form");
        reference.Value = "a";
        control.SetAttributeNode(reference);
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);

        var id = form.GetAttributeNode("id")!;
        id.Value = "b";
        HtmlFormState.GetOwner(control).Should().BeNull();
        reference.Value = "b";
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        var replacement = document.CreateAttribute("id");
        replacement.Value = "c";
        form.SetAttributeNode(replacement);
        HtmlFormState.GetOwner(control).Should().BeNull();
        reference.Value = "c";
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        form.RemoveAttributeNode(replacement);
        HtmlFormState.GetOwner(control).Should().BeNull();
    }

    [Test]
    public void DuplicateOrderAndSubtreeMovesUpdateOnlyTheAffectedReferences()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var first = document.CreateElement("form");
        first.SetAttribute("id", "same");
        root.AppendChild(first);
        var second = document.CreateElement("form");
        second.SetAttribute("id", "same");
        root.AppendChild(second);
        var control = document.CreateElement("input");
        control.SetAttribute("form", "same");
        root.AppendChild(control);
        HtmlFormState.GetOwner(control).Should().BeSameAs(first);
        root.InsertBefore(second, first);
        HtmlFormState.GetOwner(control).Should().BeSameAs(second);

        var wrapper = document.CreateElement("section");
        var moved = document.CreateElement("form");
        moved.SetAttribute("id", "same");
        wrapper.AppendChild(moved);
        root.InsertBefore(wrapper, second);
        HtmlFormState.GetOwner(control).Should().BeSameAs(moved);
        root.RemoveChild(wrapper);
        HtmlFormState.GetOwner(control).Should().BeSameAs(second);
        wrapper.AppendChild(control);
        HtmlFormState.GetOwner(control).Should().BeNull();
    }

    [Test]
    public void FragmentDrainAndFailedMutationKeepCoherentIntermediateOwnership()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var control = document.CreateElement("input");
        control.SetAttribute("form", "f");
        root.AppendChild(control);
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);

        var fragment = document.CreateDocumentFragment();
        var replacement = document.CreateElement("form");
        replacement.SetAttribute("id", "f");
        fragment.AppendChild(replacement);
        root.ReplaceChild(fragment, form);
        HtmlFormState.GetOwner(control).Should().BeSameAs(replacement);
        Assert.Throws<DomException>(() => root.AppendChild(root));
        HtmlFormState.GetOwner(control).Should().BeSameAs(replacement);
        root.ReplaceChildren(control);
        HtmlFormState.GetOwner(control).Should().BeNull();
    }

    [Test]
    public void DetachedFallbackAndShadowBoundaryAreIndependent()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var external = document.CreateElement("form");
        external.SetAttribute("id", "x");
        root.AppendChild(external);
        var detachedForm = document.CreateElement("form");
        var control = document.CreateElement("input");
        control.SetAttribute("form", "x");
        detachedForm.AppendChild(control);
        HtmlFormState.GetOwner(control).Should().BeSameAs(detachedForm);
        root.AppendChild(detachedForm);
        HtmlFormState.GetOwner(control).Should().BeSameAs(external);
        root.RemoveChild(detachedForm);
        HtmlFormState.GetOwner(control).Should().BeSameAs(detachedForm);

        var host = document.CreateElement("div");
        external.AppendChild(host);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var shadowForm = document.CreateElement("form");
        shadowForm.SetAttribute("id", "x");
        shadow.AppendChild(shadowForm);
        var shadowControl = document.CreateElement("input");
        shadowControl.SetAttribute("form", "x");
        shadow.AppendChild(shadowControl);
        HtmlFormState.GetOwner(shadowControl).Should().BeSameAs(shadowForm);
        shadow.RemoveChild(shadowForm);
        HtmlFormState.GetOwner(shadowControl).Should().BeNull();
        shadowControl.RemoveAttribute("form");
        HtmlFormState.GetOwner(shadowControl).Should().BeNull();
    }

    [Test]
    public void ParserAssociationSurvivesInsertionAndUnrelatedMutationsUntilReset()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        root.AppendChild(form);
        var control = document.CreateElement("input");
        HtmlFormState.AssociateFromParser(control, form);
        root.AppendParsedChild(control);
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        control.FormAssociationState!.ParserInserted.Should().BeTrue();
        control.SetAttribute("class", "changed");
        control.AppendChild(document.CreateTextNode("text"));
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        control.FormAssociationState.ParserInserted.Should().BeTrue();
        HtmlFormState.ResetOwner(control);
        HtmlFormState.GetOwner(control).Should().BeNull();
        control.FormAssociationState.ParserInserted.Should().BeFalse();
        Assert.Throws<InvalidOperationException>(() => HtmlFormState.AssociateFromParser(control, form));

        var explicitControl = document.CreateElement("input");
        explicitControl.SetAttribute("form", "f");
        Assert.Throws<InvalidOperationException>(() => HtmlFormState.AssociateFromParser(explicitControl, form));

        var image = document.CreateElement("img");
        HtmlFormState.AssociateFromParser(image, form);
        root.AppendChild(image);
        HtmlFormState.GetOwner(image).Should().BeSameAs(form);
        image.SetAttribute("form", "missing");
        HtmlFormState.GetOwner(image).Should().BeSameAs(form);
    }

    [Test]
    public void CloneImportAndAdoptUseFreshStateAndIntermediateTreeChanges()
    {
        var source = Document.CreateHtml();
        var root = source.CreateElement("main");
        source.AppendChild(root);
        var form = source.CreateElement("form");
        root.AppendChild(form);
        var control = source.CreateElement("input");
        form.AppendChild(control);
        var copy = (Element)form.CloneNode(true);
        var copyControl = (Element)copy.FirstChild!;
        HtmlFormState.GetOwner(copyControl).Should().BeSameAs(copy);
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);

        var other = Document.CreateHtml();
        var imported = (Element)other.ImportNode(form, true);
        HtmlFormState.GetOwner((Element)imported.FirstChild!).Should().BeSameAs(imported);
        other.AdoptNode(form);
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        other.AppendChild(form);
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        form.RemoveChild(control);
        HtmlFormState.GetOwner(control).Should().BeNull();
    }
}
