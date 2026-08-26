using System.Buffers;
using System.Globalization;
using System.Text;
using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The span overloads of <see cref="JsonParser.Parse(ReadOnlySpan{char})"/> and
/// <see cref="JsonParser.Parse(ReadOnlySpan{byte})"/> exist so a host holding a document in a buffer — a
/// UTF-8 event payload, most typically — does not have to materialize it as a string first. Their contract
/// is that they produce exactly what <see cref="JsonParser.Parse(string)"/> produces for the same
/// characters, so almost every test here is a comparison against that overload rather than against a
/// hand-written expectation.
/// </summary>
public class JsonSpanParsingTests
{
    public static TestCases<string> Documents => new()
    {
        // primitives, including the number shapes the scanner special-cases
        "null",
        "true",
        "false",
        "0",
        "-0",
        "1",
        "-1",
        "1.5",
        "-1.5",
        "1e3",
        "1E+3",
        "1e-3",
        "-1.5e-7",
        "0.1",
        "9007199254740991",
        "12345678901234567890",
        "1.7976931348623157e308",
        "\"\"",
        "\"plain\"",

        // whitespace in every position the JSON grammar allows it
        " \t\r\n{\"a\" : 1 , \"b\" : [ 1 , 2 ] }\t\r\n ",
        "[]",
        "{}",
        "[ ]",
        "{ }",

        // every escape the scanner decodes
        "\"\\\"\\\\\\/\\b\\f\\n\\r\\t\"",
        "\"\\u0000\\u001f\\u0061\\u003C\\u003e\\u003c\"",
        "\"a\\u0000b\\u001Fc\"",
        "\"\\ud83d\\ude00\"",
        "{\"\\u0041\\tb\":\"v\"}",

        // raw non-ASCII, which is exactly what the UTF-8 lane has to carry
        "\"h\u00e9llo\"",
        "\"\u4e2d\u6587\"",
        "\"\ud83d\ude00\"",
        "\"\u2028\u2029\"",
        "{\"\ud83d\ude00\":\"\u4e2d\"}",

        // structure, duplicate and integer-like keys, __proto__
        "{\"a\":1,\"b\":[1,2,3],\"c\":{\"d\":null},\"e\":true}",
        "[[[[[1]]]]]",
        "[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"name\":\"b\"},{\"id\":3,\"name\":\"a\"}]",
        "{\"a\":1,\"a\":2}",
        "{\"__proto__\":{\"x\":1}}",
        "{\"2\":\"b\",\"1\":\"a\",\"x\":\"c\"}",
        "[1,\"1\",true,null,{},[]]",
    };

    public static TestCases<string> MalformedDocuments => new()
    {
        "",
        " ",
        "{",
        "[",
        "{\"a\":1",
        "{\"a\":1},",
        "{1}",
        "{\"a\" \"a\"}",
        "{true}",
        "{:}",
        "\"\\uah\"",
        "0123",
        "1e+A",
        "1.",
        "truE",
        "nul",
        "\"ab\t\"",
        "\"ab",
        "alpha",
        "{\"a\":}",
        "{}\ufeff",
    };

    [TestCaseSource(nameof(Documents))]
    public void ParsingACharSpanMatchesParsingTheString(string json)
    {
        var engine = new Engine();

        var fromString = Describe(engine, new JsonParser(engine).Parse(json));
        var fromSpan = Describe(engine, new JsonParser(engine).Parse(json.AsSpan()));

        fromSpan.Should().Be(fromString);
    }

    [TestCaseSource(nameof(Documents))]
    public void ParsingUtf8MatchesParsingTheString(string json)
    {
        var engine = new Engine();

        var fromString = Describe(engine, new JsonParser(engine).Parse(json));
        var fromUtf8 = Describe(engine, new JsonParser(engine).Parse(Encoding.UTF8.GetBytes(json)));

        fromUtf8.Should().Be(fromString);
    }

