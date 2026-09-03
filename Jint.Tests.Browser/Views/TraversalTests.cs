using System.Runtime.ExceptionServices;

namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>Range</c>, <c>TreeWalker</c> and <c>NodeIterator</c>: the generated interfaces, plus the members whose
/// signatures the binding could not cross.
/// </summary>
public sealed class TraversalTests
{
    [Test]
    public async Task ARangeSelectsMovesAndExtracts()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><i>two</i><u>three</u></div>
            <script>
              const host = document.getElementById('host');
              const range = document.createRange();
              range.selectNodeContents(host);

              window.log = [
                range instanceof Range,
                range.startContainer === host,
                range.startOffset,
                range.endOffset,
                range.collapsed,
                range.commonAncestorContainer === host,
                range.toString(),
              ].join('|');

              const fragment = range.extractContents();
              window.after = [fragment.childNodes.length, host.childNodes.length].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("true|true|0|3|false|true|onetwothree");
        (await page.EvaluateAsync<string>("window.after")).Should().Be("3|0");
    }

    [Test]
    public async Task ARangeClonesInsertsSurroundsAndDeletes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><i>two</i></div>
            <script>
              const host = document.getElementById('host');
              const range = document.createRange();

              range.selectNode(host.querySelector('b'));
              window.cloned = range.cloneContents().childNodes.length;

              const wrapper = document.createElement('em');
              range.surroundContents(wrapper);
              window.surrounded = host.innerHTML;

              const insertion = document.createRange();
              insertion.setStart(host, 0);
              insertion.collapse(true);
              insertion.insertNode(document.createElement('s'));
              window.inserted = host.firstChild.tagName;

              const removal = document.createRange();
              removal.selectNode(host.querySelector('i'));
              removal.deleteContents();
              window.deleted = host.querySelector('i') === null;
            </script>
            """);

        (await page.EvaluateAsync<int>("window.cloned")).Should().Be(1);
        (await page.EvaluateAsync<string>("window.surrounded")).Should().Be("<em><b>one</b></em><i>two</i>");
        (await page.EvaluateAsync<string>("window.inserted")).Should().Be("S");
        (await page.EvaluateAsync<bool>("window.deleted")).Should().BeTrue();
    }

    [Test]
    public async Task ARangeAnswersZeroRectanglesBecauseThereIsNoLayout()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p id='p'>text</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const range = document.createRange();
              range.selectNodeContents(document.getElementById('p'));
              const rect = range.getBoundingClientRect();
              return [rect.x, rect.y, rect.width, rect.height, range.getClientRects().length].join('|');
            })()
            """))
            .Should().Be("0|0|0|0|0");
    }

    [Test]
    public async Task ATreeWalkerTakesAFunctionFilter()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a></a><b><i></i></b><u></u></div>
            <script>
              const host = document.getElementById('host');
              const walker = document.createTreeWalker(host, NodeFilter.SHOW_ELEMENT, node =>
                node.tagName === 'B' ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT);

              const seen = [];
              let node;
              while ((node = walker.nextNode())) { seen.push(node.tagName) }

              window.log = seen.join(',');
              window.meta = [walker instanceof TreeWalker, walker.root === host, walker.whatToShow].join('|');
            </script>
            """);

        // FILTER_REJECT on an element skips its subtree, so <i> never appears.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("A,U");
        (await page.EvaluateAsync<string>("window.meta")).Should().Be("true|true|1");
    }

    [Test]
    public async Task ATreeWalkerTakesAnObjectFilterAndReportsItBack()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a></a><b></b><u></u></div>
            <script>
              const filter = { acceptNode: node => node.tagName === 'B' ? NodeFilter.FILTER_SKIP : NodeFilter.FILTER_ACCEPT };
              const walker = document.createTreeWalker(document.getElementById('host'), NodeFilter.SHOW_ELEMENT, filter);

              const seen = [];
              let node;
              while ((node = walker.nextNode())) { seen.push(node.tagName) }

              window.log = seen.join(',');
              window.filterIsTheSameObject = walker.filter === filter;
            </script>
            """);

        // FILTER_SKIP passes over the node but keeps its subtree, unlike FILTER_REJECT.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("A,U");
        (await page.EvaluateAsync<bool>("window.filterIsTheSameObject")).Should().BeTrue();
    }

    [Test]
    public async Task ATreeWalkerWalksInEveryDirection()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a><i></i></a><b></b></div>
            <script>
              const host = document.getElementById('host');
              const walker = document.createTreeWalker(host, NodeFilter.SHOW_ELEMENT);
              const seen = [];
              seen.push(walker.firstChild().tagName);
              seen.push(walker.firstChild().tagName);
              seen.push(walker.parentNode().tagName);
              seen.push(walker.nextSibling().tagName);
              seen.push(walker.previousSibling().tagName);
              seen.push(walker.lastChild().tagName);
              window.log = seen.join(',');
              window.current = walker.currentNode.tagName;
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("A,I,A,B,A,I");
        (await page.EvaluateAsync<string>("window.current")).Should().Be("I");
    }

    [Test]
    public async Task ANodeIteratorWalksEveryNodeTypeItWasAskedFor()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><!--note--><i>two</i></div>
            <script>
              const host = document.getElementById('host');
              const iterator = document.createNodeIterator(host);
              const seen = [];
              let node;
              while ((node = iterator.nextNode())) { seen.push(node.nodeName) }
              window.all = seen.join(',');

              const elements = document.createNodeIterator(host, NodeFilter.SHOW_ELEMENT);
              const tags = [];
              while ((node = elements.nextNode())) { tags.push(node.nodeName) }
              window.elements = tags.join(',');
              window.meta = [elements instanceof NodeIterator, elements.root === host, elements.whatToShow, elements.filter].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.all")).Should().Be("DIV,B,#text,#comment,I,#text");
        (await page.EvaluateAsync<string>("window.elements")).Should().Be("DIV,B,I");

        // whatToShow defaults to 0xFFFFFFFF and is an unsigned long: a signed read would answer -1.
        (await page.EvaluateAsync<string>("document.createNodeIterator(document.body).whatToShow.toString()")).Should().Be("4294967295");
        (await page.EvaluateAsync<string>("window.meta")).Should().Be("true|true|1|");
    }

    [Test]
    public async Task ANodeIteratorGoesBackwards()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a></a><b></b></div>
            <script>
              const iterator = document.createNodeIterator(document.getElementById('host'), NodeFilter.SHOW_ELEMENT);
              iterator.nextNode();
              iterator.nextNode();
              const back = iterator.previousNode();
              window.log = [back.nodeName, iterator.referenceNode.nodeName, iterator.pointerBeforeReferenceNode].join('|');
            </script>
            """);

        // DOM's traverse(previous) moves the *pointer* from after the reference node to before it without
        // moving the node, so the first previousNode() after a nextNode() answers the same node again.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("A|A|true");
    }

    [Test]
    public async Task AFilterThatThrowsPropagatesOutOfNextNode()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='host'><a></a></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const walker = document.createTreeWalker(document.getElementById('host'), NodeFilter.SHOW_ELEMENT, () => { throw new Error('from a filter') });
              try { walker.nextNode(); return 'no throw' } catch (e) { return e.message }
            })()
            """))
            .Should().Be("from a filter");
    }

    [Test]
    public async Task NodeFilterCarriesItsConstantsAndABadFilterIsATypeError()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>("[NodeFilter.FILTER_ACCEPT, NodeFilter.FILTER_REJECT, NodeFilter.FILTER_SKIP].join(',')"))
            .Should().Be("1,2,3");
        (await page.EvaluateAsync<string>("[NodeFilter.SHOW_ALL, NodeFilter.SHOW_ELEMENT, NodeFilter.SHOW_TEXT, NodeFilter.SHOW_COMMENT].join(',')"))
            .Should().Be("4294967295,1,4,128");
        (await page.EvaluateAsync<string>("Object.prototype.toString.call(NodeFilter)")).Should().Be("[object NodeFilter]");

        (await page.EvaluateAsync<string>(
            "(() => { try { document.createTreeWalker(document.body, NodeFilter.SHOW_ALL, 7) } catch (e) { return e.constructor.name } return 'no throw' })()"))
            .Should().Be("TypeError");
    }

    // ---------------------------------------------------------------- termination
    // https://dom.spec.whatwg.org/#interface-treewalker. Every one of these drives a walk that used to run
    // forever, so each is bounded from outside rather than asserted from inside: see BoundedWalk for why an
    // internal bound cannot work.

    [Test]
    public void PreviousNodeTerminatesWhenTheFilterRejects()
    {
        // dom/traversal/TreeWalker-traversal-reject.html, "Testing previousNode". FILTER_REJECT on B1 means
        // B1 *and its subtree*, so the walk passes over B1 entirely and climbs to A1.
        BoundedWalk(
            RejectSkipTree,
            """
            var walker = document.createTreeWalker(document.getElementById('root'), NodeFilter.SHOW_ELEMENT,
              function (node) { return node.id === 'B1' ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT });
            walker.currentNode = document.getElementById('B3');
            [walker.previousNode().id, walker.previousNode().id, walker.previousNode().id,
             walker.previousNode(), walker.currentNode.id].join(',');
            """)
            // The root itself is the last node previousNode() answers — its while condition is "node is not
            // root", and the climb to a parent reports that parent even when it is the root — and the walk
            // then ends there rather than leaving it.
            .Should().Be("B2,A1,root,,root");
    }

    [Test]
    public void PreviousNodeTerminatesWhenTheFilterSkips()
    {
        // The same document's FILTER_SKIP twin: skipping B1 keeps its subtree, so C1 is visited between B2
        // and A1 where the rejecting filter passed over it.
        BoundedWalk(
            RejectSkipTree,
            """
            var walker = document.createTreeWalker(document.getElementById('root'), NodeFilter.SHOW_ELEMENT,
              function (node) { return node.id === 'B1' ? NodeFilter.FILTER_SKIP : NodeFilter.FILTER_ACCEPT });
            walker.currentNode = document.getElementById('B3');
            [walker.previousNode().id, walker.previousNode().id, walker.previousNode().id].join(',');
            """)
            .Should().Be("B2,C1,A1");
    }

    [Test]
    public void PreviousNodeTerminatesPastARejectedLastChild()
    {
        // dom/traversal/TreeWalker-previousNodeLastChildReject.html: the walk descends into B1's last child,
        // is told to reject it, and has to resume at that child's *previous sibling* rather than start over.
        BoundedWalk(
            """
            <!doctype html>
            <div id="root"><div id="A1"><div id="B1"><div id="C1"></div><div id="C2"><div id="D1"></div><div id="D2"></div></div></div><div id="B2"><div id="C3"></div><div id="C4"></div></div></div></div>
            """,
            """
            var walker = document.createTreeWalker(document.getElementById('root'), NodeFilter.SHOW_ELEMENT,
              function (node) { return node.id === 'C2' ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT });
            walker.currentNode = document.getElementById('B2');
            [walker.previousNode().id, walker.currentNode.id].join(',');
            """)
            .Should().Be("C1,C1");
    }

    [Test]
    public void EveryTraversalTerminatesFromACurrentNodeOutsideTheRoot()
    {
        // dom/traversal/TreeWalker-currentNode.html. The setter has no root check at all — DOM §6.1's
        // currentNode setter is "set this's current to the given value" — so it is each method's own root
        // test that has to stop the walk, and previousNode's is reached only after it has climbed out of the
        // document element and found no parent.
        BoundedWalk(
            "<!doctype html><html><body><div id='parent'><div id='subTree'><p>a<span>b</span></p></div></div></body></html>",
            """
            var walker = document.createTreeWalker(document.getElementById('subTree'),
              NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_COMMENT, function () { return true });
            var out = [];
            walker.currentNode = document.documentElement;
            out.push(walker.previousNode() === null, walker.currentNode === document.documentElement);
            walker.currentNode = document.documentElement;
            out.push(walker.parentNode() === null, walker.currentNode === document.documentElement);
            walker.currentNode = document.documentElement;
            out.push(walker.nextSibling() === null, walker.previousSibling() === null);
            walker.currentNode = document.documentElement;
            out.push(walker.nextNode() === document.documentElement.firstChild);
            out.join(',');
            """)
            .Should().Be("true,true,true,true,true,true,true");
    }

    [Test]
    public void ANodeIteratorTerminatesUnderTheSameFilters()
    {
        // The sibling interface, checked for the same class of defect. Its two traversals step through one
        // document-order enumeration and answer null at its ends, so a filter that never accepts is a full
        // pass and not a loop.
        BoundedWalk(
            RejectSkipTree,
            """
            var reject = document.createNodeIterator(document.getElementById('root'), NodeFilter.SHOW_ELEMENT,
              function () { return NodeFilter.FILTER_REJECT });
            var skip = document.createNodeIterator(document.getElementById('root'), NodeFilter.SHOW_ELEMENT,
              function () { return NodeFilter.FILTER_SKIP });
            [reject.nextNode(), reject.previousNode(), skip.nextNode(), skip.previousNode()].join('|');
            """)
            .Should().Be("|||");
    }

    [Test]
    public void AFilterThatWalksItsOwnWalkerIsAnInvalidStateError()
    {
        // https://dom.spec.whatwg.org/#concept-node-filter step 1: the traverser's "is active" flag. Without
        // it a filter re-entering its own walker recurses through the traversal once per node it is handed,
        // on the page's own thread, which is the same denial of service in a second shape.
        BoundedWalk(
            RejectSkipTree,
            """
            var walker = document.createTreeWalker(document.getElementById('root'), NodeFilter.SHOW_ELEMENT,
              function () { walker.nextNode(); return NodeFilter.FILTER_ACCEPT });
            (function () { try { walker.nextNode(); return 'no throw' } catch (e) { return e.name } })();
            """)
            .Should().Be("InvalidStateError");
    }

    /// <summary>
    /// <a href="https://dom.spec.whatwg.org/#interface-nodefilter">DOM §6.3</a>'s own constant values.
    /// <c>DomTestFixture</c> has no page runtime, so <c>ViewInstaller</c>'s <c>NodeFilter</c> namespace
    /// object is not installed on it; declaring the five values the walks below use keeps them at the
    /// binding layer rather than needing a whole page to reach a constant.
    /// </summary>
    private const string NodeFilterConstants =
        "const NodeFilter = { SHOW_ELEMENT: 0x1, SHOW_COMMENT: 0x80, FILTER_ACCEPT: 1, FILTER_REJECT: 2, FILTER_SKIP: 3 }; ";

    /// <summary>The tree dom/traversal's reject and skip documents build, as markup.</summary>
    private const string RejectSkipTree =
        """
        <!doctype html>
        <div id="root"><div id="A1"><div id="B1"><div id="C1"></div></div><div id="B2"></div><div id="B3"></div></div></div>
        """;

    /// <summary>
    /// Evaluates <paramref name="script"/> against a fixture built from <paramref name="html"/>, on a thread
    /// of its own, and fails rather than hangs when the walk does not come back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bound has to be outside the walk, because nothing inside it can be reached.</b> A traversal
    /// that does not terminate spins in the binding's own C# loop, and the shapes that used to hang did not
    /// even re-enter the engine on every turn — a node <c>whatToShow</c> excludes is <c>FILTER_SKIP</c>
    /// without the page's filter being called at all — so no constraint, no cancellation token and no
    /// statement budget is ever consulted. That is why a page budget cannot bound one either, and why these
    /// were hangs rather than failures in the browser lane.
    /// </para>
    /// <para>
    /// So the walk runs on a background thread and the assertion is on the join: a thread that never returns
    /// is abandoned rather than waited for, which keeps one wedged walk from taking the suite with it.
    /// </para>
    /// </remarks>
    private static string BoundedWalk(string html, string script)
    {
        string? answer = null;
        ExceptionDispatchInfo? failure = null;

        var walk = new Thread(() =>
        {
            try
            {
                using var fixture = DomTestFixture.Create(html);
                answer = fixture.Evaluate(NodeFilterConstants + script).ToString();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "bounded DOM walk",
        };

        walk.Start();
        walk.Join(TimeSpan.FromSeconds(20)).Should().BeTrue("a DOM traversal has to terminate");
        failure?.Throw();

        return answer!;
    }
}
