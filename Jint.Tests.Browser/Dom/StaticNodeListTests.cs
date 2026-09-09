using System.Runtime.CompilerServices;
using Jint.Browser.Dom.Collections;
using Jint.Native;

namespace Jint.Tests.Browser;

/// <summary>
/// The static <c>NodeList</c> <c>querySelectorAll</c> returns, and the per-index element-wrapper cache on it.
/// </summary>
/// <remarks>
/// <para>
/// The cache is a memo of <c>DomRealm.WrapNode</c>, so every assertion here about identity is an assertion
/// that it stayed one: the cached object <i>is</i> the canonical wrapper the realm's weak table holds, and a
/// second list over the same node, a fresh <c>querySelector</c> and the tree all have to keep answering it.
/// Most of these therefore pass on both sides of the change — deliberately, because "nothing observable moved"
/// is the whole claim. What only passes after it is
/// <see cref="OnlyASelectorMatchTakesTheStaticLane"/>, which pins the seam itself.
/// </para>
/// <para>
/// The retention tests are the other half, and they are reachability questions rather than heap readings, for
/// the reasons <c>Jint.Tests.Runtime.GarbageCollectionTests</c> records: a weak reference names the object
/// rather than an amount of bytes that resembles it.
/// </para>
/// </remarks>
public sealed class StaticNodeListTests
{
    private const string Page = "<div id='root'><span class='foo' id='s0'>a</span><span class='foo' id='s1'>b</span></div>";

    /// <summary>
    /// 100 elements, which is what <c>dom/nodes/support/NodeList-static-length-tampered.js</c> builds and the
    /// size the profile in <a href="https://github.com/sebastienros/jint/issues/4013">#4013</a> was taken on.
    /// </summary>
    private const string HundredSpans = """
        var root = document.getElementById('root');
        root.innerHTML = '';
        for (var i = 0; i < 100; i++) {
            var el = document.createElement('span');
            el.className = 'foo';
            root.appendChild(el);
        }
        """;

    [Test]
    public void TheSameIndexAnswersOneObjectHoweverOftenItIsRead()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("var l = document.querySelectorAll('.foo'); l[0] === l[0]").Should().BeTrue();
        fixture.Bool("l[1] === l[1]").Should().BeTrue();
        fixture.Bool("l[0] === l[1]").Should().BeFalse();