    [TestCaseSource(nameof(MalformedDocuments))]
    public void ACharSpanIsRejectedExactlyLikeTheString(string json)
    {
        var engine = new Engine();

        var fromString = Invoking(() => new JsonParser(engine).Parse(json)).Should().Throw<JavaScriptException>().Which;
        var fromSpan = Invoking(() => new JsonParser(engine).Parse(json.AsSpan())).Should().Throw<JavaScriptException>().Which;

        // the message carries the failing position, so this pins the error offsets too
        fromSpan.Message.Should().Be(fromString.Message);
    }

    [TestCaseSource(nameof(MalformedDocuments))]
    public void Utf8IsRejectedExactlyLikeTheString(string json)
    {
        var engine = new Engine();

        var fromString = Invoking(() => new JsonParser(engine).Parse(json)).Should().Throw<JavaScriptException>().Which;
        var fromUtf8 = Invoking(() => new JsonParser(engine).Parse(Encoding.UTF8.GetBytes(json))).Should().Throw<JavaScriptException>().Which;

        fromUtf8.Message.Should().Be(fromString.Message);
    }

    /// <summary>
    /// U+FEFF is not JSON whitespace, so a leading one is a syntax error for the character overloads just
    /// as it is for the string overload. Only the UTF-8 overload strips a mark, and it strips the byte
    /// sequence rather than the decoded character.
    /// </summary>
    [Test]
    public void ALeadingByteOrderMarkIsASyntaxErrorForTheCharacterOverloads()
    {
        var engine = new Engine();
        var json = (char) 0xFEFF + "{}";

        var fromString = Invoking(() => new JsonParser(engine).Parse(json)).Should().Throw<JavaScriptException>().Which;
        Invoking(() => new JsonParser(engine).Parse(json.AsSpan()))
            .Should().Throw<JavaScriptException>().WithMessage(fromString.Message);

        // the same document handed over as UTF-8 bytes parses, because the mark is stripped there
        new JsonSerializer(engine).Serialize(new JsonParser(engine).Parse(Encoding.UTF8.GetBytes(json)))
            .AsString().Should().Be("{}");
    }

    /// <summary>
    /// The scanner reads straight out of the caller's span instead of copying the document. An allocation
    /// count would be a brittle way to pin that, so this pins the half of it a caller actually depends on:
    /// nothing the parse produces keeps a view on the buffer, which is what makes it safe to hand over a
    /// pooled or stack buffer and reuse it immediately afterwards.
    /// </summary>
    [Test]
    public void TheDocumentBufferIsNotRetainedAfterTheParse()
    {
        var engine = new Engine();
        var buffer = new char[64];
        "{\"a\":\"value\"}".AsSpan().CopyTo(buffer);

        var parsed = new JsonParser(engine).Parse(buffer.AsSpan(0, 13));

        // scribble over the caller's buffer: a parser that had kept a view on it would be visible here
        buffer.AsSpan().Fill('\0');

        new JsonSerializer(engine).Serialize(parsed).AsString().Should().Be("{\"a\":\"value\"}");
    }

    [Test]
    public void ANullStringIsRejected()
    {
        var parser = new JsonParser(new Engine());

        Invoking(() => parser.Parse((string) null)).Should().Throw<ArgumentNullException>();
    }

    #region UTF-8 specifics

    [Test]
    public void SkipsALeadingByteOrderMark()
    {
        var engine = new Engine();
        const string Json = "{\"a\":[1,2,\"h\u00e9llo \ud83d\ude00\"]}";

        var withBom = WithBom(Encoding.UTF8.GetBytes(Json));

        Describe(engine, new JsonParser(engine).Parse(withBom))
            .Should().Be(Describe(engine, new JsonParser(engine).Parse(Json)));
    }

