using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Extraction;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Extraction;

public sealed class NativeInnerTextTests
{
    [TestCase("<div id=t style='white-space:pre-line'>a\n  b   c</div>", "a\nb c")]
    [TestCase("<div id=t style='white-space:pre'><span style='white-space:normal'>a\n  b   c</span></div>", "a b c")]
    [TestCase("<div id=t style='white-space-collapse:preserve-breaks'>a\n  b   c</div>", "a\nb c")]
    public async Task ScriptGetterUsesTheActualComputedCollapseMode(string markup, string expected)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(markup);
        (await page.EvaluateAsync<string>("document.getElementById('t').innerText")).Should().Be(expected);
    }

    [TestCase("<div id=t>a<div hidden>b</div><div>c</div>d<br>e</div>", "a\nc\nd\ne")]
    [TestCase("<div id=t hidden>  a<br>b\nc </div>", "  ab\nc ")]
    [TestCase("<section style='display:none'><div id=t> a<br>b </div></section>", " ab ")]
    [TestCase("<div id=t style='visibility:hidden'>hidden<span style='visibility:visible'>visible</span>hidden</div>", "visible")]
    [TestCase("<div id=t>a<div style='visibility:collapse'>hidden<span style='visibility:visible'>visible</span>hidden</div>b</div>", "avisibleb")]
    [TestCase("<table><tr><td id=t>A</td><td>B</td></tr><tr><td>C</td></tr></table>", "A")]
    [TestCase("<table><tr id=t><td>A</td><td>B</td></tr><tr><td>C</td></tr></table>", "A\tB")]
    [TestCase("<div><br id=t>after</div>", "")]
    public void GetterUsesRenderedExtractionAndDescendantTextForAnUnrenderedRoot(string markup, string expected)
    {
        using var engine = new Engine();
        var target = ContentDom.ElementById(ContentDom.Parse(markup), "t")!;
        DomInnerText.Get(DomRealm.Of(engine), target).Should().Be(expected);
    }

    [Test]
    public void SetterPublishesOneReplaceAllAndPreservesTheTargetPlacement()
    {
        using var engine = new Engine();
        var document = ContentDom.Parse("<div><span id=t>old</span><b id=next>next</b></div>");
        var target = ContentDom.ElementById(document, "t")!;
        var parent = target.ParentNode;
        var next = target.NextSibling;
        using var changes = document.ObserveMutations(target, new MutationObserverOptions { ChildList = true });
        DomInnerText.Set(DomRealm.Of(engine), target, "a\r\nb\rc\nd");
        target.ParentNode.Should().BeSameAs(parent);
        target.NextSibling.Should().BeSameAs(next);
        target.GetAttribute("id").Should().Be("t");
        var nodes = target.ChildNodes.ToArray();
        nodes.Should().HaveCount(7);
        ((Text) nodes[0]).Data.Should().Be("a");
        ((Text) nodes[2]).Data.Should().Be("b");
        ((Text) nodes[4]).Data.Should().Be("c");
        ((Text) nodes[6]).Data.Should().Be("d");
        foreach (var index in new[] { 1, 3, 5 })
        {
            ((Element) nodes[index]).LocalName.Should().Be("br");
            ((Element) nodes[index]).NamespaceUri.Should().Be(Namespaces.Html);
        }
        var records = changes.TakeRecords();
        records.Should().ContainSingle();
        records[0].RemovedNodes.Should().ContainSingle();
        records[0].AddedNodes.Should().HaveCount(7);
    }

    [Test]
    public void CancellationWhilePreparingSetterLeavesTheExistingChildrenIntact()
    {
        var constraint = new CancelPreparation();
        using var engine = new Engine(options => options.AddConstraint(constraint));
        var target = ContentDom.ElementById(ContentDom.Parse("<div id=t><b>old</b></div>"), "t")!;
        var original = target.FirstChild;
        var realm = DomRealm.Of(engine);
        constraint.Armed = true;
        Action set = () => DomInnerText.Set(realm, target, new string('x', 8192));
        set.Should().ThrowExactly<OperationCanceledException>();
        target.FirstChild.Should().BeSameAs(original);
        original!.NextSibling.Should().BeNull();
    }

    [Test]
    public void ParserBackedTextCancelsBeforeMaterializingItsWholeString()
    {
        var small = ContentDom.ElementById(ContentDom.Parse("<div id=t>warm</div>"), "t")!;
        TextExtractor.InnerText(small, false, null, default).Should().Be("warm");
        var target = ContentDom.ElementById(ContentDom.Parse("<div id=t>" + new string('x', 1_048_576) + "</div>"), "t")!;
        Action read = () => TextExtractor.InnerText(target, false, units =>
        {
            if (units == 256) throw new OperationCanceledException();
        }, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Caught.Exception(read);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        failure.Should().BeOfType<OperationCanceledException>();
        allocated.Should().BeLessThan(131_072, "a canceled bounded read must not copy the one-megabyte parser-backed text");
    }

    [Test]
    public void DeepRenderedTraversalUsesAnIterativeWalk()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("span");
        document.AppendChild(root);
        var parent = root;
        for (var i = 0; i < 4096; i++)
        {
            var child = document.CreateElement("span");
            parent.AppendChild(child);
            parent = child;
        }
        parent.AppendChild(document.CreateTextNode("leaf"));
        TextExtractor.InnerText(root, false, null, default).Should().Be("leaf");
    }

    [Test]
    public void InlineStyleDelimiterRunsPollTheActualReadWork()
    {
        var target = ContentDom.ElementById(ContentDom.Parse("<div id=t>text</div>"), "t")!;
        target.SetAttribute("style", new string(';', 1_048_576));
        var checks = new List<int>();
        var work = new DomReadWork(units =>
        {
            checks.Add(units);
            if (units == 256) throw new OperationCanceledException();
        }, default);
        var visibility = new ElementVisibility(false, work);
        Action read = () => visibility.Style(target);
        read.Should().ThrowExactly<OperationCanceledException>();
        checks.Should().Contain(256);
    }

    private sealed class CancelPreparation : Constraint
    {
        internal bool Armed;
        private int _checks;
        public override void Check()
        {
            if (Armed && ++_checks == 2) throw new OperationCanceledException();
        }
        public override void Reset() { }
    }
}
