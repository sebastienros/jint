#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A string iterates by code point, never by grapheme cluster: <c>String.prototype[%Symbol.iterator%]</c>
/// (https://tc39.es/ecma262/#sec-string.prototype-%symbol.iterator%) builds the iterator of
/// https://tc39.es/ecma262/#sec-createstringiterator, whose every step takes
/// <c>CodePointAt(s, position)</c> (https://tc39.es/ecma262/#sec-codepointat) and advances by its code unit count:
/// two for a surrogate pair, one for anything else, a lone surrogate included. Jint walked the text with
/// <c>StringInfo.GetTextElementEnumerator</c>, which joins a CRLF, a letter and its combining mark, or an emoji
/// sequence held together by U+200D into one element, and which answers differently on each runtime's Unicode data
/// (sebastienros/jint#4228).
/// <para>
/// Every consumer of the string iterator is checked against every string, the async ones beside the sync ones. Each
/// element is reported as its code units in hex, and elements are separated by <c>|</c>. The reporting itself
/// reads code units by index, so it never iterates a string. Every expectation was read off Node 26.13.1, where all
/// eleven consumers agree on every row.
/// </para>
/// </summary>
public class StringIteratorCodePointTests
{
    private static readonly (string Name, string Source, string Expected)[] Subjects =
    [
        ("loneHigh", @"'\uD800'", "d800"),
        ("loneHighBetween", @"'a\uD800b'", "61|d800|62"),
        ("loneLowBetween", @"'a\uDC00b'", "61|dc00|62"),
        ("highThenNonLow", @"'\uD800a'", "d800|61"),
        ("reversedPair", @"'\uDC00\uD800'", "dc00|d800"),
        ("validPair", @"'x\u{1F600}y'", "78|d83d de00|79"),
        ("loneHighThenPair", @"'\uD800\u{1F600}'", "d800|d83d de00"),
        ("crlf", @"'a\r\nb'", "61|d|a|62"),
        ("combiningMark", @"'e\u0301'", "65|301"),
        ("zwjSequence", @"'\u{1F468}\u200D\u{1F469}'", "d83d dc68|200d|d83d dc69"),
    ];

    // Each body receives the string as `v` and returns the list of elements it produced.
    private static readonly Dictionary<string, string> SyncConsumers = new()
    {
        ["spread"] = "return [...v];",
        ["arrayFrom"] = "return Array.from(v);",
        ["forOf"] = "var r = []; for (var c of v) r.push(c); return r;",
        ["destructuring"] = "var [a, b, ...rest] = v; return [a, b].concat(rest).filter(function (x) { return x !== undefined; });",
        ["set"] = "return Array.from(new Set(v));",
        ["iteratorFrom"] = "return Iterator.from(v).toArray();",
        ["yieldStar"] = "function* g() { yield* v; } return Array.from(g());",
        ["next"] = "var it = v[Symbol.iterator](), r = [], s; while (!(s = it.next()).done) r.push(s.value); return r;",
    };

    private static readonly Dictionary<string, string> AsyncConsumers = new()
    {
        ["forAwait"] = "var r = []; for await (var c of v) r.push(c); return r;",
        ["asyncYieldStar"] = "async function* g() { yield* v; } var r = []; for await (var c of g()) r.push(c); return r;",
        ["fromAsync"] = "return await Array.fromAsync(v);",
    };

    private const string Encode = """
        function enc(list) {
            var out = [];
            for (var i = 0; i < list.length; i++) {
                var e = list[i], u = [];
                for (var j = 0; j < e.length; j++) u.push(e.charCodeAt(j).toString(16));
                out.push(u.join(' '));
            }
            return out.join('|');
        }
        """;

    public static TheoryData<string, string, string, string> SyncRows => Rows(SyncConsumers);

    public static TheoryData<string, string, string, string> AsyncRows => Rows(AsyncConsumers);

    private static TheoryData<string, string, string, string> Rows(Dictionary<string, string> consumers)
    {
        var rows = new TheoryData<string, string, string, string>();
        foreach (var consumer in consumers.Keys)
        {
            foreach (var (name, source, expected) in Subjects)
            {
                rows.Add(consumer, name, source, expected);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(SyncRows))]
    public void SyncConsumerStepsByCodePoint(string consumer, string subject, string source, string expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            {{Encode}}
            enc((function (v) { {{SyncConsumers[consumer]}} })({{source}}));
            """);

        result.AsString().Should().Be(expected, $"{consumer} over {subject}");
    }

    [Theory]
    [MemberData(nameof(AsyncRows))]
    public void AsyncConsumerStepsByCodePoint(string consumer, string subject, string source, string expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            {{Encode}}
            (async function (v) { {{AsyncConsumers[consumer]}} })({{source}}).then(enc);
            """).UnwrapIfPromise();

        result.AsString().Should().Be(expected, $"{consumer} over {subject}");
    }

    /// <summary>
    /// The repro of sebastienros/jint#4228, as its reporter wrote it: the first element of a CRLF is the CR alone,
    /// and a loop reading each element's first code unit sees the LF.
    /// </summary>
    [Fact]
    public void FirstStepOfACrlfIsTheCarriageReturnAlone()
    {
        var engine = new Engine();
        engine.Evaluate("[...'\\r\\n'].length").AsNumber().Should().Be(2);
        engine.Evaluate("'\\r\\n'[Symbol.iterator]().next().value.length").AsNumber().Should().Be(1);
        engine.Evaluate("const [first] = '\\r\\n'; first.length").AsNumber().Should().Be(1);
        engine.Evaluate("new Set('\\r\\n').size").AsNumber().Should().Be(2);
        engine.Evaluate("var units = []; for (const ch of 'a\\r\\nb') units.push(ch.charCodeAt(0)); units.join(',')")
            .AsString().Should().Be("97,13,10,98");
    }

    /// <summary>
    /// A finished iterator stays finished, and the step after the last element is <c>{ value: undefined, done: true }</c>
    /// (https://tc39.es/ecma262/#sec-createstringiterator step 1.b), whatever the last element was.
    /// </summary>
    [Theory]
    [InlineData(@"'\u{1F600}'")]
    [InlineData(@"'\uD800'")]
    [InlineData(@"'a\u0301'")]
    [InlineData("''")]
    public void ExhaustedIteratorStaysDone(string source)
    {
        var engine = new Engine();
        engine.Evaluate($$"""
            var it = {{source}}[Symbol.iterator]();
            while (!it.next().done) { }
            var a = it.next(), b = it.next();
            a.done === true && a.value === undefined && b.done === true && b.value === undefined;
            """).AsBoolean().Should().BeTrue();
    }
}
