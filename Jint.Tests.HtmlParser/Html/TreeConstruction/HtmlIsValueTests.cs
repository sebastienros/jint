#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // DOM §4.9 and HTML §13.2.6.4: element creation uses the token's is value.
    [TestCase("<div is='A&amp;B'></div>", "A&B", "A&B")]
    [TestCase("<div IS=MiXeD></div>", "MiXeD", "MiXeD")]
    [TestCase("<div is=''></div>", "", "")]
    [TestCase("<div></div>", null, null)]
    [TestCase("<div is=first IS=second></div>", "first", "first")]
    [TestCase("<div is='not a name'></div>", "not a name", "not a name")]
    public void StartTokenKeepsDecodedFirstAcceptedIsValueAtEveryQuota(
        string source, string? expectedSlot, string? expectedAttribute)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var element = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
            element.IsValue.Should().Be(expectedSlot);
            element.GetAttribute("is").Should().Be(expectedAttribute);
        }
    }

    [Test]
    public void SplitTokenInputKeepsTheSameCreationValue()
    {
        const string source = "<html is='root'><head is='head'></head><body is='body'><div IS='A&amp;B' is=ignored></div>";
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            DrainToNeedInput(session, 1);
            session.AppendInput(source[split..], isFinal: true);
            DrainToCompletion(session, 1);
            var html = document.DocumentElement!;
            var head = (Element) html.FirstChild!;
            var body = (Element) html.LastChild!;
            html.IsValue.Should().Be("root", $"split {split}");
            head.IsValue.Should().Be("head", $"split {split}");
            body.IsValue.Should().Be("body", $"split {split}");
            ((Element) body.FirstChild!).IsValue.Should().Be("A&B", $"split {split}");
        }
    }

    [Test]
    public void ImpliedRootsStayNullWhenLaterTokensMergeIsAttributes()
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse("<p>x</p><html is=late-root><body is=late-body>", quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var html = parsed.Document.DocumentElement!;
            var head = (Element) html.FirstChild!;
            var body = (Element) html.LastChild!;
            html.IsValue.Should().BeNull();
            head.IsValue.Should().BeNull();
            body.IsValue.Should().BeNull();
            html.GetAttribute("is").Should().Be("late-root");
            body.GetAttribute("is").Should().Be("late-body");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReconstructionUsesSavedCreationValueAndAttributesAfterLiveEdits(bool removeIs)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput("<p><b is='A&amp;B' data-x=original>x</p>");
            DrainToNeedInput(session, quota);
            var body = (Element) document.DocumentElement!.LastChild!;
            var original = (Element) body.FirstChild!.FirstChild!;
            original.IsValue.Should().Be("A&B");
            if (removeIs) original.RemoveAttribute("is");
            else original.SetAttribute("is", "changed");
            original.SetAttribute("data-x", "changed");
            session.AppendInput("y", isFinal: true);
            DrainToCompletion(session, quota);
            var recreated = (Element) body.LastChild!;
            recreated.Should().NotBeSameAs(original);
            recreated.IsValue.Should().Be("A&B");
            recreated.GetAttribute("is").Should().Be("A&B");
            recreated.GetAttribute("data-x").Should().Be("original");
            original.GetAttribute("is").Should().Be(removeIs ? null : "changed");
        }
    }

    [Test]
    public void AdoptionKeepsBothInnerAndFinalReplacementCreationValuesAfterAttributeEdits()
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput("<b is=outer data-x=old-outer><i is=inner data-x=old-inner><p>x");
            DrainToNeedInput(session, quota);
            var body = (Element) document.DocumentElement!.LastChild!;
            var oldB = (Element) body.FirstChild!;
            var oldI = (Element) oldB.FirstChild!;
            oldB.SetAttribute("is", "changed-outer");
            oldB.SetAttribute("data-x", "changed-outer");
            oldI.RemoveAttribute("is");
            oldI.SetAttribute("data-x", "changed-inner");
            session.AppendInput("</b>y", isFinal: true);
            DrainToCompletion(session, quota);

            var replacements = Descendants(body).Where(element =>
                !ReferenceEquals(element, oldB) && !ReferenceEquals(element, oldI)).ToArray();
            var inner = replacements.Single(element => element.LocalName == "i");
            var outer = replacements.Single(element => element.LocalName == "b");
            inner.IsValue.Should().Be("inner");
            inner.GetAttribute("is").Should().Be("inner");
            inner.GetAttribute("data-x").Should().Be("old-inner");
            outer.IsValue.Should().Be("outer");
            outer.GetAttribute("is").Should().Be("outer");
            outer.GetAttribute("data-x").Should().Be("old-outer");
            oldB.IsValue.Should().Be("outer");
            oldI.IsValue.Should().Be("inner");
        }
    }

    [Test]
    public void RelocatedInnerReplacementUsesTheCommonAncestorsOwnerAndSavedIsValue()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<b><i is=creation><p>x");
        DrainToNeedInput(session, 1);
        var body = (Element) document.DocumentElement!.LastChild!;
        var oldI = (Element) body.FirstChild!.FirstChild!;
        var paragraph = (Element) oldI.FirstChild!;
        var foreignOwner = Document.CreateHtml();
        foreignOwner.AdoptNode(oldI);
        oldI.SetAttribute("is", "changed");

        session.AppendInput("</b>", isFinal: true);
        DrainToCompletion(session, 1);
        var replacement = Descendants(body).Single(element => element.LocalName == "i");
        replacement.Should().NotBeSameAs(oldI);
        replacement.OwnerDocument.Should().BeSameAs(document);
        replacement.IsValue.Should().Be("creation");
        replacement.GetAttribute("is").Should().Be("creation");
        replacement.FirstChild.Should().BeSameAs(paragraph);
        paragraph.OwnerDocument.Should().BeSameAs(document);
        oldI.OwnerDocument.Should().BeSameAs(foreignOwner);
        oldI.IsValue.Should().Be("creation");
    }

    private static IEnumerable<Element> Descendants(Node root)
    {
        foreach (var child in root.ChildNodes)
        {
            if (child is Element element) yield return element;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
