#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>application/x-www-form-urlencoded</c> arm of <c>formData()</c>:
/// https://fetch.spec.whatwg.org/#concept-body-package-data runs
/// https://url.spec.whatwg.org/#concept-urlencoded-parser over the body's bytes and returns a
/// <c>FormData</c> whose entry list is what came back.
/// </summary>
/// <remarks>
/// <para>
/// The parser is <c>URLSearchParams</c>'s own, which is the point: a browser answers
/// <c>new URLSearchParams(s)</c>, <c>new Request(url, { body: s }).formData()</c> and
/// <c>new Response(s).formData()</c> with the same tuples, and
/// <c>url/urlencoded-parser.any.js</c> asserts exactly that by feeding one corpus of 35 inputs through all
/// three. What is pinned here is the part of that agreement the vendored corpus does not reach — the
/// <c>Content-Type</c> that selects the arm, an empty and a null body, and the bytes a browser is never
/// asked to parse.
/// </para>
/// <para>
/// The sibling arm, <c>multipart/form-data</c>, is in <see cref="MultipartTests"/>.
/// </para>
/// </remarks>
public class UrlEncodedBodyTests
{
    private static Engine WebEngine() => new(options => options.UseFetch());

    /// <summary>The entry list of <paramref name="expression"/>'s <c>formData()</c>, as JSON.</summary>
    private static string Entries(Engine engine, string expression)
        => engine.Evaluate($"({expression}).formData().then(fd => JSON.stringify([...fd]))")
            .UnwrapIfPromise()
            .AsString();

    /// <summary>
    /// A response carrying <paramref name="body"/> under an <c>application/x-www-form-urlencoded</c> type,
    /// with whatever <paramref name="parameters"/> the header should also carry.
    /// </summary>
    private static string Response(string body, string parameters = "")
        => $"new Response('{body}', {{ headers: {{ 'content-type': 'application/x-www-form-urlencoded{parameters}' }} }})";

    [Test]
    public void PlusIsASpaceAndPercentEscapesAreDecoded()
    {
        var engine = WebEngine();

        // Both halves of a tuple are decoded, and a repeated name keeps both entries in order.
        Entries(engine, Response("a=1&b=hello+world&a=%C3%A9"))
            .Should().Be("""[["a","1"],["b","hello world"],["a","é"]]""");

        // 0x2B (+) becomes 0x20 (SP) in the name as well as in the value, and the replacement happens
        // *before* the percent-decoding — so "%2B" is a plus and "+" is a space, never the other way round.
        Entries(engine, Response("d+e=f&%2B=%2B"))
            .Should().Be("""[["d e","f"],["+","+"]]""");
    }

    [Test]
    public void TheEssenceAloneSelectsTheParserAndEveryParameterIsIgnored()
    {
        var engine = WebEngine();

        // The two legacy charsets url/urlencoded-parser.any.js sends. The parser is UTF-8 only — the only
        // conforming encoding — so "%C3%A9" is U+00E9 whatever the header claims.
        Entries(engine, Response("a=%C3%A9", ";charset=windows-1252")).Should().Be("""[["a","é"]]""");
        Entries(engine, Response("a=%C3%A9", ";charset=shift_jis")).Should().Be("""[["a","é"]]""");

        // A MIME type's essence is ASCII-lowercased, and a parameter that means something to the *other*
        // arm means nothing to this one.
        Entries(engine, "new Response('a=1', { headers: { 'content-type': 'APPLICATION/X-WWW-FORM-URLENCODED' } })")
            .Should().Be("""[["a","1"]]""");
        Entries(engine, Response("a=1", ";boundary=\"nonsense\"; q=3")).Should().Be("""[["a","1"]]""");

        // Leading and trailing HTTP whitespace is removed before the type is parsed.
        Entries(engine, "new Response('a=1', { headers: { 'content-type': '  application/x-www-form-urlencoded  ' } })")
            .Should().Be("""[["a","1"]]""");
    }

    [Test]
    public void AnEmptyBodyIsAnEmptyFormDataRatherThanAFailure()
    {
        var engine = WebEngine();

        Entries(engine, Response("")).Should().Be("[]");

        // A null body is not the same as an empty one — consume-body runs the success steps over an empty
        // byte sequence without disturbing anything — but it packages to the same empty entry list.
        Entries(engine, "new Response(null, { headers: { 'content-type': 'application/x-www-form-urlencoded' } })")
            .Should().Be("[]");
    }

