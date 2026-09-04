namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#staticrange">DOM §5.3's <c>StaticRange</c></a> and the
/// <a href="https://dom.spec.whatwg.org/#abstractrange">§5.1 <c>AbstractRange</c></a> members it answers
/// with, as a page sees them.
/// </summary>
/// <remarks>
/// AngleSharp has no <c>StaticRange</c> at all — no type, no <c>[DomName]</c> — so the generator can never
/// see one and this is a <c>DomManualInterfaces</c> row, the way <c>HTMLFrameSetElement</c> is. The
/// distinction the interface exists for is that a static range is <b>four values and nothing else</b>: it
/// holds no reference the tree can invalidate, it is never validated against a container's length, and
/// unlike a <c>Range</c> it does not move when the tree beneath it does.
/// </remarks>
public sealed class StaticRangeTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="testDiv">abc<span>def</span>ghi</div></body></html>
        """;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-staticrange-staticrange: the interface object is on the window, it
    /// is constructible, and an instance reports itself as one.
    /// </summary>
    [TestCase("typeof StaticRange", "function")]
    [TestCase("StaticRange.name", "StaticRange")]
    [TestCase("StaticRange.length", "1")]
    [TestCase("new StaticRange({startContainer: document, startOffset: 0, endContainer: document, endOffset: 0}) instanceof StaticRange", "true")]
    [TestCase("Object.prototype.toString.call(new StaticRange({startContainer: document, startOffset: 0, endContainer: document, endOffset: 0}))", "[object StaticRange]")]
    [TestCase("StaticRange.prototype.constructor === StaticRange", "true")]
    public void TheInterfaceObjectIsOnTheWindowAndConstructible(string source, string expected)
    {
        Answer(source).Should().Be(expected);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#abstractrange: the five readonly attributes, answered from the four
    /// values the constructor was handed and never from the tree.
    /// </summary>
    [TestCase("Start(1, 2).startContainer === testDiv", "true")]
    [TestCase("Start(1, 2).startOffset", "1")]
    [TestCase("Start(1, 2).endContainer === testDiv", "true")]
    [TestCase("Start(1, 2).endOffset", "2")]
    [TestCase("Start(1, 2).collapsed", "false")]
    [TestCase("Start(0, 0).collapsed", "true")]
    [TestCase("Start(2, 2).collapsed", "true")]
    public void TheFourValuesRoundTripAndCollapsedIsDerived(string source, string expected)
    {
        Answer(source).Should().Be(expected);
    }

    /// <summary>
    /// The wrapper identity a page compares against: the container it gets back is the very object it
    /// passed in, which is what <c>staticRange.startContainer === testDiv</c> asks.
    /// </summary>
    [TestCase("text", TestName = "a Text container round-trips")]
    [TestCase("document", TestName = "a Document container round-trips")]
    [TestCase("document.createComment('abc')", TestName = "a Comment container round-trips")]
    [TestCase("document.createDocumentFragment()", TestName = "a DocumentFragment container round-trips")]
    [TestCase("document.createElement('div')", TestName = "a detached Element container round-trips")]
    [TestCase("document.createProcessingInstruction('foo', 'abc')", TestName = "a ProcessingInstruction container round-trips")]
    public void AContainerIsHandedBackAsTheSameObject(string container)
    {
        Answer($$"""
            (function () {
              var n = {{container}};
              var r = new StaticRange({startContainer: n, startOffset: 0, endContainer: n, endOffset: 0});
              return r.startContainer === n && r.endContainer === n;
            })()
            """).Should().Be("true");
    }

    /// <summary>
    /// The offsets are WebIDL <c>unsigned long</c>s and nothing else: a static range is never validated
    /// against its container's length, so an offset past the end is kept as given.
    /// </summary>
    [TestCase("Start(0, 15).endOffset", "15")]
    [TestCase("Start(1, 0).startOffset", "1")]
    public void AnOffsetIsNeverValidatedAgainstTheContainer(string source, string expected)
    {
        Answer(source).Should().Be(expected);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-staticrange-staticrange step 1: a <c>DocumentType</c> or an
    /// <c>Attr</c> is an <c>InvalidNodeTypeError</c>, and it is the only refusal the constructor makes.
    /// </summary>
    [TestCase("document.doctype", TestName = "a DocumentType container is refused")]
    [TestCase("document.getElementById('testDiv').getAttributeNode('id')", TestName = "an Attr container is refused")]
    public void ADocumentTypeOrAttrContainerIsAnInvalidNodeTypeError(string container)
    {
        Refusal($$"""
            (function () {
              var n = {{container}};
              return new StaticRange({startContainer: n, startOffset: 0, endContainer: n, endOffset: 0});
            })()
            """).Should().Be("InvalidNodeTypeError");
    }

    /// <summary>
    /// Every member of <c>StaticRangeInit</c> is <c>required</c> and the containers are non-nullable
    /// <c>Node</c>s, so WebIDL's conversion refuses before the constructor's own step 1 ever runs — which is
    /// why these are <c>TypeError</c>s and not <c>InvalidNodeTypeError</c>s.
    /// </summary>
    [TestCase("new StaticRange()", TestName = "no argument at all is a TypeError")]
    [TestCase("new StaticRange({startOffset: 0, endContainer: testDiv, endOffset: 0})", TestName = "a missing startContainer is a TypeError")]
    [TestCase("new StaticRange({startContainer: testDiv, endContainer: testDiv, endOffset: 0})", TestName = "a missing startOffset is a TypeError")]
    [TestCase("new StaticRange({startContainer: testDiv, startOffset: 0, endOffset: 0})", TestName = "a missing endContainer is a TypeError")]
    [TestCase("new StaticRange({startContainer: testDiv, startOffset: 0, endContainer: testDiv})", TestName = "a missing endOffset is a TypeError")]
    [TestCase("new StaticRange({startContainer: null, startOffset: 0, endContainer: testDiv, endOffset: 0})", TestName = "a null startContainer is a TypeError")]
    [TestCase("new StaticRange({startContainer: testDiv, startOffset: 0, endContainer: null, endOffset: 0})", TestName = "a null endContainer is a TypeError")]
    [TestCase("new StaticRange({startContainer: 'div', startOffset: 0, endContainer: testDiv, endOffset: 0})", TestName = "a container that is not a Node is a TypeError")]
    public void AMissingOrNonNodeMemberIsATypeError(string source)
    {
        Refusal(source).Should().Be("TypeError");
    }

    /// <summary>Calling the constructor without <c>new</c> is WebIDL's <c>TypeError</c>.</summary>
    [Test]
    public void TheConstructorRefusesToBeCalledAsAFunction()
    {
        Refusal("StaticRange({startContainer: document, startOffset: 0, endContainer: document, endOffset: 0})")
            .Should().Be("TypeError");
    }

    /// <summary>
    /// The endpoints are kept as handed over, so a range whose end precedes its start, and one whose
    /// endpoints are in different trees, are both ordinary static ranges rather than refusals.
    /// </summary>
    [Test]
    public void InvertedAndDisconnectedEndpointsAreKept()
    {
        Answer("""
            (function () {
              var other = document.createElement('div');
              var inverted = new StaticRange({startContainer: testDiv, startOffset: 1, endContainer: document.body, endOffset: 0});
              var split = new StaticRange({startContainer: testDiv, startOffset: 1, endContainer: other, endOffset: 2});
              return [
                inverted.startContainer === testDiv,
                inverted.endContainer === document.body,
                inverted.collapsed,
                split.endContainer === other,
                split.endOffset,
              ].join('|');
            })()
            """).Should().Be("true|true|false|true|2");
    }

    /// <summary>
    /// The five attributes are readonly accessors on the prototype, which is where WebIDL puts them — not
    /// own properties of the instance.
    /// </summary>
    [Test]
    public void TheAttributesLiveOnThePrototype()
    {
        Answer("""
            (function () {
              var r = new StaticRange({startContainer: document, startOffset: 0, endContainer: document, endOffset: 0});
              var names = ['startContainer', 'startOffset', 'endContainer', 'endOffset', 'collapsed'];
              return names.map(function (n) {
                var d = Object.getOwnPropertyDescriptor(StaticRange.prototype, n);
                return d && typeof d.get === 'function' && d.set === undefined
                  && !Object.prototype.hasOwnProperty.call(r, n);
              }).join('|');
            })()
            """).Should().Be("true|true|true|true|true");
    }

    private const string Preamble = """
        var testDiv = document.getElementById('testDiv');
        var text = testDiv.firstChild;
        function Start(a, b) {
          return new StaticRange({startContainer: testDiv, startOffset: a, endContainer: testDiv, endOffset: b});
        }
        """;

    private static string? Answer(string source)
    {
        using var fixture = DomTestFixture.Create(Page);

        return fixture.Text($$"""
            (function () {
              {{Preamble}}
              try { return String({{source}}); }
              catch (e) { return e.constructor.name + ': ' + e.message; }
            })()
            """);
    }

    private static string? Refusal(string source)
    {
        using var fixture = DomTestFixture.Create(Page);

        var answer = fixture.Text($$"""
            (function () {
              {{Preamble}}
              try { {{source}}; return ''; }
              catch (e) { return e.name || String(e); }
            })()
            """);

        return string.IsNullOrEmpty(answer) ? null : answer;
    }
}
