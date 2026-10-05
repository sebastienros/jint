#nullable enable
using System.Xml.XPath;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Views;

public sealed class XPathAncestorWorkTests
{
    [TestCase("//*[ancestor::r]", true)]
    [TestCase("//*[ancestor::missing]", false)]
    public void BrowserAncestorPredicatesHaveLinearConstraintWork(string source, bool matches)
    {
        var expression = NativeXPath.Compile(source);
        int Measure(int depth)
        {
            var document = DeepTree(depth);
            var probe = new ReadProbe();
            using var engine = new Engine(options => options.AddConstraint(probe));
            var navigator = new BrowserXPathNavigator(DomRealm.Of(engine), new DomNodeIdentity(document));
            probe.Checks = 0;
            var nodes = (XPathNodeIterator) expression.EvaluatePrepared(navigator.Evaluate);
            var count = 0;
            while (nodes.MoveNext()) count++;
            navigator.PublishResult();
            count.Should().Be(matches ? depth : 0);
            return probe.Checks;
        }
        var small = Measure(512);
        var medium = Measure(1024);
        var large = Measure(2048);
        small.Should().BeGreaterThan(0);
        medium.Should().BeLessThan(small * 5 / 2);
        large.Should().BeLessThan(medium * 5 / 2);
        large.Should().BeLessThan(200);
    }

    [Test]
    public void BrowserLazyPredicateKeepsHostBudgetFailureFatal()
    {
        var document = DeepTree(2048);
        Node leaf = document.DocumentElement!;
        while (leaf.FirstChild is { } child) leaf = child;
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var navigator = new BrowserXPathNavigator(DomRealm.Of(engine), new DomNodeIdentity(leaf.ParentNode!));
        var expression = NativeXPath.Compile("child::*[ancestor::missing]");
        var failure = new NotSupportedException("host budget exhausted");
        probe.OnCheck = () => throw failure;
        var error = Assert.Throws<NotSupportedException>(() =>
        {
            var nodes = (XPathNodeIterator) expression.EvaluatePrepared(navigator.Evaluate);
            while (nodes.MoveNext()) { }
        });
        error.Should().BeSameAs(failure);
    }

    private static Document DeepTree(int depth)
        => MarkupParser.ParseXml("<r>" + string.Concat(Enumerable.Repeat("<x>", depth)) + string.Concat(Enumerable.Repeat("</x>", depth)) + "</r>");

    private sealed class ReadProbe : Constraint
    {
        internal int Checks;
        internal Action? OnCheck;
        public override void Check()
        {
            Checks++;
            OnCheck?.Invoke();
        }
        public override void Reset() { }
    }
}
