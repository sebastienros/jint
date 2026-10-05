using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public sealed class FragmentMutationWorkTests
{
    [TestCase(128)]
    [TestCase(256)]
    [TestCase(512)]
    public void ObservedOwnerDoesNotScanEveryDetachedAncestor(int depth)
    {
        var document = Document.CreateHtml();
        using var observer = document.ObserveMutations(document, new MutationObserverOptions { ChildList = true, Subtree = true });
        var visits = 0;
        document.MutationAncestorVisited = () => visits++;
        var fragment = HtmlParserSession.ParseFragment(string.Concat(Enumerable.Repeat("<div>", depth)), document.CreateElement("div"));
        visits.Should().BeLessThanOrEqualTo(3 * depth);
        observer.TakeRecords().Should().BeEmpty();
        Node node = fragment;
        for (var i = 0; i < depth; i++)
        {
            node.ChildCount.Should().Be(1);
            node = node.FirstChild!;
            node.OwnerDocument.Should().BeSameAs(document);
        }
        node.ChildCount.Should().Be(0);
        document.AppendChild(document.CreateElement("html"));
        document.DocumentElement!.AppendChild(fragment);
        observer.TakeRecords().Count.Should().Be(2);
    }

    [Test]
    public void RegistrationAddedBetweenParserSlicesRetiresNegativeAncestry()
    {
        var document = Document.CreateHtml();
        using var unrelated = document.ObserveMutations(document, new MutationObserverOptions { ChildList = true, Subtree = true });
        var session = HtmlParserSession.CreateFragment(document.CreateElement("div"));
        session.AppendInput("<div><div>");
        while (session.Drive(4096, default).Kind == HtmlParseStepKind.Yielded) { }
        using var observer = document.ObserveMutations(session.Fragment!, new MutationObserverOptions { ChildList = true, Subtree = true });
        session.AppendInput("<span>x</span>", isFinal: true);
        while (session.Drive(4096, default).Kind == HtmlParseStepKind.Yielded) { }
        var records = observer.TakeRecords();
        records.Count.Should().Be(2);
        records[0].AddedNodes[0].Should().BeOfType<Element>().Which.LocalName.Should().Be("span");
        records[1].AddedNodes[0].Should().BeOfType<Text>().Which.Data.Should().Be("x");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReparentingAndAdoptionRetireNegativeAncestry(bool adopt)
    {
        var document = Document.CreateHtml();
        var destination = adopt ? Document.CreateHtml() : document;
        var host = destination.CreateElement("div");
        using var observer = destination.ObserveMutations(host, new MutationObserverOptions { ChildList = true, Subtree = true, Attributes = true });
        using var unrelated = document.ObserveMutations(document, new MutationObserverOptions { ChildList = true });
        var fragment = HtmlParserSession.ParseFragment("<div><div>", document.CreateElement("div"));
        var deepest = fragment.FirstChild!.FirstChild!;
        host.AppendChild(fragment);
        observer.TakeRecords();
        deepest.AppendParsedChild(destination.CreateElement("span"));
        ((Element) deepest).SetAttribute("x", "y");
        observer.TakeRecords().Count.Should().Be(2);
    }

    [Test]
    public void UninterestedRegistrationDoesNotBecomeARegistrationFreeWitness()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        using var observer = document.ObserveMutations(root, new MutationObserverOptions { Attributes = true, Subtree = true });
        root.AppendParsedChild(document.CreateElement("div"));
        ((Element) root.FirstChild!).SetAttribute("x", "y");
        observer.TakeRecords().Count.Should().Be(1);
    }
}
