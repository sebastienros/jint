#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// AssignmentRestElement and BindingRestElement build the rest array out of <em>the remainder of the
/// iterator</em> — "Repeat, while iteratorRecord.[[Done]] is false" — so a pattern whose leading
/// elements already exhausted the source contributes nothing and the rest array is empty.
/// <list type="bullet">
/// <item>https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization (BindingRestElement)</item>
/// <item>https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation (AssignmentRestElement)</item>
/// </list>
/// Jint short-circuits the iterator for an array source and copies the tail by index instead, and that
/// lane used to compute the tail length as an unsigned <c>length - i</c>. With more leading elements
/// than the source has, <c>i</c> is past <c>length</c> and the subtraction wrapped, so the rest array
/// was created with a length near 2^32 — reported by <c>.length</c>, and consumed: <c>JSON.stringify</c>
/// raised <c>RangeError: Invalid string length</c> and a spread raised a CLR
/// <c>OutOfMemoryException</c> straight out of <c>Evaluate</c>. Every expectation below is node v24's.
/// </summary>
public class DestructuringRestElementTests
{
    /// <summary>
    /// Reports the rest array by four independent routes, so a length that merely <em>says</em> zero
    /// cannot pass: the count, the serialization, how many elements a generic actually visits, and the
    /// own keys. Each probe is guarded on its own, so one failing still shows the other three.
    /// <para>
    /// The early return on an implausible length is what keeps a regression a <em>fast</em> failure:
    /// <c>Array.prototype.map</c> walks whatever the length claims, so on the wrapped length it takes
    /// some 37 seconds per row to arrive at zero callbacks. The count is the diagnostic either way.
    /// </para>
    /// </summary>
    private const string Describe = """
        function describe(v) {
            function safe(f) { try { return String(f()); } catch (e) { return 'THREW:' + ((e && e.name) || e); } }
            var len = safe(function () { return v.length; });
            if (!(v.length >= 0 && v.length <= 1000)) { return 'len=' + len + ' (implausible, probes skipped)'; }
            return 'len=' + len
                + ' json=' + safe(function () { return JSON.stringify(v); })
                + ' map=' + safe(function () { var n = 0; v.map(function () { n++; }); return n; })
                + ' keys=' + safe(function () { return JSON.stringify(Object.keys(v)); });
        }
        """;

    private const string Empty = "len=0 json=[] map=0 keys=[]";

