using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class StringTests
{
    public StringTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    private readonly Engine _engine;

    [Fact]
    public void MixedTypeAdditionShouldEvaluateLeftToRight()
    {
        var engine = new Engine();
        // Numbers before a string literal must be added numerically first
        Assert.Equal("5m", engine.Evaluate("2.0 + 3.0 + 'm'").AsString());
        Assert.Equal("5m", engine.Evaluate("2 + 3 + 'm'").AsString());
        Assert.Equal("5mx", engine.Evaluate("2.0 + 3.0 + 'm' + 'x'").AsString());
        Assert.Equal("64", engine.Evaluate("1 + 2 + 3 + '4'").AsString());

        // String literal first: all ops are string concatenation
        Assert.Equal("m23", engine.Evaluate("'m' + 2 + 3").AsString());
        // String literal at index 1: correct too
        Assert.Equal("2m3", engine.Evaluate("2 + 'm' + 3").AsString());
    }

    [Fact]
    public void StringConcatenationAndReferences()
    {
        const string script = @"
var foo = 'foo';
foo += 'foo';
var bar = foo;
bar += 'bar';
";
        var value = _engine.Execute(script);
        var foo = _engine.Evaluate("foo").AsString();
        var bar = _engine.Evaluate("bar").AsString();
        Assert.Equal("foofoo", foo);
        Assert.Equal("foofoobar", bar);
    }

    [Fact]
    public void TrimLeftRightShouldBeSameAsTrimStartEnd()
    {
        _engine.Execute(@"
                assert(''.trimLeft === ''.trimStart);
                assert(''.trimRight === ''.trimEnd);
");
    }

    [Fact]
    public void HasProperIteratorPrototypeChain()
    {
        const string Script = @"
        // Iterator instance
        var iterator = ''[Symbol.iterator]();
        // %StringIteratorPrototype%
        var proto1 = Object.getPrototypeOf(iterator);
        // %IteratorPrototype%
        var proto2 = Object.getPrototypeOf(proto1);";

        var engine = new Engine();
        engine.Execute(Script);
        Assert.True(engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean());
    }

    [Fact]
    public void IndexOf()
    {
        var engine = new Engine();
        Assert.Equal(0, engine.Evaluate("''.indexOf('', 0)"));
        Assert.Equal(0, engine.Evaluate("''.indexOf('', 1)"));
    }

    [Fact]
    public void RepeatRejectsCountsThatCannotFitInClrStringCapacity()
    {
        var engine = new Engine();
        var repeatCount = ClrLimits.MaxArrayLength / 2 + 1UL;

        var exception = Assert.Throws<JavaScriptException>(
            () => engine.Evaluate($"'xx'.repeat({repeatCount});"));

        Assert.True(exception.Error.InstanceofOperator(engine.Intrinsics.RangeError));
    }

    [Fact]
    public void TemplateLiteralsWithArrays()
    {
        var engine = new Engine();
        engine.Execute("var a = [1,2,'three',true];");
        Assert.Equal("test 1,2,three,true", engine.Evaluate("'test ' + a"));
        Assert.Equal("test 1,2,three,true", engine.Evaluate("`test ${a}`"));
    }

    [Fact]
    public void TemplateLiteralAsObjectKey()
    {
        var engine=new Engine();
        var result = engine.Evaluate("({ [`key`]: 'value' })").AsObject();
        Assert.True(result.HasOwnProperty("key"));
        Assert.Equal("value", result["key"]);
    }

    [Fact]
    public void TaggedTemplateCachesTemplateObjectPerCallSite()
    {
        var engine = new Engine();

        // https://tc39.es/ecma262/#sec-gettemplateobject : the same call site must pass
        // the identical (frozen) strings array on every invocation; raw preserves escapes.
        var result = engine.Evaluate("""
            var seen = [];
            function tag(strings, v) { seen.push(strings); return strings[0] + v + strings[1]; }
            function run(v) { return tag`a ${v}\n`; }
            var r1 = run(1);
            var r2 = run(2);
            JSON.stringify({
                r1: r1,
                r2: r2,
                sameIdentity: seen[0] === seen[1],
                frozen: Object.isFrozen(seen[0]) && Object.isFrozen(seen[0].raw),
                cooked: seen[0][1] === '\n',
                raw: seen[0].raw[1] === '\\n',
                distinctSites: (function () { tag`x${0}`; tag`x${0}`; return seen[seen.length - 2] !== seen[seen.length - 1]; })()
            });
            """).AsString();

        Assert.Equal(
            """{"r1":"a 1\n","r2":"a 2\n","sameIdentity":true,"frozen":true,"cooked":true,"raw":true,"distinctSites":true}""",
            result);
    }

    [Fact]
    public void TaggedTemplateMemberTagPreservesThisBinding()
    {
        var engine = new Engine();

        // https://tc39.es/ecma262/#sec-evaluatecall : a member-expression tag is called with its receiver as `this`
        Assert.Equal("x 6 y", engine.Evaluate("""
            var obj = { mul: 3, tag: function (strings, v) { return strings[0] + (v * this.mul) + strings[1]; } };
            obj.tag`x ${2} y`;
            """).AsString());

        // computed member tag
        Assert.Equal("x 12 y", engine.Evaluate("obj['tag']`x ${4} y`;").AsString());

        // super property tag: GetThisValue returns the reference's [[ThisValue]] (the instance), not the home object
        Assert.Equal("b:q1", engine.Evaluate("""
            class A { tag(strings, v) { return this.name + ':' + strings[0] + v; } }
            class B extends A { constructor() { super(); this.name = 'b'; } m() { return super.tag`q${1}`; } }
            new B().m();
            """).AsString());

        // `with`-scoped tag: this is the with-statement binding object (WithBaseObject)
        Assert.True(engine.Evaluate("""
            var box = { tag: function (strings) { return this === box; } };
            var wr; with (box) { wr = tag`x`; }
            wr;
            """).AsBoolean());

        // plain identifier tag in sloppy mode: this is undefined, coerced to globalThis by the call
        Assert.True(engine.Evaluate("""
            function gt() { return this === globalThis; }
            gt`x`;
            """).AsBoolean());
    }

    [Fact]
    public void ShouldCompareWithLocale()
    {
        var engine = new Engine();
        Assert.Equal(1, engine.Evaluate("'王五'.localeCompare('张三')").AsInteger());
        Assert.Equal(-1, engine.Evaluate("'王五'.localeCompare('张三', 'zh-CN')").AsInteger());
    }

    [Fact]
    public void SlicedStringSearchMatchesFlatString()
    {
        // A large slice is represented internally as a zero-copy view (SlicedString) whose
        // indexOf/includes/startsWith/endsWith search the backing span directly. This differential
        // test compares those results against an identical *flat* string (built via a JSON round-trip,
        // which routes through the base ToString-backed search) for many needles and positions. The
        // sliced value is deliberately searched while still un-materialized, then again after being
        // forced to materialize, so both AsSpan() sources are exercised.
        const string script = @"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 4096) s += s;          // 4096 chars
var sub = s.slice(100, 4000);            // length 3900 -> SlicedString (zero-copy view)
var flat = JSON.parse(JSON.stringify(s.slice(100, 4000))); // identical content, plain string

if (sub.length !== flat.length) throw new Error('length mismatch');

var needles = ['aB3$x', 'Q9pLm', 'kEwZ', 'ZaB', '~none~', '', 'm0_', seed, seed + seed, 'Z'];
var positions = [-1, 0, 1, 50, 100, 1000, 3899, 3900, 3901, 5000];

function compareAll() {
    for (var n = 0; n < needles.length; n++) {
        var ndl = needles[n];
        if (sub.indexOf(ndl) !== flat.indexOf(ndl)) return 'indexOf:' + n;
        if (sub.includes(ndl) !== flat.includes(ndl)) return 'includes:' + n;
        if (sub.startsWith(ndl) !== flat.startsWith(ndl)) return 'startsWith:' + n;
        if (sub.endsWith(ndl) !== flat.endsWith(ndl)) return 'endsWith:' + n;
        for (var p = 0; p < positions.length; p++) {
            var pos = positions[p];
            if (sub.indexOf(ndl, pos) !== flat.indexOf(ndl, pos)) return 'indexOf@' + n + ',' + pos;
            if (sub.includes(ndl, pos) !== flat.includes(ndl, pos)) return 'includes@' + n + ',' + pos;
            if (sub.startsWith(ndl, pos) !== flat.startsWith(ndl, pos)) return 'startsWith@' + n + ',' + pos;
            if (sub.endsWith(ndl, pos) !== flat.endsWith(ndl, pos)) return 'endsWith@' + n + ',' + pos;
        }
    }
    return 'ok';
}

var first = compareAll();              // sub is still an un-materialized view here
var forced = sub + 'x';                // forces sub to materialize its backing substring
var second = compareAll();             // now searches the materialized span

// Absolute spot-checks (sub begins at s[100]; 100 % 16 == 4 -> 'x'), so a bug shared by both
// the sliced and flat search paths cannot make the differential pass silently.
var abs = sub.charAt(0) === 'x'
    && sub.startsWith('xQ9pLm0_kEwZ')
    && sub.indexOf('aB3$x') === 12
    && sub.indexOf('x', 1) === 16
    && sub.includes('kEwZaB3$')
    && sub.endsWith(flat.slice(-7));

first + '|' + second + '|' + (abs ? 'ok' : 'absfail');
";
        Assert.Equal("ok|ok|ok", _engine.Evaluate(script).AsString());
    }

    [Fact]
    public void StringReceiverMethodCallsInLoopReturnCorrectValues()
    {
        // Repeated str.method() calls on a primitive string receiver go through the per-node
        // prototype-method cache; the results must stay correct across iterations, and methods
        // found deeper on the chain (Object.prototype) must still resolve via the slow path.
        const string script = @"
var s = 'abcdef';
var last = '';
var sum = 0;
for (var i = 0; i < 10000; i++) {
    last = s.slice(1);
    sum += s.charCodeAt(i % 6);
}
var upper = s.toUpperCase();
var deep = s.hasOwnProperty('length');
last + '|' + sum + '|' + upper + '|' + deep;
";
        // 10000 = 1666 full cycles of 6 (sum 597 each) + 4 leftovers (97+98+99+100)
        var engine = new Engine();
        Assert.Equal("bcdef|" + (1666 * 597 + 394) + "|ABCDEF|true", engine.Evaluate(script).AsString());
    }

    [Fact]
    public void ReplacingStringPrototypeMethodMidLoopIsHonored()
    {
        // The call cache is guarded by holder identity + properties version and caches the live
        // descriptor: an in-place assignment (String.prototype.slice = fn), a defineProperty swap
        // and a delete must all take effect immediately on the next call.
        const string script = @"
var s = 'abcdef';
var nativeSlice = String.prototype.slice;
var seen = [];
for (var i = 0; i < 10; i++) {
    seen.push(s.slice(1, 2));
    if (i === 4) { String.prototype.slice = function () { return 'X'; }; }
}
var assignPhase = seen.join('');
String.prototype.slice = nativeSlice;

seen = [];
for (var i = 0; i < 10; i++) {
    seen.push(s.slice(2, 3));
    if (i === 4) { Object.defineProperty(String.prototype, 'slice', { value: function () { return 'Y'; }, writable: true, configurable: true }); }
}
var definePhase = seen.join('');

var deletePhase = 'no-error';
for (var i = 0; i < 10; i++) {
    if (i === 4) { delete String.prototype.slice; }
    try { s.slice(0, 1); } catch (e) { deletePhase = (e instanceof TypeError) ? 'TypeError' : 'other'; break; }
}
String.prototype.slice = nativeSlice;

// An accessor-backed method must resolve through its getter exactly once per call,
// with the primitive receiver as `this`.
var getterCalls = 0;
Object.defineProperty(String.prototype, 'accfn', {
    get: function () { getterCalls++; return function () { return 'g:' + this; }; },
    configurable: true
});
var accResult = '';
for (var i = 0; i < 5; i++) { accResult = s.accfn(); }
delete String.prototype.accfn;

assignPhase + '|' + definePhase + '|' + deletePhase + '|' + accResult + '|' + getterCalls;
";
        var engine = new Engine();
        Assert.Equal("bbbbbXXXXX|cccccYYYYY|TypeError|g:abcdef|5", engine.Evaluate(script).AsString());
    }

    [Fact]
    public void StringLengthAccessIsUnaffectedByCallFastPath()
    {
        // `length` is an own property of the boxed string and is excluded from the prototype-only
        // call cache at build time: reads keep working and `s.length()` keeps throwing TypeError.
        const string script = @"
var s = 'abc';
var len = 0;
for (var i = 0; i < 100; i++) { len = s.length; }
var error = 'none';
try { s.length(); } catch (e) { error = (e instanceof TypeError) ? 'TypeError' : 'other'; }
len + '|' + error;
";
        var engine = new Engine();
        Assert.Equal("3|TypeError", engine.Evaluate(script).AsString());
    }

    [Fact]
    public void PlantedStringPrototypeIndexOrLengthFunctionIsNotCalledOnStringReceiver()
    {
        // Index-coercible names ('0', '0x1', ...) resolve to OWN character properties on a string
        // receiver and shadow anything planted on String.prototype; the prototype-only call cache
        // must not engage for them, so the planted function is never invoked.
        const string script = @"
String.prototype['0'] = function () { return 'planted'; };
var s = 'abc';
var ownChar = s['0'];
var error = 'none';
try { s['0'](); } catch (e) { error = (e instanceof TypeError) ? 'TypeError' : 'other'; }
delete String.prototype['0'];
ownChar + '|' + error;
";
        var engine = new Engine();
        Assert.Equal("a|TypeError", engine.Evaluate(script).AsString());
    }

    [Fact]
    public void SliceOfSliceMatchesMaterializedExpectation()
    {
        // Build a ~128K backing string; slice/substring/substr produce a zero-copy view over it, and a
        // second slice of that (still un-materialized) view must rebase onto the original backing string
        // rather than materializing the intermediate. Each chained result is compared against an
        // identical *flat* string produced via a JSON round-trip (a separate, fully materialized path).
        const string script = @"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 131072) s += s;         // 131072 chars

// slice-of-slice (view then slice), substring-of-slice, substr-of-slice
var v1 = s.slice(0, 100000);              // large -> view
var a = v1.slice(1000, 60000);            // rebased slice-of-slice
var b = v1.substring(2000, 50000);        // rebased substring-of-slice
var c = v1.substr(3000, 40000);           // rebased substr-of-slice

// materialized expectations from the original backing string
var ea = JSON.parse(JSON.stringify(s.slice(1000, 60000)));
var eb = JSON.parse(JSON.stringify(s.substring(2000, 50000)));
var ec = JSON.parse(JSON.stringify(s.substr(3000, 40000)));

var ok =
    a.length === ea.length && a === ea &&
    b.length === eb.length && b === eb &&
    c.length === ec.length && c === ec &&
    // boundary spot checks so a shared bug can't pass silently
    a.charAt(0) === s.charAt(1000) &&
    a.charAt(a.length - 1) === s.charAt(59999) &&
    b.charAt(0) === s.charAt(2000);

// slice of an ALREADY-materialized view must also stay correct (treated as flat)
var v2 = s.slice(0, 100000);
var forced = v2 + '';                      // materialize v2's backing substring
var d = v2.slice(500, 70000);
var ed = JSON.parse(JSON.stringify(s.slice(500, 70000)));

ok && d.length === ed.length && d === ed ? 'ok' : 'fail';
";
        Assert.Equal("ok", _engine.Evaluate(script).AsString());
    }

    [Fact]
    public void SliceOfSliceStaysZeroCopyForLargeResult()
    {
        // A moderate slice of a large view whose unused remainder stays within the retention budget is
        // kept as a zero-copy view (SlicedString), rebased onto the original backing string.
        _engine.Execute(@"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 131072) s += s;
var v1 = s.slice(0, 100000);");

        Assert.Contains("Sliced", _engine.Evaluate("v1").GetType().Name);
        Assert.Contains("Sliced", _engine.Evaluate("v1.slice(1000, 60000)").GetType().Name);
    }

    [Fact]
    public void ChainedSliceOfViewCopiesAgainstOriginalSource()
    {
        // Evaluating the retention policy against the ORIGINAL backing string (not the intermediate
        // view) prevents a chained view from pinning a much larger source: a 200K slice of a 256K view
        // over a 512K backing string copies, because the ~312K unused remainder of the backing string
        // exceeds the retention budget. Value stays correct regardless.
        _engine.Execute(@"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 524288) s += s;         // 512K
var v1 = s.slice(0, 262144);              // half -> view
var small = v1.slice(0, 200000);          // rebased; copies against the 512K source");

        // v1 is a view, but the chained slice is copied (flat JsString), not a compounded view.
        Assert.Contains("Sliced", _engine.Evaluate("v1").GetType().Name);
        Assert.DoesNotContain("Sliced", _engine.Evaluate("small").GetType().Name);

        // and the value is still exactly s[0..200000]
        Assert.Equal("ok", _engine.Evaluate(
            "small.length === 200000 && small === JSON.parse(JSON.stringify(s.slice(0, 200000))) ? 'ok' : 'fail'").AsString());
    }

    [Fact]
    public void SplitEmptySeparatorAscii()
    {
        Assert.Equal(@"[""h"",""e"",""l"",""l"",""o""]", _engine.Evaluate(@"JSON.stringify('hello'.split(''))").AsString());
        Assert.Equal(5, _engine.Evaluate("'hello'.split('').length").AsNumber());
        // limit truncates
        Assert.Equal(@"[""h"",""e"",""l""]", _engine.Evaluate(@"JSON.stringify('hello'.split('', 3))").AsString());
        // empty string splits to an empty array
        Assert.Equal(0, _engine.Evaluate("''.split('').length").AsNumber());
        // cached single-char instances still compare equal by value
        Assert.True(_engine.Evaluate("'aba'.split('')[0] === 'a' && 'aba'.split('')[2] === 'a'").AsBoolean());
    }

    [Fact]
    public void SplitEmptySeparatorNonAscii()
    {
        // BMP chars above the ASCII cache (é = U+00E9, Greek U+03B1..) must round-trip correctly.
        Assert.Equal(@"[""c"",""a"",""f"",""é""]", _engine.Evaluate(@"JSON.stringify('café'.split(''))").AsString());
        Assert.Equal(@"[""α"",""β"",""γ""]", _engine.Evaluate(@"JSON.stringify('αβγ'.split(''))").AsString());
        Assert.Equal(3, _engine.Evaluate("'αβγ'.split('').length").AsNumber());

        // split('') splits by UTF-16 code unit: an astral char (surrogate pair) becomes two elements.
        Assert.Equal(2, _engine.Evaluate("'😀'.split('').length").AsNumber());
        Assert.True(_engine.Evaluate(
            "var p = '😀'.split(''); p[0] === '\uD83D' && p[1] === '\uDE00'").AsBoolean());
    }

    [Fact]
    public void SplitTailAndSegmentsAreCorrect()
    {
        // Small segments and tail
        Assert.Equal(@"[""a"",""b"",""cdefghij""]", _engine.Evaluate(@"JSON.stringify('a,b,cdefghij'.split(','))").AsString());
        // consecutive separators produce empty segments
        Assert.Equal(@"[""a"","""",""b""]", _engine.Evaluate(@"JSON.stringify('a,,b'.split(','))").AsString());
        // multi-char separator
        Assert.Equal(@"[""a"",""b"",""c""]", _engine.Evaluate(@"JSON.stringify('a<>b<>c'.split('<>'))").AsString());
        // trailing separator yields a trailing empty segment
        Assert.Equal(@"[""a"",""b"",""""]", _engine.Evaluate(@"JSON.stringify('a,b,'.split(','))").AsString());

        // Large tail segment: the final piece of a large string, routed through the retention policy,
        // must still equal the exact backing substring.
        const string script = @"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 131072) s += s;
var parts = ('HEADER|' + s).split('|');   // ['HEADER', s]
var tail = parts[1];
parts.length === 2 &&
    parts[0] === 'HEADER' &&
    tail.length === s.length &&
    tail === JSON.parse(JSON.stringify(s)) ? 'ok' : 'fail';
";
        Assert.Equal("ok", _engine.Evaluate(script).AsString());
    }

    [Fact]
    public void SplitPolicyCopiesSmallSegmentsAndViewsLargeOnes()
    {
        _engine.Execute(@"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 131072) s += s;
var small = 'a,b,c'.split(',');
var big = (s + '|tail').split('|');       // [s, 'tail']: first segment is ~the whole source -> view");

        // small segments are copied (not views)
        Assert.DoesNotContain("Sliced", _engine.Evaluate("small[0]").GetType().Name);
        // a large-enough segment of a large string is kept as a zero-copy view
        Assert.Contains("Sliced", _engine.Evaluate("big[0]").GetType().Name);
        Assert.True(_engine.Evaluate("big.length === 2 && big[0] === s && big[1] === 'tail'").AsBoolean());
    }

    public static TheoryData<string, string> GetLithuaniaTestsData()
    {
        return new StringTetsLithuaniaData().TestData();
    }

    /// <summary>
    /// Lithuanian case is special and Test262 suite tests cover only correct parsing by character. See:
    /// https://github.com/tc39/test262/blob/main/test/intl402/String/prototype/toLocaleUpperCase/special_casing_Lithuanian.js
    /// Added logic in the engine needs to parse full strings and not only spare characters. This is what these tests cover.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetLithuaniaTestsData))]
    public void LithuanianToLocaleUpperCase(string parseStr, string result)
    {
        var value = _engine.Evaluate($"('{parseStr}').toLocaleUpperCase('lt')").AsString();
        Assert.Equal(result, value);
    }
}
