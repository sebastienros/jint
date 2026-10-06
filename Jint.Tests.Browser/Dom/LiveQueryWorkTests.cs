#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class LiveQueryWorkTests
{
    [TestCase(512, false)]
    [TestCase(1024, false)]
    [TestCase(512, true)]
    [TestCase(1024, true)]
    public void SequentialIndexedReadsRequireLinearMatchingWork(int size, bool reverse)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < size; i++)
        {
            root.AppendChild(document.CreateComment("gap"));
            root.AppendChild(document.CreateElement("span"));
        }
        var filter = new CountingFilter();
        var collection = new DomLiveHtmlCollection(root, filter);
        using var engine = new Engine();
        var realm = DomRealm.Of(engine);
        for (var i = 0; i < size; i++)
            collection.GetItem(realm, (uint) (reverse ? size - i - 1 : i)).Should().NotBeNull();
        filter.Count.Should().BeLessThanOrEqualTo(2 * size);
        filter.Count = 0;
        collection.Read(realm).Count().Should().Be(size);
        filter.Count.Should().Be(size);
    }

    [Test]
    public void WarmCursorObservesReorderRemovalAndAdoption()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var first = document.CreateElement("span");
        var second = document.CreateElement("span");
        root.AppendChild(first);
        root.AppendChild(second);
        var collection = new DomLiveHtmlCollection(root, new CountingFilter());
        using var engine = new Engine();
        var realm = DomRealm.Of(engine);
        collection.GetItem(realm, 1).Should().BeSameAs(second);
        root.AppendChild(first);
        collection.GetItem(realm, 1).Should().BeSameAs(first);
        Document.CreateHtml().AdoptNode(root);
        root.RemoveChild(second);
        collection.GetItem(realm, 0).Should().BeSameAs(first);
        collection.GetItem(realm, 1).Should().BeNull();
    }

    [Test]
    public void WarmCursorAndEmptyQueriesStillCheckHostConstraints()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        root.AppendChild(document.CreateElement("span"));
        var probe = new Probe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var collection = new DomLiveHtmlCollection(root, new CountingFilter());
        collection.GetItem(realm, 0).Should().NotBeNull();
        probe.Fail = true;
        Assert.Throws<OperationCanceledException>(() => collection.GetItem(realm, 0));
        Assert.Throws<OperationCanceledException>(() => new DomLiveHtmlCollection(root, DomElementFilter.None).GetItem(realm, 0));
    }

    [Test]
    public void CheckpointMutationInvalidatesTraversal()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        root.AppendChild(document.CreateElement("span"));
        var probe = new Probe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        probe.Callback = () => root.AppendChild(document.CreateComment("changed"));
        var collection = new DomLiveHtmlCollection(root, new CountingFilter());
        Assert.Throws<InvalidOperationException>(() => collection.GetItem(DomRealm.Of(engine), 0));
    }

    [TestCase("")]
    [TestCase("<!doctype html>")]
    public void ClassTokensAcrossWorkChunksKeepBoundariesAndLiveOrder(string doctype)
    {
        using var fixture = DomTestFixture.Create(doctype + "<main id=root></main>");
        fixture.Execute("""
            var root = document.getElementById('root');
            var longClass = 'x'.repeat(600);
            root.innerHTML = '<span id=first></span><div><span id=second></span></div>';
            var first = document.getElementById('first'), second = document.getElementById('second');
            first.className = ' '.repeat(255) + longClass + ' A';
            second.className = longClass + ' A';
            var items = root.getElementsByClassName(longClass + ' A');
            """);
        fixture.Bool("items[1] === second && items[0] === first && items.namedItem('second') === second").Should().BeTrue();
        fixture.Execute("root.insertBefore(second, first); first.className += 'suffix'");
        fixture.Bool("items.length === 1 && items[0] === second && [...items][0] === second").Should().BeTrue();
        fixture.Execute("first.className = longClass + ' A'; second.remove()");
        fixture.Bool("items.length === 1 && items.item(0) === first && items.namedItem('second') === null").Should().BeTrue();
    }

    [Test]
    public void XmlQualifiedNamesAndForeignCaseSurviveWarmReads()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute("""
            var xml = document.implementation.createDocument(null, 'root');
            var a = xml.createElementNS('urn:test', 'p:Item'); a.id = 'upper';
            var b = xml.createElementNS('urn:test', 'p:item'); b.id = 'lower';
            xml.documentElement.appendChild(a); xml.documentElement.appendChild(b);
            var upper = xml.getElementsByTagName('p:Item');
            var lower = xml.getElementsByTagNameNS('urn:test', 'item');
            """);
        fixture.Bool("upper.length === 1 && upper[0].id === 'upper' && lower[0].id === 'lower'").Should().BeTrue();
        fixture.Execute("xml.documentElement.appendChild(upper[0]); lower[0].remove()");
        fixture.Bool("upper[0].id === 'upper' && lower.length === 0").Should().BeTrue();
    }

    [Test]
    public void LongSpanComparisonsRemainBoundedAndPropagateHostExceptions()
    {
        var checks = 0;
        var work = new DomReadWork(_ => { if (++checks == 3) throw new OperationCanceledException(); }, default);
        var text = new string('x', 4096);
        Assert.Throws<OperationCanceledException>(() => work.EqualSpan(text.AsSpan(), text.AsSpan()));
        checks.Should().Be(3);
    }

    private sealed class CountingFilter : DomElementFilter
    {
        internal int Count;
        internal override bool Matches(Element element) { Count++; return true; }
    }

    private sealed class Probe : Constraint
    {
        internal bool Fail;
        internal Action? Callback;
        public override void Check()
        {
            if (Fail) throw new OperationCanceledException();
            Callback?.Invoke();
        }
        public override void Reset() { }
    }
}
