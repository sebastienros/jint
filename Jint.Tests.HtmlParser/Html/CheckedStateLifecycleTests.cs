#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

public class CheckedStateLifecycleTests
{
    private static Element Radio(Document document, bool check = true)
    {
        var element = document.CreateElement("input");
        element.InitializeParsedAttributes(new[] { new ParserAttribute(null, "checked", null, ""),
            new ParserAttribute(null, "name", null, "g"), new ParserAttribute(null, "type", null, "radio") }, default);
        if (!check) HtmlCheckednessAlgorithms.Set(element, false, HtmlCheckedChangeOrigin.Algorithm, default);
        return element;
    }
    private static HtmlInputCheckedState State(Element element) => HtmlCheckableState.Get(element)!;

    [Test]
    public void RealHtmlParserBatchesDuplicateAttributesBeforeConnectedInsertion()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<form><input checked name=g type=radio checked='ignored'><input type=RADIO checked name=g></form>", isFinal: true);
        HtmlParseStep step;
        var turns = 0;
        do
        {
            step = session.Drive(64, default);
            (++turns).Should().BeLessThan(1000);
        } while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var form = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var a = (Element) form.FirstChild!;
        var b = (Element) form.LastChild!;
        State(a).Checked.Should().BeFalse();
        State(b).Checked.Should().BeTrue();
        State(a).DirtyCheckedness.Should().BeFalse();
        a.GetAttribute("checked").Should().Be("");
        HtmlFormState.GetOwner(a).Should().BeSameAs(form);
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(2);
    }

    [Test]
    public void XmlDtdDefaultsInitializeTheCompleteEffectiveAttributeBatch()
    {
        var document = XmlTreeParser.ParseDocument("<!DOCTYPE root [<!ATTLIST input checked CDATA 'false' name CDATA 'g' type CDATA 'radio'>]>" +
            "<root xmlns='http://www.w3.org/1999/xhtml'><input/><input type='RADIO'/></root>", ParseLimits.Unbounded, default);
        var a = (Element) document.DocumentElement!.FirstChild!;
        var b = (Element) document.DocumentElement.LastChild!;
        State(a).DefaultChecked.Should().BeTrue();
        State(a).DirtyCheckedness.Should().BeFalse();
        State(a).Checked.Should().BeFalse();
        State(b).Checked.Should().BeTrue();
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(2);
    }

    [Test]
    public void ParserOnlyOwnerParticipatesAndCloneDoesNotCopyIt()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        root.AppendChild(form);
        var a = Radio(document);
        HtmlFormState.AssociateFromParser(a, form);
        root.AppendParsedChild(a);
        var b = Radio(document);
        HtmlFormState.AssociateFromParser(b, form);
        root.AppendParsedChild(b);
        HtmlFormState.GetOwner(a).Should().BeSameAs(form);
        State(a).Checked.Should().BeFalse();
        State(b).Checked.Should().BeTrue();
        HtmlFormState.GetOwner((Element) b.CloneNode()).Should().BeNull();
        var other = Document.CreateHtml();
        other.AdoptNode(b);
        State(b).Checked.Should().BeTrue();
        HtmlFormState.GetOwner(b).Should().BeNull();
        HtmlCheckableState.SameRadioGroup(a, b, default).Should().BeFalse();
    }

    [Test]
    public void FragmentTemplateAndShadowRootsRemainSeparate()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var light = Radio(document);
        host.AppendChild(light);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var nestedHost = document.CreateElement("div");
        shadow.AppendChild(nestedHost);
        var nested = ShadowTree.Attach(nestedHost, new ShadowRootInit(ShadowRootMode.Closed), default);
        var a = Radio(document);
        var b = Radio(document);
        shadow.AppendChild(a);
        shadow.AppendChild(b);
        var inner = Radio(document);
        nested.AppendChild(inner);
        var template = document.CreateElement("template");
        var templated = Radio(template.TemplateContent!.OwnerDocument!);
        template.TemplateContent.AppendChild(templated);
        host.AppendChild(template);
        HtmlCheckableState.GetRadioGroupFacts(a, default).CheckedCount.Should().Be(2);
        document.AppendChild(host);
        State(a).Checked.Should().BeTrue();
        State(b).Checked.Should().BeFalse();
        State(inner).Checked.Should().BeTrue();
        State(light).Checked.Should().BeTrue();
        State(templated).Checked.Should().BeTrue();
        HtmlCheckableState.SameRadioGroup(light, a, default).Should().BeFalse();
        HtmlCheckableState.SameRadioGroup(a, inner, default).Should().BeFalse();
        var fragment = document.CreateDocumentFragment();
        var f1 = Radio(document);
        var f2 = Radio(document);
        fragment.AppendChild(f1);
        fragment.AppendChild(f2);
        HtmlCheckableState.GetRadioGroupFacts(f1, default).CheckedCount.Should().Be(2);
        host.AppendChild(fragment);
        State(f1).Checked.Should().BeFalse();
        State(f2).Checked.Should().BeTrue();
        State(light).Checked.Should().BeFalse();
        fragment.RadioIndex!.RegisteredCount.Should().Be(0);
    }

    [Test]
    public void EqualTypeDoesNotExcludeAndResetOrderUsesLastTriggeredDefault()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document);
        var b = Radio(document);
        root.AppendChild(a);
        root.AppendChild(b);
        b.SetAttribute("type", "RADIO");
        State(a).Checked.Should().BeTrue();
        State(b).Checked.Should().BeTrue();
        b.SetAttribute("type", "text");
        b.SetAttribute("type", "radio");
        State(a).Checked.Should().BeFalse();
        State(b).Checked.Should().BeTrue();
        HtmlCheckednessAlgorithms.ResetCheckedness(a, default);
        State(a).Checked.Should().BeTrue();
        State(b).Checked.Should().BeFalse();
        HtmlCheckednessAlgorithms.ResetCheckedness(b, default);
        State(a).Checked.Should().BeFalse();
        State(b).Checked.Should().BeTrue();
        State(a).DefaultChecked.Should().BeTrue();
        State(b).DefaultChecked.Should().BeTrue();
    }

    [Test]
    public void IdResetCoversAllListedCandidatesAndPublishesBothOwnerStores()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var a = Radio(document);
        a.SetAttribute("form", "f");
        root.AppendChild(a);
        var b = Radio(document);
        root.AppendChild(b);
        foreach (var name in new[] { "button", "fieldset", "object", "output", "select", "textarea" })
        {
            var control = document.CreateElement(name);
            control.SetAttribute("form", "");
            root.AppendChild(control);
        }
        var unrelated = document.CreateElement("div");
        unrelated.SetAttribute("id", "x");
        root.AppendChild(unrelated);
        State(b).SetChecked(true, default);
        var stores = new List<(Element? Old, Element? New)>();
        document.CheckedWorkProbe = new HtmlCheckedWorkProbe
        {
            OwnerStore = (element, oldOwner, newOwner) => { if (ReferenceEquals(element, a)) stores.Add((oldOwner, newOwner)); }
        };
        using var probe = new HtmlFormWorkProbe(document);
        unrelated.SetAttribute("id", "y");
        probe.ResetCandidates.Should().Be(7);
        stores.Should().Equal((form, null), (null, form));
        State(b).Checked.Should().BeFalse();
        var visits = probe.ResetCandidates;
        unrelated.SetAttribute("class", "x");
        probe.ResetCandidates.Should().Be(visits);
    }

    [Test]
    public void DuplicateIdsAndUnsuccessfulInsertionLeaveAccurateMembership()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var a = Radio(document);
        a.SetAttribute("form", "f");
        root.AppendChild(a);
        var blocker = document.CreateElement("div");
        blocker.SetAttribute("id", "f");
        root.InsertBefore(blocker, form);
        HtmlFormState.GetOwner(a).Should().BeNull();
        root.RemoveChild(blocker);
        HtmlFormState.GetOwner(a).Should().BeSameAs(form);
        var before = document.MutationStamp;
        Assert.Throws<DomException>(() => a.AppendChild(root));
        document.MutationStamp.Should().Be(before);
        State(a).Checked.Should().BeTrue();
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(1);
        var clone = (Document) document.CloneNode(true);
        var clonedRoot = clone.DocumentElement!;
        var clonedInput = (Element) clonedRoot.LastChild!;
        State(clonedInput).Checked.Should().BeTrue();
        HtmlFormState.GetOwner(clonedInput).Should().BeSameAs(clonedRoot.FirstChild);
        root.ReplaceChildren();
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(1);
    }
}
