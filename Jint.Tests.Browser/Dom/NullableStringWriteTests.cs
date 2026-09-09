namespace Jint.Tests.Browser.Dom;

/// <summary>
/// The two IDL attributes whose setter turns JavaScript <c>null</c> into the empty string —
/// <a href="https://dom.spec.whatwg.org/#dom-characterdata-data">DOM §4.10</a>'s
/// <c>[LegacyNullToEmptyString] data</c> and
/// <a href="https://dom.spec.whatwg.org/#dom-node-nodevalue">DOM §4.4</a>'s <c>nodeValue</c>, whose first
/// setter step says the same thing in prose — plus the indexed getter that must not throw.
/// </summary>
public sealed class NullableStringWriteTests
{
    [TestCase("document.createTextNode('test')")]
    [TestCase("document.createComment('test')")]
    [TestCase("document.createProcessingInstruction('pi', 'test')")]
    public void WritingNullEmptiesTheNodeAndUndefinedDoesNot(string create)
    {
        using var fixture = DomTestFixture.Create("<body></body>");

        fixture.Text($"var node = {create}; node.data = null; node.data").Should().Be("");
        fixture.Number("node.length").Should().Be(0);

        // Only `null` is named by the extended attribute; `undefined` converts the ordinary way.
        fixture.Text("node.data = undefined; node.data").Should().Be("undefined");
        fixture.Text("node.data = 0; node.data").Should().Be("0");
    }

    [TestCase("document.createTextNode('test')")]
    [TestCase("document.createComment('test')")]
    [TestCase("document.createProcessingInstruction('pi', 'test')")]
    public void WritingNullToNodeValueEmptiesTheNode(string create)
    {
        using var fixture = DomTestFixture.Create("<body></body>");

        fixture.Text($"var node = {create}; node.nodeValue = null; node.nodeValue").Should().Be("");
        fixture.Text("node.data").Should().Be("");
    }

    /// <summary>The getter half stays nullable: DOM gives an element and a document no node value at all.</summary>
    [Test]
    public void NodeValueStaysNullForANodeThatHasNone()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><body><a id='a'></a></body>");

        fixture.Evaluate("document.getElementById('a').nodeValue").IsNull().Should().BeTrue();
        fixture.Evaluate("document.getElementById('a').nodeValue = 'foo'; document.getElementById('a').nodeValue")
            .IsNull().Should().BeTrue();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-nodelist-item — an indexed property getter answers the nullable
    /// return type's null past the end, where the CLR indexer behind it raises.
    /// </summary>
    [Test]
    public void NodeListItemAnswersNullPastTheEnd()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><body><div id='p'><b></b></div></body>");

        fixture.Bool(
            """
            var children = document.getElementById('p').childNodes;
            children.item(0) === children[0]
                && children.item(1) === null
                && children.item(-1) === null
                && children[1] === undefined;
            """).Should().BeTrue();
    }
}