    public static TheoryData<string, string> RestShapes() => new()
    {
        // The four shapes the issue filed, all of which reach the array lane.
        { "var a, b, r; [a, b, ...r] = [1]; describe(r)", Empty },
        { "var [a, b, ...r] = [1]; describe(r)", Empty },
        { "var [x, ...r] = []; describe(r)", Empty },
        { "var [, , ...r] = [1]; describe(r)", Empty },
        { "var o = {}; [o.a, o.b, ...o.rest] = [1]; describe(o.rest)", Empty },

        // Rest parameters were always correct; pinned so the two routes cannot drift apart.
        { "function f(p, q, ...r) { return r; } describe(f(1))", Empty },
        { "function f([p, q, ...r]) { return r; } describe(f([1]))", Empty },

        // An empty source with a single rest target, in all three target forms.
        { "var [...r] = []; describe(r)", Empty },
        { "var r; [...r] = []; describe(r)", Empty },
        { "var o = {}; [...o.r] = []; describe(o.r)", Empty },

        // An elision consumes a step exactly as a binding target does, so it advances the index too.
        { "var [, ...r] = [1]; describe(r)", Empty },
        { "var [, ...r] = []; describe(r)", Empty },
        { "var [, , ...r] = [1, 2, 3]; describe(r)", "len=1 json=[3] map=1 keys=[\"0\"]" },
        { "var [, , , ...r] = [1, 2]; describe(r)", Empty },
        { "var [, , , ...r] = [1, 2, 3, 4]; describe(r)", "len=1 json=[4] map=1 keys=[\"0\"]" },

        // Nested patterns containing a rest, including a rest whose own target is a pattern.
        { "var [[a, ...r]] = [[1]]; describe(r)", Empty },
        { "var [[a, b, ...r]] = [[1]]; describe(r)", Empty },
        { "var [q1, [q2, q3, ...r]] = [1, [2]]; describe(r)", Empty },
        { "var [...[z1, ...r]] = [1]; describe(r)", Empty },
        { "var [...[z1, z2, ...r]] = [1]; describe(r)", Empty },
        { "var o = {}; [o.a, ...[o.b, ...o.r]] = [1]; describe(o.r)", Empty },

        // A rest whose target is a member expression.
        { "var o = {}; [a, ...o.r] = []; describe(o.r)", Empty },
        { "var o = {}; [...o.r] = [1, 2]; describe(o.r)", "len=2 json=[1,2] map=2 keys=[\"0\",\"1\"]" },

        // A source shorter than, equal to, and longer than the pattern.
        { "var [s1, s2, ...r] = [1, 2]; describe(r)", Empty },
        { "var [t1, ...r] = [1, 2, 3]; describe(r)", "len=2 json=[2,3] map=2 keys=[\"0\",\"1\"]" },
        { "var [u1, u2, u3, ...r] = [1]; describe(r)", Empty },
        { "var [u1, u2, u3, u4, u5, ...r] = [1]; describe(r)", Empty },

        // A holey source. The array iterator answers a hole with a plain Get, so the rest array carries
        // an own undefined at that slot rather than a hole of its own — note keys and the map count.
        { "var [h, ...r] = [1, , 3]; describe(r)", "len=2 json=[null,3] map=2 keys=[\"0\",\"1\"]" },
        { "var [, ...r] = [1, , ]; describe(r)", "len=1 json=[null] map=1 keys=[\"0\"]" },
        { "var [a, ...r] = [1, , , 4]; describe(r)", "len=3 json=[null,null,4] map=3 keys=[\"0\",\"1\",\"2\"]" },
        { "var [a, b, c, d, ...r] = [1, , , 4]; describe(r)", Empty },

        // Sources that are iterable but not arrays take the general iterator lane, which was already
        // correct and is the oracle the array lane has to agree with.
        { "function* g() { yield 1; } var [k1, k2, ...r] = g(); describe(r)", Empty },
        { "function* g() { } var [...r] = g(); describe(r)", Empty },
        { "var [m1, m2, ...r] = new Set([1]); describe(r)", Empty },
        { "var [d1, d2, ...r] = new Map([[1, 2]]); describe(r)", Empty },
        { "var [p1, p2, ...r] = 'a'; describe(r)", Empty },
        { "var [w1, ...r] = 'abc'; describe(r)", "len=2 json=[\"b\",\"c\"] map=2 keys=[\"0\",\"1\"]" },
        { "var [y1, y2, ...r] = new Int8Array([1]); describe(r)", Empty },
        { "var [c1, c2, ...r] = (function () { return arguments; })(1); describe(r)", Empty },
        {
            "var al = { length: 1, 0: 'a' }; al[Symbol.iterator] = Array.prototype[Symbol.iterator];"
            + " var [v1, v2, ...r] = al; describe(r)",
            Empty
        },

        // The other statement positions that reach the same pattern handler.
        { "var out; for (var [fa, fb, ...r] of [[1]]) { out = r; } describe(out)", Empty },
        { "var out; for (const [fa, fb, ...r] of [[]]) { out = r; } describe(out)", Empty },
        { "var out; try { throw [1]; } catch ([ca, cb, ...r]) { out = r; } describe(out)", Empty },
        { "var f = ([aa, ab, ...r]) => r; describe(f([1]))", Empty },

        // Defaults ahead of the rest still consume a step each, taken or not.
        { "var [a = 9, b = 9, ...r] = [1]; describe(r)", Empty },
        { "var [a = 9, b = 9, ...r] = []; describe(r)", Empty },
    };

    [Theory]
    [MemberData(nameof(RestShapes))]
    public void TheRestOfAnExhaustedSourceIsEmpty(string script, string expected)
    {
        string outcome;
        try
        {
            outcome = new Engine().Evaluate(Describe + script).AsString();
        }
        catch (Exception ex)
        {
            outcome = ex.GetType().Name + ": " + ex.Message;
        }

        outcome.Should().Be(expected);
    }

