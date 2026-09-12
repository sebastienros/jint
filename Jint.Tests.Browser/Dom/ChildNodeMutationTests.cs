namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-childnode">DOM §4.2.8</a>'s <c>before</c>, <c>after</c>
/// and <c>replaceWith</c>, whose viable-sibling step runs before the argument conversion.
/// </summary>
/// <remarks>
/// Each case here passes an argument list that includes the receiver or one of its siblings, which is what
/// separates the standard's order from AngleSharp's: "convert nodes into a node" moves every argument node
/// into a fragment, so a reference node chosen after it has already left the parent.
/// </remarks>
public sealed class ChildNodeMutationTests
{
    [Test]
    public void BeforeAcceptsTheReceiverAmongItsArguments()
    {
        using var fixture = DomTestFixture.Create("<body><div id='p'><b id='c'></b></div></body>");

        fixture.Text(
            """
            var c = document.getElementById('c');
            c.before('text', c);
            document.getElementById('p').innerHTML;
            """).Should().Be("text<b id=\"c\"></b>");
    }

    [Test]
    public void BeforeReordersWhenTheReceiverAndASiblingSwapPlaces()
    {
        using var fixture = DomTestFixture.Create("<body><div id='p'><b id='c'></b><i id='x'></i></div></body>");

        fixture.Text(
            """
            var c = document.getElementById('c');
            c.before(document.getElementById('x'), c);
            document.getElementById('p').innerHTML;
            """).Should().Be("<i id=\"x\"></i><b id=\"c\"></b>");
    }

    [Test]
    public void AfterReordersWhenTheReceiverAndASiblingSwapPlaces()
    {
        using var fixture = DomTestFixture.Create("<body><div id='p'><i id='x'></i><b id='c'></b></div></body>");

        // The receiver comes last, so its next sibling is only null once the conversion has moved it into the
        // fragment - which is where AngleSharp reads it, and why it then looks for a node that has left.
        fixture.Text(
            """
            var c = document.getElementById('c');
            c.after(c, document.getElementById('x'));
            document.getElementById('p').innerHTML;
            """).Should().Be("<b id=\"c\"></b><i id=\"x\"></i>");
    }

    [Test]
    public void ReplaceWithAcceptsTheReceiverAmongItsArguments()
    {
        using var fixture = DomTestFixture.Create("<body><div id='p'><i id='x'></i><b id='c'></b></div></body>");

        // The receiver is replaced by a fragment it is itself in, so the last step's "is this still a child of
        // parent" question is the one that decides between a replace and a pre-insert.
        fixture.Text(
            """
            var c = document.getElementById('c');
            c.replaceWith(document.getElementById('x'), c);
            document.getElementById('p').innerHTML;
            """).Should().Be("<i id=\"x\"></i><b id=\"c\"></b>");
    }

    [Test]
    public void ADetachedReceiverDoesNothing()
    {
        using var fixture = DomTestFixture.Create("<body></body>");

        fixture.Text(
            """
            var x = document.createElement('x');
            var y = document.createElement('y');
            x.before(y);
            x.after(y);
            x.replaceWith(y);
            [x.previousSibling, x.nextSibling, y.parentNode].map(v => String(v)).join('|');
            """).Should().Be("null|null|null");
    }

    [Test]
    public void ReplaceWithNoArgumentsRemovesTheReceiver()
    {
        using var fixture = DomTestFixture.Create("<body><div id='p'><b id='c'></b></div></body>");

        fixture.Text(
            """
            document.getElementById('c').replaceWith();
            document.getElementById('p').innerHTML;
            """).Should().Be("");
    }
}