    [Test]
    public void ATupleWithNoEqualsHasAnEmptyValueAndAnEmptySequenceIsSkipped()
    {
        var engine = WebEngine();

        // "Otherwise, let name have the value of bytes and let value be the empty byte sequence" — and step
        // 5.1 drops an empty sequence, so neither the trailing nor the doubled "&" produces an entry.
        Entries(engine, Response("a&b=1&")).Should().Be("""[["a",""],["b","1"]]""");
        Entries(engine, Response("&&a=1&&")).Should().Be("""[["a","1"]]""");

        // "If 0x3D (=) is the first byte, then name will be the empty byte sequence."
        Entries(engine, Response("=v&k=")).Should().Be("""[["","v"],["k",""]]""");
    }

    [Test]
    public void ArbitraryBytesParseRatherThanFail()
    {
        var engine = WebEngine();

        // The rows url/urlencoded-parser.any.js uses for this: a percent that begins no escape is kept
        // verbatim, and one that begins a partial escape consumes only what it can.
        Entries(engine, Response("id=0&value=%")).Should().Be("""[["id","0"],["value","%"]]""");
        Entries(engine, Response("b=%2sf%2a")).Should().Be("""[["b","%2sf*"]]""");
        Entries(engine, Response("b=%%2a")).Should().Be("""[["b","%*"]]""");

        // An ill-formed UTF-8 run decodes to U+FFFD. There is no failure step in the parser at all, so
        // formData()'s "if entries is failure, then throw a TypeError" has nothing to fire on.
        engine.Evaluate("""
            new Response(new Uint8Array([0x61, 0x3D, 0xFF, 0xFE]),
                { headers: { 'content-type': 'application/x-www-form-urlencoded' } })
                .formData().then(fd => Array.from(fd.get('a'), c => c.charCodeAt(0).toString(16)).join(','))
            """).UnwrapIfPromise().AsString().Should().Be("fffd,fffd");

        // "UTF-8 decode without BOM", so a leading BOM is part of the name rather than stripped from it.
        engine.Evaluate("""
            new Response(new Uint8Array([0xEF, 0xBB, 0xBF, 0x61, 0x3D, 0x31]),
                { headers: { 'content-type': 'application/x-www-form-urlencoded' } })
                .formData().then(fd => (fd.get('a') === null) + '|' + [...fd.keys()][0].charCodeAt(0))
            """).UnwrapIfPromise().AsString().Should().Be("true|65279");
    }

    [Test]
    public void EveryEntryIsAString()
    {
        // "Return a new FormData object whose entry list is entries" — the format cannot carry a file, so
        // no entry is ever a File however the value reads.
        var engine = WebEngine();
        engine.Execute("var parsed = " + Response("a=1&doc=hello") + ".formData();");

        engine.Evaluate("parsed.then(fd => typeof fd.get('a') + ',' + typeof fd.get('doc'))")
            .UnwrapIfPromise().AsString().Should().Be("string,string");
        engine.Evaluate("parsed.then(fd => fd.get('doc') instanceof File)")
            .UnwrapIfPromise().AsBoolean().Should().BeFalse();

        // And it is a FormData of this realm.
        engine.Evaluate("parsed.then(fd => fd instanceof FormData)")
            .UnwrapIfPromise().AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AnyOtherMimeTypeIsATypeError()
    {
        var engine = WebEngine();

        // "Otherwise: throw a TypeError" — including for a body carrying no Content-Type at all, where the
        // bytes would parse perfectly well.
        engine.Evaluate("new Response('a=1').formData().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
        engine.Evaluate("new Response('a=1', { headers: { 'content-type': 'text/plain' } }).formData().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");

        // A near miss is a miss: the essence is compared whole, never by prefix.
        engine.Evaluate("new Response('a=1', { headers: { 'content-type': 'application/x-www-form-urlencoded-v2' } }).formData().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
    }

    [Test]
    public void ARequestParsesItsOwnBodyTheSameWay()
    {
        var engine = WebEngine();

        // The shape url/urlencoded-parser.any.js builds for its request half, custom method and all.
        engine.Evaluate("""
            new Request('about:blank', {
                body: 'a=1&b=hello+world',
                method: 'LADIDA',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=windows-1252' },
            }).formData().then(fd => JSON.stringify([...fd]))
            """).UnwrapIfPromise().AsString().Should().Be("""[["a","1"],["b","hello world"]]""");

        // A URLSearchParams body implies the Content-Type, so it round-trips without one being set by hand.
        engine.Evaluate("new Response(new URLSearchParams({ q: 'a b' })).formData().then(fd => fd.get('q'))")
            .UnwrapIfPromise().AsString().Should().Be("a b");

        // And a body that only ever existed as a stream reaches the same parser through the fully-read path.
        engine.Evaluate("""
            new Response(new Blob(['a=1&b=2']).stream(),
                { headers: { 'content-type': 'application/x-www-form-urlencoded' } })
                .formData().then(fd => JSON.stringify([...fd]))
            """).UnwrapIfPromise().AsString().Should().Be("""[["a","1"],["b","2"]]""");
    }
}
#endif