    [Test]
    public void SkipsOnlyOneByteOrderMark()
    {
        var engine = new Engine();
        var twoMarks = WithBom(WithBom(Encoding.UTF8.GetBytes("{}")));

        // the second mark decodes to U+FEFF, which is not JSON whitespace
        Invoking(() => new JsonParser(engine).Parse(twoMarks))
            .Should().Throw<JavaScriptException>()
            .WithMessage("*at position 0");
    }

    [Test]
    public void AByteOrderMarkInsideTheDocumentIsASyntaxError()
    {
        var engine = new Engine();
        var bytes = Encoding.UTF8.GetBytes("[1,\ufeff2]");

        Invoking(() => new JsonParser(engine).Parse(bytes)).Should().Throw<JavaScriptException>();
    }

    [Test]
    public void EmptyInputIsRejectedTheSameWayByEveryOverload()
    {
        var engine = new Engine();
        var expected = Invoking(() => new JsonParser(engine).Parse("")).Should().Throw<JavaScriptException>().Which.Message;

        expected.Should().Be("Unexpected end of JSON input at position 0");

        Invoking(() => new JsonParser(engine).Parse(ReadOnlySpan<char>.Empty))
            .Should().Throw<JavaScriptException>().WithMessage(expected);

        Invoking(() => new JsonParser(engine).Parse(ReadOnlySpan<byte>.Empty))
            .Should().Throw<JavaScriptException>().WithMessage(expected);

        // a document that is nothing but a byte order mark is an empty document
        Invoking(() => new JsonParser(engine).Parse(WithBom([])))
            .Should().Throw<JavaScriptException>().WithMessage(expected);
    }

    public static TestCases<byte[], string> InvalidUtf8 => new()
    {
        { [0xFF], "a byte that never starts a sequence" },
        { [0x22, 0x80, 0x22], "a lone continuation byte" },
        { [0x22, 0xC3, 0x22], "a truncated two-byte sequence" },
        { [0x22, 0xE2, 0x82, 0x22], "a truncated three-byte sequence" },
        { [0x22, 0xF0, 0x9F, 0x98, 0x22], "a truncated four-byte sequence" },
        { [0x22, 0xC0, 0xAF, 0x22], "an overlong encoding" },
        { [0x22, 0xED, 0xA0, 0x80, 0x22], "a surrogate encoded as UTF-8" },
        { [0x22, 0xF5, 0x80, 0x80, 0x80, 0x22], "a code point beyond U+10FFFF" },
        { [0x5B, 0x31, 0x2C, 0xC3, 0x5D], "a bad sequence in the middle of a document" },
        { [0x22, 0x61, 0x62, 0x63, 0xE2], "a sequence truncated by the end of the input" },
    };

    [TestCaseSource(nameof(InvalidUtf8))]
    public void InvalidUtf8IsReportedAsASyntaxError(byte[] bytes, string because)
    {
        var engine = new Engine();

        var exception = Invoking(() => new JsonParser(engine).Parse(bytes))
            .Should().Throw<JavaScriptException>(because).Which;

        exception.Message.Should().StartWith("Invalid UTF-8 sequence in JSON at position ");
        exception.Error.Should().BeOfType<JsError>()
            .Which.Get("name").ToString().Should().Be("SyntaxError");
    }

