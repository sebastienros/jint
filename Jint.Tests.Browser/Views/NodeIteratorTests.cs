namespace Jint.Tests.Browser.Views;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-nodeiterator">DOM §6.1's <c>NodeIterator</c></a>, and in
/// particular what a removal does to one: the
/// <a href="https://dom.spec.whatwg.org/#nodeiterator-pre-removing-steps">pre-remove steps</a> adjust both
/// the reference and the in-flight candidate, and
/// <a href="https://dom.spec.whatwg.org/#concept-nodeiterator-traverse">traverse</a> promotes the candidate
/// only once a node is accepted.
/// </summary>
/// <remarks>
/// The four removal cases are <c>dom/traversal/NodeIterator-removal-during-filtering.html</c>'s, which is
/// where the defect was found; the rest are the ordinary traversal, so that the algorithm moving to this side
/// of the binding is held to what it already answered.
/// </remarks>
public sealed class NodeIteratorTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="host"><b>one</b><i>two</i><u>three</u></div></body></html>
        """;

    /// <summary>
    /// The <c>NodeFilter</c> constants, spelled out because the interface object is installed by the page
    /// runtime and this fixture is the binding on its own.
    /// </summary>
    private const string Filters = """
        const SHOW_ELEMENT = 1, SHOW_TEXT = 4;
        const FILTER_ACCEPT = 1, FILTER_REJECT = 2, FILTER_SKIP = 3;
        """;

    /// <summary>The tree every removal case is built on: <c>root &gt; [a, b, c]</c>.</summary>
    private const string Tree = """
        const root = document.createElement("div");
        const a = document.createElement("a-el");
        const b = document.createElement("b-el");
        const c = document.createElement("c-el");
        root.append(a, b, c);
        """;

    /// <summary>
    /// The value answered is the node the filter looked at, and the reference is retargeted to a node that is
    /// still in the tree — so the traversal can go on.
    /// </summary>
    [Test]
    public void RemovingTheAcceptedNodeDuringItsOwnFilteringRetargetsTheReference()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              {{Tree}}
              const it = document.createNodeIterator(root, SHOW_ELEMENT, {
                acceptNode(node) {
                  if (node === b) { b.remove(); }
                  return FILTER_ACCEPT;
                }
              });

              const first = it.nextNode();
              const second = it.nextNode();
              const returned = it.nextNode();

              return [
                first === root,
                second === a,
                returned === b,
                it.referenceNode === a,
                it.pointerBeforeReferenceNode,
                it.nextNode() === c,
              ].join('|');
            })()
            """).Should().Be("true|true|true|true|false|true");
    }

    /// <summary>
    /// A removal that does not cover either position changes neither, so forward traversal is undisturbed.
    /// </summary>
    [Test]
    public void RemovingAnAlreadyVisitedSubtreeDuringFilteringDoesNotDisturbTheWalk()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              {{Tree}}
              const it = document.createNodeIterator(root, SHOW_ELEMENT, {
                acceptNode(node) {
                  if (node === b) { a.remove(); }
                  return FILTER_ACCEPT;
                }
              });

              return [
                it.nextNode() === root,
                it.nextNode() === a,
                it.nextNode() === b,
                it.referenceNode === b,
                it.nextNode() === c,
              ].join('|');
            })()
            """).Should().Be("true|true|true|true|true");
    }

    /// <summary>
    /// While a node is being filtered it is the <i>candidate</i>, not the reference: <c>referenceNode</c>
    /// still answers the last node that was accepted, which is what the candidate exists to keep true.
    /// </summary>
    [Test]
    public void ReferenceNodeDuringFilteringIsTheLastAcceptedNode()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              {{Tree}}
              let it;
              const observed = [];
              it = document.createNodeIterator(root, SHOW_ELEMENT, {
                acceptNode(node) {
                  observed.push([node, it.referenceNode]);
                  return node === b ? FILTER_REJECT : FILTER_ACCEPT;
                }
              });

              const walked = [it.nextNode() === root, it.nextNode() === a, it.nextNode() === c];
              const names = observed.map(pair => pair[0].localName).join(',');
              const references = observed.map(pair => pair[1].localName || 'div').join(',');

              return [walked.join('|'), names, references].join(' / ');
            })()
            """).Should().Be("true|true|true / div,a-el,b-el,c-el / div,div,a-el,a-el");
    }

    /// <summary>
    /// The branch the pre-remove steps take when the pointer is <i>before</i> the removed subtree and root
    /// holds nothing after it: the position moves to the last node before the subtree and the pointer flips.
    /// </summary>
    /// <remarks>
    /// The wpt file this comes from notes that WebKit and Blink leave the position on the detached node
    /// instead, which is what makes it worth pinning rather than assuming.
    /// </remarks>
    [Test]
    public void RemovingAnAncestorOfTheInFlightPositionMovesItBackWithinRoot()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              const root = document.createElement("div");
              const a = document.createElement("a-el");
              const a1 = document.createElement("a1-el");
              const b = document.createElement("b-el");
              const b1 = document.createElement("b1-el");
              a.append(a1);
              b.append(b1);
              root.append(a, b);

              let armed = false;
              const it = document.createNodeIterator(root, SHOW_ELEMENT, {
                acceptNode(node) {
                  if (armed && node === b1) { b.remove(); }
                  return FILTER_ACCEPT;
                }
              });

              for (let i = 0; i < 5; i++) { it.nextNode(); }
              const advanced = [it.referenceNode === b1, it.pointerBeforeReferenceNode];

              armed = true;
              const returned = it.previousNode();

              return advanced.concat([
                returned === b1,
                it.referenceNode === a1,
                it.pointerBeforeReferenceNode,
              ]).join('|');
            })()
            """).Should().Be("true|false|true|true|false");
    }

    /// <summary>
    /// A removal <i>between</i> traversals moves the reference the same way, which is the half AngleSharp
    /// already answered and which has to go on answering.
    /// </summary>
    [Test]
    public void ARemovalBetweenTraversalsRetargetsTheReference()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              {{Tree}}
              const it = document.createNodeIterator(root, SHOW_ELEMENT);
              it.nextNode();
              it.nextNode();
              it.nextNode();
              const before = it.referenceNode === b;
              b.remove();
              return [before, it.referenceNode === a, it.pointerBeforeReferenceNode, it.nextNode() === c].join('|');
            })()
            """).Should().Be("true|true|false|true");
    }

    /// <summary>
    /// The ordinary traversal: forward to the end, backward to the start, <c>whatToShow</c> deciding what is
    /// seen, and a bare function accepted as the filter.
    /// </summary>
    [Test]
    public void TheWalkGoesForwardAndBackAndHonoursWhatToShow()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              const host = document.getElementById('host');
              const elements = document.createNodeIterator(host, SHOW_ELEMENT);
              const forward = [];
              for (let n = elements.nextNode(); n; n = elements.nextNode()) { forward.push(n.localName); }
              const back = [];
              for (let n = elements.previousNode(); n; n = elements.previousNode()) { back.push(n.localName); }

              const text = document.createNodeIterator(host, SHOW_TEXT);
              const texts = [];
              for (let n = text.nextNode(); n; n = text.nextNode()) { texts.push(n.data); }

              const skipped = document.createNodeIterator(host, SHOW_ELEMENT,
                node => node.localName === 'i' ? FILTER_REJECT : FILTER_ACCEPT);
              const kept = [];
              for (let n = skipped.nextNode(); n; n = skipped.nextNode()) { kept.push(n.localName); }

              return [forward.join(','), back.join(','), texts.join(','), kept.join(',')].join(' / ');
            })()
            """).Should().Be("div,b,i,u / u,i,b,div / one,two,three / div,b,u");
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-node-filter step 1: a filter that traverses its own iterator is
    /// an <c>InvalidStateError</c>, rather than recursion the page's thread cannot come back from.
    /// </summary>
    [Test]
    public void AFilterThatTraversesItsOwnIteratorIsRefused()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              const host = document.getElementById('host');
              let it;
              it = document.createNodeIterator(host, SHOW_ELEMENT, function () {
                it.nextNode();
                return FILTER_ACCEPT;
              });
              try { it.nextNode(); return 'no throw'; }
              catch (e) { return e.name; }
            })()
            """).Should().Be("InvalidStateError");
    }

    /// <summary>
    /// The members the generator projects still answer, which is what says the shape found this class: the
    /// filter is the value the page passed, and the rest is the iterator's own state.
    /// </summary>
    [Test]
    public void TheProjectedMembersAnswerOverTheOwnIterator()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              {{Filters}}
              const host = document.getElementById('host');
              const filter = node => FILTER_ACCEPT;
              const it = document.createNodeIterator(host, SHOW_ELEMENT, filter);
              return [
                it instanceof NodeIterator,
                Object.prototype.toString.call(it),
                it.root === host,
                it.referenceNode === host,
                it.pointerBeforeReferenceNode,
                it.whatToShow,
                it.filter === filter,
                document.createNodeIterator(host).filter === null,
              ].join('|');
            })()
            """).Should().Be("true|[object NodeIterator]|true|true|true|1|true|true");
    }
}