    /// <summary>
    /// The length is not merely a number the array reports: a spread and <c>concat</c> both size a CLR
    /// buffer from it, and on the wrapped length they escaped as an <c>OutOfMemoryException</c> and an
    /// <c>IndexOutOfRangeException</c> that no script <c>catch</c> could see. Kept apart from the table
    /// above because neither failure can be caught in JavaScript and either would take the whole
    /// describe call down with it.
    /// <para>
    /// Each script guards on the length before it consumes, and that guard is load-bearing rather than
    /// decorative: growing a CLR buffer towards 2^32 takes about a minute and a half per row before it
    /// finally throws, and anything that walks the claimed length instead — <c>Array.from</c>,
    /// <c>for..of</c>, <c>forEach</c> — never finishes at all. With the guard a regression fails here
    /// immediately, naming the wrong length, and the consumption probe runs only on an array that has
    /// already claimed to be empty.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRestArrayIsGenuinelyEmptyAndNotMerelyReportingZero()
    {
        const string Guard = " if (r.length !== 0) { throw new Error('length=' + r.length); } ";

        var scripts = new[]
        {
            "var a, b, r; [a, b, ...r] = [1];" + Guard + "[...r].length",
            "var [a, b, ...r] = [1];" + Guard + "[...r].length",
            "var [x, ...r] = [];" + Guard + "[...r].length",
            "var [, , ...r] = [1];" + Guard + "[...r].length",
            "var o = {}; [o.a, o.b, ...o.rest] = [1]; var r = o.rest;" + Guard + "[...r].length",
            "var [x, ...r] = [];" + Guard + "r.concat([1]).length - 1",
            "var [x, ...r] = [];" + Guard + "Array.from(r).length",
            "var [x, ...r] = [];" + Guard + "var n = 0; for (var e of r) { n++; } n",
        };

        var expected = new List<string>();
        var actual = new List<string>();

        foreach (var script in scripts)
        {
            expected.Add(script + "  =>  0");

            string outcome;
            try
            {
                outcome = new Engine().Evaluate(script).ToString();
            }
            catch (Exception ex)
            {
                outcome = ex.GetType().Name + ": " + ex.Message;
            }

            actual.Add(script + "  =>  " + outcome);
        }

        string.Join("\n", actual).Should().Be(string.Join("\n", expected));
    }

    /// <summary>
    /// The exact shape from the issue: an ordinary "take the head off a possibly empty row" script.
    /// </summary>
    [Fact]
    public void StringifyingTheRestOfAnEmptyRowReturnsAnEmptyArray()
    {
        new Engine()
            .Evaluate("function f(row) { const [first, ...rest] = row; return JSON.stringify(rest); } f([])")
            .AsString().Should().Be("[]");
    }

    /// <summary>
    /// The array lane is entered by anything array-like carrying the original array iterator, which is
    /// a wrapped CLR collection as well as a JsArray. Same arithmetic, same wrap.
    /// </summary>
    [Fact]
    public void AWrappedClrCollectionTakesTheSameLaneAndIsEmptyToo()
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1 });

        engine.Evaluate("var [a, b, ...r] = list; r.length").AsNumber().Should().Be(0);
        engine.Evaluate("var [a, b, ...r] = list; JSON.stringify(r)").AsString().Should().Be("[]");
        engine.Evaluate("var [a, ...r] = list; JSON.stringify(r)").AsString().Should().Be("[]");
    }

    private sealed class HostList : ArrayLikeObject
    {
        private readonly List<JsValue> _items;

        public HostList(Engine engine, params JsValue[] items) : base(engine)
        {
            _items = new List<JsValue>(items);
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override uint Length => (uint) _items.Count;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Count)
            {
                value = _items[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }

    /// <summary>
    /// The third receiver the array lane accepts: a host <see cref="ArrayLikeObject"/>.
    /// </summary>
    [Fact]
    public void AHostArrayLikeObjectTakesTheSameLaneAndIsEmptyToo()
    {
        var engine = new Engine();
        engine.SetValue("host", new HostList(engine, 1));

        engine.Evaluate("var [a, b, ...r] = host; r.length").AsNumber().Should().Be(0);
        engine.Evaluate("var [a, b, ...r] = host; JSON.stringify(r)").AsString().Should().Be("[]");
        engine.Evaluate("var [a, ...r] = host; JSON.stringify(r)").AsString().Should().Be("[]");
        engine.Evaluate("var [...r] = host; JSON.stringify(r)").AsString().Should().Be("[1]");

        engine.SetValue("empty", new HostList(engine));
        engine.Evaluate("var [a, ...r] = empty; JSON.stringify(r)").AsString().Should().Be("[]");
    }
}