    /// <summary>
    /// The decoder's own <see cref="DecoderFallbackException"/> is an <see cref="ArgumentException"/>,
    /// which script cannot catch; the overload contracts to raise the parser's own SyntaxError instead, so
    /// a host wrapping the call in a script-visible function behaves like <c>JSON.parse</c> does.
    /// </summary>
    [Test]
    public void InvalidUtf8IsCatchableFromScript()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);
        byte[] bytes = [0x5B, 0x22, 0xC3, 0x22, 0x5D];

        engine.SetValue("hostParse", new Func<JsValue>(() => parser.Parse(bytes)));

        var result = engine.Evaluate(
            """
            try {
                hostParse();
                'no throw';
            } catch (e) {
                (e instanceof SyntaxError) + '|' + e.message;
            }
            """).AsString();

        result.Should().StartWith("true|Invalid UTF-8 sequence in JSON at position ");
    }

    [Test]
    public void InvalidUtf8ReportsTheByteOffsetOfTheOffendingSequence()
    {
        var engine = new Engine();

        // '[' '1' ',' '1' ',' then a lone continuation byte at index 5
        Invoking(() => new JsonParser(engine).Parse((byte[]) [0x5B, 0x31, 0x2C, 0x31, 0x2C, 0x80, 0x5D]))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Invalid UTF-8 sequence in JSON at position 5");

        // the offset is counted after the byte order mark, so the same document reports the same position
        Invoking(() => new JsonParser(engine).Parse(WithBom([0x5B, 0x31, 0x2C, 0x31, 0x2C, 0x80, 0x5D])))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Invalid UTF-8 sequence in JSON at position 5");
    }

    #endregion

    #region Buffer sizing

    /// <summary>
    /// Short documents transcode into a stack buffer and longer ones into a pooled array. The threshold is
    /// an implementation detail, so these sizes bracket it generously from both sides rather than probing
    /// for it.
    /// </summary>
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(64)]
    [TestCase(254)]
    [TestCase(255)]
    [TestCase(256)]
    [TestCase(257)]
    [TestCase(258)]
    [TestCase(512)]
    [TestCase(4096)]
    [TestCase(70_000)]
    public void TranscodesAsciiDocumentsOfAnySize(int byteLength)
    {
        var json = "[\"" + new string('x', byteLength - 4) + "\"]";
        var bytes = Encoding.UTF8.GetBytes(json);
        bytes.Length.Should().Be(byteLength);

        var engine = new Engine();
        Describe(engine, new JsonParser(engine).Parse(bytes))
            .Should().Be(Describe(engine, new JsonParser(engine).Parse(json)));
    }

    /// <summary>
    /// Multi-byte content makes the decoded char count strictly smaller than the byte count, which is the
    /// direction the buffer sizing relies on (a UTF-8 sequence never yields more chars than it has bytes).
    /// </summary>
    [TestCase(1)]
    [TestCase(20)]
    [TestCase(28)]
    [TestCase(29)]
    [TestCase(30)]
    [TestCase(31)]
    [TestCase(200)]
    [TestCase(5000)]
    public void TranscodesMultiByteDocumentsOfAnySize(int repeats)
    {
        var padding = string.Concat(Enumerable.Repeat("\u00e9\u4e2d\ud83d\ude00", repeats));
        var json = "[\"" + padding + "\"]";
        var bytes = Encoding.UTF8.GetBytes(json);

        // 2 + 3 + 4 bytes per repeat, but only 1 + 1 + 2 chars
        bytes.Length.Should().Be(4 + (9 * repeats));

        var engine = new Engine();
        Describe(engine, new JsonParser(engine).Parse(bytes))
            .Should().Be(Describe(engine, new JsonParser(engine).Parse(json)));
    }

    /// <summary>
    /// The pooled buffer is rented before transcoding and must come back on the throwing paths too — both
    /// the one that fails inside the decoder and the one that fails later in the parser. A buffer lost to
    /// the pool would not fail here directly, but a buffer returned twice or handed out while still in use
    /// would corrupt the parse that follows, which is what this checks.
    /// </summary>
    [Test]
    public void AFailedLargeParseLeavesThePoolUsable()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);
        var padding = new string('x', 5000);

        var badEncoding = Encoding.UTF8.GetBytes("[\"" + padding + "\"").Concat((byte[]) [0xC3, 0x5D]).ToArray();
        var badSyntax = Encoding.UTF8.GetBytes("[\"" + padding + "\" \"y\"]");
        var good = Encoding.UTF8.GetBytes("[\"" + padding + "\"]");

        for (var i = 0; i < 3; i++)
        {
            Invoking(() => parser.Parse(badEncoding)).Should().Throw<JavaScriptException>();
            Invoking(() => parser.Parse(badSyntax)).Should().Throw<JavaScriptException>();

            var parsed = parser.Parse(good);
            new JsonSerializer(engine).Serialize(parsed).AsString().Should().Be("[\"" + padding + "\"]");
        }
    }

    #endregion

    [Test]
    public void OneParserInstanceServesEveryOverload()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);
        var serializer = new JsonSerializer(engine);

        // the per-parse intern tables are reset by every entry point, so repeated keys and values across
        // consecutive parses through different overloads must not leak into one another
        const string First = "{\"key\":\"value\",\"other\":\"value\"}";
        const string Second = "{\"key\":\"different\",\"other\":\"value\"}";

        serializer.Serialize(parser.Parse(First)).AsString().Should().Be(First);
        serializer.Serialize(parser.Parse(Second.AsSpan())).AsString().Should().Be(Second);
        serializer.Serialize(parser.Parse(Encoding.UTF8.GetBytes(First))).AsString().Should().Be(First);
        serializer.Serialize(parser.Parse(Second)).AsString().Should().Be(Second);
    }

    [Test]
    public void RoundTripsAValueThroughTheUtf8Serializer()
    {
        var engine = new Engine();
        var value = engine.Evaluate(
            "({ id: 7, name: 'h\\u00e9llo \\uD83D\\uDE00 \\u4E2D', items: [1, 2.5, null, true, ''], nested: { a: [{ b: 'x' }] } })");

        var writer = new TestBufferWriter();
        new JsonSerializer(engine).Serialize(value, writer).Should().BeTrue();

        var reparsed = new JsonParser(engine).Parse(writer.WrittenSpan);

        Describe(engine, reparsed).Should().Be(Describe(engine, value));
    }

    [Test]
    public void RoundTripsALargeValueThroughTheUtf8Serializer()
    {
        var engine = new Engine();
        var value = engine.Evaluate(
            "(function () { var a = []; for (var i = 0; i < 2000; i++) { a.push({ id: i, name: 'v\\uD83D\\uDE00' + i }); } return a; })()");

        var writer = new TestBufferWriter();
        new JsonSerializer(engine).Serialize(value, writer).Should().BeTrue();
        writer.WrittenSpan.Length.Should().BeGreaterThan(256);

        var reparsed = new JsonParser(engine).Parse(writer.WrittenSpan);

        Describe(engine, reparsed).Should().Be(Describe(engine, value));
    }

    /// <summary>
    /// A structural fingerprint of a parsed value: the JSON the serializer produces for it, plus the exact
    /// bits of a number so that <c>-0</c> is distinguishable from <c>0</c> (which JSON cannot express).
    /// </summary>
    private static string Describe(Engine engine, JsValue value)
    {
        var serialized = new JsonSerializer(engine).Serialize(value);
        var text = serialized.IsUndefined() ? "<undefined>" : serialized.AsString();

        if (value.IsNumber())
        {
            text += "|" + BitConverter.DoubleToInt64Bits(value.AsNumber()).ToString(CultureInfo.InvariantCulture);
        }

        return value.Type + "|" + text;
    }

    private static byte[] WithBom(byte[] bytes) => [0xEF, 0xBB, 0xBF, .. bytes];

    private sealed class TestBufferWriter : IBufferWriter<byte>
    {
        private byte[] _buffer = new byte[16];

        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, WrittenCount);

        private int WrittenCount { get; set; }

        public void Advance(int count) => WrittenCount += count;

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            // Prepare can replace _buffer, so it has to run before the receiver is read
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
            var wanted = System.Math.Max(sizeHint, 1);
            if (_buffer.Length - WrittenCount < wanted)
            {
                Array.Resize(ref _buffer, System.Math.Max(_buffer.Length * 2, WrittenCount + wanted));
            }

            return _buffer.Length - WrittenCount;
        }
    }
}
