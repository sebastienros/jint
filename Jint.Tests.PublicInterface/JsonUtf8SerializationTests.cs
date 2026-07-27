using System.Buffers;
using System.Text;
using Jint.Native;
using Jint.Native.Json;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The UTF-8 overloads of <see cref="JsonSerializer.Serialize(JsValue, IBufferWriter{byte})"/> exist so a
/// host that emits UTF-8 does not have to materialize the document as a string first. Their contract is
/// that the bytes are exactly the UTF-8 encoding of what the string-returning overloads produce, so every
/// test here is a round-trip against that overload rather than against a hand-written expectation.
/// </summary>
public class JsonUtf8SerializationTests
{
    public static TheoryData<string> Documents => new()
    {
        // primitives and empties
        "undefined",
        "null",
        "true",
        "({})",
        "([])",
        "''",
        "0",
        "-0",
        "1.5",
        "1e300",
        "NaN",
        "Infinity",
        "'plain text'",

        // no JSON representation at all
        "(function () {})",
        "(() => 1)",
        "({ a: undefined, b: function () {} })",

        // every escape class the serializer emits
        "'\\n\\t\\\"\\\\\\b\\f\\r'",
        "'\\u0000\\u0001\\u001F'",
        "'\\u007F\\u2028\\u2029\\u00FF\\u0100\\u07FF\\u0800\\uFFFD'",
        "'a\\u0000b\\u001Fc'",

        // surrogates and non-BMP
        "'\\uD83D\\uDE00'",
        "'\\u{1F600}\\u{10FFFF}'",
        "'\\uD800'",
        "'\\uDC00'",
        "'a\\uD800b\\uDC00c'",
        "({ '\\uD800': '\\uDC00' })",
        "({ '\\uD83D\\uDE00': '\\u{10FFFF}' })",

        // structure
        "({ a: 1, b: [1, 2, { c: null }], d: 'x' })",
        "[[[[[[[[[[1]]]]]]]]]]",
        "(function () { var o = {}; var c = o; for (var i = 0; i < 100; i++) { c.next = {}; c = c.next; } return o; })()",

        // documents large enough to need many transcode rounds
        "(function () { var a = []; for (var i = 0; i < 5000; i++) { a.push({ id: i, name: 'value \\uD83D\\uDE00 ' + i }); } return a; })()",
        "(function () { var s = ''; for (var i = 0; i < 4000; i++) { s += '\\uD83D\\uDE00'; } return s; })()",
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void WritesTheSameDocumentAsTheStringOverload(string script)
    {
        AssertRoundTrip(script);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("10")]
    [InlineData("100")]
    [InlineData("0")]
    [InlineData("'\\t'")]
    [InlineData("'--'")]
    [InlineData("'0123456789abcdef'")]
    public void HonoursTheSpaceArgument(string space)
    {
        AssertRoundTrip("({ a: 1, b: [1, { c: 'x\\uD83D\\uDE00' }], d: {}, e: [] })", space: space);
    }

    [Fact]
    public void HonoursAReplacerFunction()
    {
        AssertRoundTrip(
            "({ a: 1, b: 'keep', c: { d: 2 } })",
            replacer: "(function (key, value) { return key === 'a' ? undefined : value; })");
    }

    [Fact]
    public void HonoursAReplacerArray()
    {
        AssertRoundTrip("({ a: 1, b: 2, c: 3 })", replacer: "['c', 'a']");
    }

    [Fact]
    public void HonoursAReplacerAndSpaceTogether()
    {
        AssertRoundTrip(
            "({ a: 1, b: { c: 2, d: 3 } })",
            replacer: "(function (key, value) { return value; })",
            space: "'\\t'");
    }

    [Fact]
    public void ACallableWithAReplacerStillProducesOutput()
    {
        AssertRoundTrip("(function () {})", replacer: "(function (key, value) { return 42; })");
    }

    [Fact]
    public void ReportsNoOutputWithoutTouchingTheWriter()
    {
        var engine = new Engine();
        var writer = new TestBufferWriter();

        var written = new JsonSerializer(engine).Serialize(JsValue.Undefined, writer);

        written.Should().BeFalse();
        writer.WrittenCount.Should().Be(0);
        writer.GetSpanCallCount.Should().Be(0);
    }

    [Fact]
    public void ALargeDocumentIsWrittenInSeveralRounds()
    {
        var engine = new Engine();
        var value = engine.Evaluate("(function () { var a = []; for (var i = 0; i < 5000; i++) { a.push(i); } return a; })()");

        var writer = new TestBufferWriter(exactSpans: true);
        new JsonSerializer(engine).Serialize(value, writer).Should().BeTrue();

        writer.GetSpanCallCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public void ARequestedSpanIsNeverProportionalToTheDocument()
    {
        var engine = new Engine();
        var script = "(function () { var a = []; for (var i = 0; i < 20000; i++) { a.push('abcdefghij'); } return a; })()";
        var value = engine.Evaluate(script);

        var writer = new TestBufferWriter(exactSpans: true);
        new JsonSerializer(engine).Serialize(value, writer).Should().BeTrue();

        writer.WrittenCount.Should().BeGreaterThan(200_000);
        writer.LargestSpanRequest.Should().BeLessThan(8 * 1024);
    }

    [Fact]
    public void ANullWriterIsRejected()
    {
        var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        Invoking(() => serializer.Serialize(JsValue.Undefined, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AWriterHandingBackLessThanTheRequestedSpanIsRejected()
    {
        // GetSpan(sizeHint) must return at least sizeHint bytes. A writer that does not is a contract
        // violation the transcode loop cannot make progress against, so it has to be reported rather
        // than spun on.
        var engine = new Engine();
        var value = engine.Evaluate("({ a: 'x' })");

        Invoking(() => new JsonSerializer(engine).Serialize(value, new ShortSpanBufferWriter()))
            .Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// A deliberately contract-violating writer: <see cref="IBufferWriter{T}.GetSpan"/> ignores the size
    /// hint and hands back an empty span.
    /// </summary>
    private sealed class ShortSpanBufferWriter : IBufferWriter<byte>
    {
        public void Advance(int count)
        {
        }

        public Memory<byte> GetMemory(int sizeHint = 0) => Memory<byte>.Empty;

        public Span<byte> GetSpan(int sizeHint = 0) => Span<byte>.Empty;
    }

    /// <summary>
    /// Strings and property names are always escaped, so an unpaired surrogate cannot reach the output
    /// through them. The <c>space</c> argument is copied verbatim, which is the one route by which the
    /// string overload can produce text that UTF-8 cannot represent; both overloads then agree only after
    /// the encoder's U+FFFD substitution, which is what the documented contract promises.
    /// </summary>
    [Fact]
    public void AnUnpairedSurrogateInTheIndentBecomesTheReplacementCharacter()
    {
        var engine = new Engine();
        var value = engine.Evaluate("({ a: 1 })");
        var space = engine.Evaluate("'\\uD800'");

        var asString = new JsonSerializer(engine).Serialize(value, JsValue.Undefined, space).AsString();
        asString.Should().Contain("\uD800");

        var writer = new TestBufferWriter();
        new JsonSerializer(engine).Serialize(value, JsValue.Undefined, space, writer).Should().BeTrue();

        writer.ToArray().Should().Equal(Encoding.UTF8.GetBytes(asString));
        writer.AsUtf8String().Should().Contain("\uFFFD").And.NotContain("\uD800");
    }

    private static void AssertRoundTrip(string script, string replacer = null, string space = null)
    {
        var engine = new Engine();
        var value = engine.Evaluate(script);
        var replacerValue = replacer is null ? JsValue.Undefined : engine.Evaluate(replacer);
        var spaceValue = space is null ? JsValue.Undefined : engine.Evaluate(space);

        var expected = new JsonSerializer(engine).Serialize(value, replacerValue, spaceValue);

        foreach (var exactSpans in new[] { false, true })
        {
            var writer = new TestBufferWriter(exactSpans);
            var written = new JsonSerializer(engine).Serialize(value, replacerValue, spaceValue, writer);

            if (expected.IsUndefined())
            {
                written.Should().BeFalse($"'{script}' has no JSON representation");
                writer.WrittenCount.Should().Be(0);
                continue;
            }

            written.Should().BeTrue();
            writer.ToArray().Should().Equal(Encoding.UTF8.GetBytes(expected.AsString()));
        }
    }

    /// <summary>
    /// A writer that hands back exactly the requested amount when <paramref name="exactSpans"/> is set,
    /// which is the smallest span the <see cref="IBufferWriter{T}"/> contract allows and therefore forces
    /// the serializer through its chunking loop instead of letting one oversized span absorb everything.
    /// </summary>
    private sealed class TestBufferWriter(bool exactSpans = false) : IBufferWriter<byte>
    {
        private byte[] _buffer = new byte[16];

        public int WrittenCount { get; private set; }

        public int GetSpanCallCount { get; private set; }

        public int LargestSpanRequest { get; private set; }

        public byte[] ToArray() => _buffer.AsSpan(0, WrittenCount).ToArray();

        public string AsUtf8String() => Encoding.UTF8.GetString(_buffer, 0, WrittenCount);

        public void Advance(int count)
        {
            count.Should().BeGreaterThanOrEqualTo(0);
            (WrittenCount + count).Should().BeLessThanOrEqualTo(_buffer.Length);
            WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            var length = Prepare(sizeHint);
            return _buffer.AsMemory(WrittenCount, length);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            var length = Prepare(sizeHint);
            return _buffer.AsSpan(WrittenCount, length);
        }

        private int Prepare(int sizeHint)
        {
            GetSpanCallCount++;
            LargestSpanRequest = System.Math.Max(LargestSpanRequest, sizeHint);

            var wanted = System.Math.Max(sizeHint, 1);
            if (_buffer.Length - WrittenCount < wanted)
            {
                Array.Resize(ref _buffer, System.Math.Max(_buffer.Length * 2, WrittenCount + wanted));
            }

            return exactSpans ? wanted : _buffer.Length - WrittenCount;
        }
    }
}