        // The expando is the reason identity matters at all, and it survives the cache being the thing that
        // answered the second read.
        fixture.Evaluate("l[0].__state = 7;");
        fixture.Number("l[0].__state").Should().Be(7);
    }

    [Test]
    public void ACachedElementIsTheSameObjectEveryOtherRouteToTheNodeAnswers()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Execute("var l = document.querySelectorAll('.foo'); var first = l[0];");

        fixture.Bool("first === document.querySelector('.foo')").Should().BeTrue();
        fixture.Bool("first === document.getElementById('s0')").Should().BeTrue();
        fixture.Bool("first === document.getElementById('root').firstElementChild").Should().BeTrue();
        fixture.Bool("first === document.getElementById('root').childNodes[0]").Should().BeTrue();

        // And the other way round: a wrapper the tree handed out first is what the list answers afterwards.
        fixture.Execute("var second = document.getElementById('s1'); var l2 = document.querySelectorAll('.foo');");
        fixture.Bool("l2[1] === second").Should().BeTrue();
    }

    [Test]
    public void TwoListsOverTheSameNodesAnswerTheSameObjects()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Execute("var a = document.querySelectorAll('.foo'); var b = document.querySelectorAll('.foo');");

        // DOM makes each call a new static NodeList, so the two lists are different objects...
        fixture.Bool("a === b").Should().BeFalse();
        // ...while every element they answer is the one wrapper its node has.
        fixture.Bool("a[0] === b[0]").Should().BeTrue();
        fixture.Bool("a[1] === b[1]").Should().BeTrue();

        // Filling one list's cache first must not change what the other one answers, in either order.
        fixture.Execute("var c = document.querySelectorAll('.foo'); var seen = c[0]; var d = document.querySelectorAll('.foo');");
        fixture.Bool("d[0] === seen").Should().BeTrue();
    }

    [Test]
    public void ARemovedNodeStillAnswersTheSameWrapperFromTheListThatMatchedIt()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Execute("var l = document.querySelectorAll('.foo'); var first = l[0];");
        fixture.Execute("first.__state = 'kept'; first.remove();");

        // "Static" means the membership does not change, so a removed node is still element 0...
        fixture.Number("l.length").Should().Be(2);
        fixture.Bool("l[0] === first").Should().BeTrue();
        fixture.Text("l[0].__state").Should().Be("kept");
        fixture.Bool("l[0].isConnected").Should().BeFalse();

        // ...and the live NodeList beside it does change, which is what says the two lanes stayed apart.
        fixture.Number("document.getElementById('root').childNodes.length").Should().Be(1);

        // A fresh match no longer finds it, and the wrapper the removed node keeps is still the only one.
        fixture.Number("document.querySelectorAll('.foo').length").Should().Be(1);
        fixture.Bool("document.querySelectorAll('.foo')[0] === l[1]").Should().BeTrue();
    }

    [Test]
    public void AnIndexPastTheEndIsUndefinedAndItemIsNull()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Execute("var l = document.querySelectorAll('.foo');");

        // dom/nodes/Node-childNodes.html asserts these two answers are different on purpose.
        fixture.Evaluate("l[2]").IsUndefined().Should().BeTrue();
        fixture.Evaluate("l[99]").IsUndefined().Should().BeTrue();
        fixture.Evaluate("l.item(2)").IsNull().Should().BeTrue();
        fixture.Evaluate("l.item(-1)").IsNull().Should().BeTrue();

        fixture.Bool("2 in l").Should().BeFalse();
        fixture.Bool("1 in l").Should().BeTrue();
        fixture.Bool("l.hasOwnProperty('0')").Should().BeTrue();
        fixture.Bool("l.hasOwnProperty('2')").Should().BeFalse();
        fixture.Text("Object.keys(l).join(',')").Should().Be("0,1");

        // An empty match keeps the same shape, with no array to allocate at all.
        fixture.Number("document.querySelectorAll('.nothing').length").Should().Be(0);
        fixture.Evaluate("document.querySelectorAll('.nothing')[0]").IsUndefined().Should().BeTrue();
    }

    [Test]
    public void TheWrapperIsStillANodeListInEveryWayAPageCanSee()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Execute("var l = document.querySelectorAll('.foo');");

        fixture.Bool("l instanceof NodeList").Should().BeTrue();
        fixture.Bool("Object.getPrototypeOf(l) === NodeList.prototype").Should().BeTrue();
        fixture.Text("Object.prototype.toString.call(l)").Should().Be("[object NodeList]");
        fixture.Bool("Array.isArray(l)").Should().BeFalse();
        fixture.Bool("l[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();
        fixture.Text("Array.from(l).map(function (e) { return e.id; }).join(',')").Should().Be("s0,s1");
        fixture.Text("[...l].map(function (e) { return e.id; }).join(',')").Should().Be("s0,s1");
        fixture.Number("Array.prototype.indexOf.call(l, l[1])").Should().Be(1);
        fixture.Text("JSON.stringify(Array.prototype.map.call(l, function (e) { return e.id; }))")
            .Should().Be("[\"s0\",\"s1\"]");

        var forEach = "var ids = []; l.forEach(function (e) { ids.push(e.id); }); ids.join(',')";
        fixture.Text(forEach).Should().Be("s0,s1");
    }

    /// <summary>
    /// The three <c>NodeList-static-length-getter-tampered-*.html</c> shapes, in miniature: an own
    /// <c>length</c> a page defines over the list must decide what a loop and <c>Array.prototype.indexOf</c>
    /// see, and the element reads must keep answering the elements they always did.
    /// </summary>
    [Test]
    public void ATamperedLengthDecidesWhatALoopSeesAndTheElementsDoNotMove()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Execute(HundredSpans);
        fixture.Execute("var l = document.querySelectorAll('.foo'); var el = l[50];");

        const string Loop = """
            (function () {
                for (var j = 0; j < l.length; j++) {
                    if (l[j] === el) { return j; }
                }
                return -1;
            })()
            """;

        fixture.Number(Loop).Should().Be(50);
        fixture.Number("Array.prototype.indexOf.call(l, el)").Should().Be(50);

        // Exactly what the wpt documents do half way through their loop.
        fixture.Execute("Object.defineProperty(l, 'length', { get: function () { return 10; } });");

        fixture.Number("l.length").Should().Be(10);
        fixture.Number(Loop).Should().Be(-1);
        fixture.Number("Array.prototype.indexOf.call(l, el)").Should().Be(-1);

        // The tampered length shortens what a length-driven walk sees; it does not change an element read,
        // and index 50 is still the object it was before the redefinition.
        fixture.Bool("l[50] === el").Should().BeTrue();
        fixture.Bool("l[99] === undefined").Should().BeFalse();
        fixture.Number("l.item(50) === el ? 1 : 0").Should().Be(1);
    }

    /// <summary>
    /// The seam: only a selector match takes the static lane, and it takes it through its <i>target</i>. A
    /// live <c>NodeList</c> — <c>childNodes</c>, and a labelable element's <c>labels</c> — must keep reading
    /// through the accessor, because a per-index cache over a list whose membership moves would answer the
    /// wrong node.
    /// </summary>
    /// <remarks>
    /// The second thing it pins is the shape, and that half is a performance contract: a static
    /// <c>NodeList</c> and a live one are the <b>same</b> wrapper class, so the interpreter's array-like read
    /// lane — which devirtualizes <c>ArrayLikeObject.TryGetIndex</c> from a class profile holding one guess —
    /// sees one candidate for both. Splitting them makes that guess a race between whichever list warmed the
    /// call site first, and the live lists pay for the static one's cache. This is the one test here that
    /// fails against the unfixed code.
    /// </remarks>
    [Test]
    public void OnlyASelectorMatchTakesTheStaticLane()
    {
        using var fixture = DomTestFixture.Create(
            "<div id='root'><span class='foo' id='s0'>a</span></div><label for='c'>l</label><input id='c'>");

        static object TargetOf(JsValue collection)
        {
            collection.Should().BeOfType<DomCollectionObject>("every DOM collection is the one wrapper class");
            return ((DomCollectionObject) collection).DomTarget;
        }

        TargetOf(fixture.Evaluate("document.querySelectorAll('.foo')")).Should().BeOfType<DomStaticNodeList>();
        TargetOf(fixture.Evaluate("document.getElementById('root').querySelectorAll('span')")).Should().BeOfType<DomStaticNodeList>();
        TargetOf(fixture.Evaluate("document.createDocumentFragment().querySelectorAll('span')")).Should().BeOfType<DomStaticNodeList>();

        TargetOf(fixture.Evaluate("document.getElementById('root').childNodes")).Should().NotBeOfType<DomStaticNodeList>();
        TargetOf(fixture.Evaluate("document.getElementById('c').labels")).Should().BeOfType<DomLabelNodeList>();

        // A live NodeList reports the tree it currently has, which is the property a cache would have broken.
        fixture.Execute("var live = document.getElementById('root').childNodes;");
        fixture.Number("live.length").Should().Be(1);
        fixture.Execute("document.getElementById('root').appendChild(document.createElement('b'));");
        fixture.Number("live.length").Should().Be(2);
        fixture.Text("live[1].tagName").Should().Be("B");
        fixture.Execute("document.getElementById('root').removeChild(document.getElementById('s0'));");
        fixture.Text("live[0].tagName").Should().Be("B");
    }

    /// <summary>
    /// Dropping the page and the list lets the nodes and their wrappers go — the cache adds no retention over
    /// the snapshot, which adds none over the collection AngleSharp already built.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void DroppingTheListAndThePageCollectsTheNodesAndTheirWrappers()
    {
        var (node, wrapper, list) = BuildAndDropAPageWithAStaticNodeList();

        Collect();

        list.IsAlive.Should().BeFalse("nothing names the NodeList wrapper any more");
        wrapper.IsAlive.Should().BeFalse("the element wrapper was only reachable through the list's cache and the realm's weak table");
        node.IsAlive.Should().BeFalse("the AngleSharp element was only reachable through the document and the snapshot");
    }

    /// <summary>
    /// The control, and the reason the test above can be trusted: the same three objects, with the page and
    /// the list still held, must survive. Without it a harness that failed to build anything would satisfy
    /// the assertions above for the wrong reason.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void APageThatStillHoldsItsListKeepsTheNodesAndTheirWrappers()
    {
        using var fixture = DomTestFixture.Create(Page);
        var (node, wrapper, list) = ReadAStaticNodeList(fixture, keepInAGlobal: true);

        Collect();

        list.IsAlive.Should().BeTrue("a global still names the NodeList");
        wrapper.IsAlive.Should().BeTrue("the list still names the element wrapper");
        node.IsAlive.Should().BeTrue("the document still holds the element");

        GC.KeepAlive(fixture);
    }

    /// <summary>
    /// The sharper half: within a page that is still alive, a static list script has dropped must not go on
    /// holding the wrappers it cached. This is what fails if the cache is ever moved off the wrapper — onto
    /// the realm, or onto a table keyed by anything the engine outlives.
    /// </summary>
    [Test]
    [NonParallelizable]
    public void ACacheDoesNotOutliveTheListInsideALivePage()
    {
        using var fixture = DomTestFixture.Create(Page);
        var (node, wrapper, _) = ReadAStaticNodeList(fixture, keepInAGlobal: false);

        // The nodes leave the tree too: while the document holds them, their wrappers are reachable through
        // the realm's table whatever this cache does, so detaching them is what makes the question about the
        // cache.
        fixture.Execute("document.getElementById('root').innerHTML = '';");

        Collect();

        wrapper.IsAlive.Should().BeFalse("the list that cached the wrapper is unreachable and the node has left the tree");
        node.IsAlive.Should().BeFalse("nothing reaches the detached element any more");

        GC.KeepAlive(fixture);
    }

    /// <summary>
    /// Builds a whole page, reads one element out of a static <c>NodeList</c>, and hands back weak references
    /// to the AngleSharp element, its wrapper and the list — keeping no strong reference to any of them, nor
    /// to the fixture. <c>NoInlining</c> so nothing stays rooted in the caller's frame.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Node, WeakReference Wrapper, WeakReference List) BuildAndDropAPageWithAStaticNodeList()
    {
        using var fixture = DomTestFixture.Create(Page);
        return ReadAStaticNodeList(fixture, keepInAGlobal: false);
    }

    /// <summary>
    /// Reads <c>querySelectorAll('.foo')[0]</c> on <paramref name="fixture"/> and weakly observes the element,
    /// its wrapper and the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Node, WeakReference Wrapper, WeakReference List) ReadAStaticNodeList(
        DomTestFixture fixture,
        bool keepInAGlobal)
    {
        WeakReference? wrapper = null;
        WeakReference? list = null;

        fixture.Engine.SetValue("observe", new Action<JsValue, JsValue>((element, nodeList) =>
        {
            wrapper ??= new WeakReference(element);
            list ??= new WeakReference(nodeList);
        }));

        fixture.Execute($$"""
            var kept = null;
            (function () {
                var l = document.querySelectorAll('.foo');
                observe(l[0], l);
                {{(keepInAGlobal ? "kept = l;" : "")}}
            })();
            """);

        var node = new WeakReference(fixture.Document.QuerySelector(".foo")!);
        return (node, wrapper!, list!);
    }

    private static void Collect()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }
}
