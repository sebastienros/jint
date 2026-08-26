using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class StringTests
{
    public StringTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    private readonly Engine _engine;

    [Test]
    public void MixedTypeAdditionShouldEvaluateLeftToRight()
    {
        var engine = new Engine();
        // Numbers before a string literal must be added numerically first
        engine.Evaluate("2.0 + 3.0 + 'm'").AsString().Should().Be("5m");
        engine.Evaluate("2 + 3 + 'm'").AsString().Should().Be("5m");
        engine.Evaluate("2.0 + 3.0 + 'm' + 'x'").AsString().Should().Be("5mx");
        engine.Evaluate("1 + 2 + 3 + '4'").AsString().Should().Be("64");

        // String literal first: all ops are string concatenation
        engine.Evaluate("'m' + 2 + 3").AsString().Should().Be("m23");
        // String literal at index 1: correct too
        engine.Evaluate("2 + 'm' + 3").AsString().Should().Be("2m3");
    }

    [Test]
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
        foo.Should().Be("foofoo");
        bar.Should().Be("foofoobar");
    }

    [Test]
    public void TrimLeftRightShouldBeSameAsTrimStartEnd()
    {
        _engine.Execute(@"
                assert(''.trimLeft === ''.trimStart);
                assert(''.trimRight === ''.trimEnd);
");
    }

    [Test]
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
        engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean().Should().BeTrue();
        engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void IndexOf()
    {
        var engine = new Engine();
        engine.Evaluate("''.indexOf('', 0)").Should().Be(0);
        engine.Evaluate("''.indexOf('', 1)").Should().Be(0);
    }

    [Test]
    public void RepeatRejectsCountsThatExceedTheMaximumStringLength()
    {
        var engine = new Engine();
        var repeatCount = JsString.MaxLength / 2 + 1;

        var exception = Invoking(() => engine.Evaluate($"'xx'.repeat({repeatCount});")).Should().ThrowExactly<JavaScriptException>().Which;

        exception.Error.InstanceofOperator(engine.Intrinsics.RangeError).Should().BeTrue();
    }

    [Test]
    public void TemplateLiteralsWithArrays()
    {
        var engine = new Engine();
        engine.Execute("var a = [1,2,'three',true];");
        engine.Evaluate("'test ' + a").Should().Be("test 1,2,three,true");
        engine.Evaluate("`test ${a}`").Should().Be("test 1,2,three,true");
    }

    [Test]
    public void TemplateLiteralAsObjectKey()
    {
        var engine=new Engine();
        var result = engine.Evaluate("({ [`key`]: 'value' })").AsObject();
        result.HasOwnProperty("key").Should().BeTrue();
        result["key"].Should().Be("value");
    }

    [Test]
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

        result.Should().Be("""{"r1":"a 1\n","r2":"a 2\n","sameIdentity":true,"frozen":true,"cooked":true,"raw":true,"distinctSites":true}""");
    }

    [Test]
    public void TaggedTemplateMemberTagPreservesThisBinding()
    {
        var engine = new Engine();

        // https://tc39.es/ecma262/#sec-evaluatecall : a member-expression tag is called with its receiver as `this`
        engine.Evaluate("""
            var obj = { mul: 3, tag: function (strings, v) { return strings[0] + (v * this.mul) + strings[1]; } };
            obj.tag`x ${2} y`;
            """).AsString().Should().Be("x 6 y");

        // computed member tag
        engine.Evaluate("obj['tag']`x ${4} y`;").AsString().Should().Be("x 12 y");

        // super property tag: GetThisValue returns the reference's [[ThisValue]] (the instance), not the home object
        engine.Evaluate("""
            class A { tag(strings, v) { return this.name + ':' + strings[0] + v; } }
            class B extends A { constructor() { super(); this.name = 'b'; } m() { return super.tag`q${1}`; } }
            new B().m();
            """).AsString().Should().Be("b:q1");

        // `with`-scoped tag: this is the with-statement binding object (WithBaseObject)
        engine.Evaluate("""
            var box = { tag: function (strings) { return this === box; } };
            var wr; with (box) { wr = tag`x`; }
            wr;
            """).AsBoolean().Should().BeTrue();

        // plain identifier tag in sloppy mode: this is undefined, coerced to globalThis by the call
        engine.Evaluate("""
            function gt() { return this === globalThis; }
            gt`x`;
            """).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ShouldCompareWithLocale()
    {
        var engine = new Engine();
        engine.Evaluate("'王五'.localeCompare('张三')").AsInteger().Should().Be(1);
        engine.Evaluate("'王五'.localeCompare('张三', 'zh-CN')").AsInteger().Should().Be(-1);
    }

    [Test]
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
        _engine.Evaluate(script).AsString().Should().Be("ok|ok|ok");
    }

    [Test]
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
        engine.Evaluate(script).AsString().Should().Be("bcdef|" + (1666 * 597 + 394) + "|ABCDEF|true");
    }

    [Test]
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
        engine.Evaluate(script).AsString().Should().Be("bbbbbXXXXX|cccccYYYYY|TypeError|g:abcdef|5");
    }

    [Test]
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
        engine.Evaluate(script).AsString().Should().Be("3|TypeError");
    }

    [Test]
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
        engine.Evaluate(script).AsString().Should().Be("a|TypeError");
    }

    [Test]
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
        _engine.Evaluate(script).AsString().Should().Be("ok");
    }

    [Test]
    public void SliceOfSliceStaysZeroCopyForLargeResult()
    {
        // A moderate slice of a large view whose unused remainder stays within the retention budget is
        // kept as a zero-copy view (SlicedString), rebased onto the original backing string.
        _engine.Execute(@"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 131072) s += s;
var v1 = s.slice(0, 100000);");

        _engine.Evaluate("v1").GetType().Name.Should().Contain("Sliced");
        _engine.Evaluate("v1.slice(1000, 60000)").GetType().Name.Should().Contain("Sliced");
    }

    [Test]
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
        _engine.Evaluate("v1").GetType().Name.Should().Contain("Sliced");
        _engine.Evaluate("small").GetType().Name.Should().NotContain("Sliced");

        // and the value is still exactly s[0..200000]
        _engine.Evaluate(
            "small.length === 200000 && small === JSON.parse(JSON.stringify(s.slice(0, 200000))) ? 'ok' : 'fail'").AsString().Should().Be("ok");
    }

    [Test]
    public void SplitEmptySeparatorAscii()
    {
        _engine.Evaluate(@"JSON.stringify('hello'.split(''))").AsString().Should().Be(@"[""h"",""e"",""l"",""l"",""o""]");
        _engine.Evaluate("'hello'.split('').length").AsNumber().Should().Be(5);
        // limit truncates
        _engine.Evaluate(@"JSON.stringify('hello'.split('', 3))").AsString().Should().Be(@"[""h"",""e"",""l""]");
        // empty string splits to an empty array
        _engine.Evaluate("''.split('').length").AsNumber().Should().Be(0);
        // cached single-char instances still compare equal by value
        _engine.Evaluate("'aba'.split('')[0] === 'a' && 'aba'.split('')[2] === 'a'").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void SplitEmptySeparatorNonAscii()
    {
        // BMP chars above the ASCII cache (é = U+00E9, Greek U+03B1..) must round-trip correctly.
        _engine.Evaluate(@"JSON.stringify('café'.split(''))").AsString().Should().Be(@"[""c"",""a"",""f"",""é""]");
        _engine.Evaluate(@"JSON.stringify('αβγ'.split(''))").AsString().Should().Be(@"[""α"",""β"",""γ""]");
        _engine.Evaluate("'αβγ'.split('').length").AsNumber().Should().Be(3);

        // split('') splits by UTF-16 code unit: an astral char (surrogate pair) becomes two elements.
        _engine.Evaluate("'😀'.split('').length").AsNumber().Should().Be(2);
        _engine.Evaluate(
            "var p = '😀'.split(''); p[0] === '\uD83D' && p[1] === '\uDE00'").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void SplitTailAndSegmentsAreCorrect()
    {
        // Small segments and tail
        _engine.Evaluate(@"JSON.stringify('a,b,cdefghij'.split(','))").AsString().Should().Be(@"[""a"",""b"",""cdefghij""]");
        // consecutive separators produce empty segments
        _engine.Evaluate(@"JSON.stringify('a,,b'.split(','))").AsString().Should().Be(@"[""a"","""",""b""]");
        // multi-char separator
        _engine.Evaluate(@"JSON.stringify('a<>b<>c'.split('<>'))").AsString().Should().Be(@"[""a"",""b"",""c""]");
        // trailing separator yields a trailing empty segment
        _engine.Evaluate(@"JSON.stringify('a,b,'.split(','))").AsString().Should().Be(@"[""a"",""b"",""""]");

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
        _engine.Evaluate(script).AsString().Should().Be("ok");
    }

    [Test]
    public void SplitPolicyCopiesSmallSegmentsAndViewsLargeOnes()
    {
        _engine.Execute(@"
var seed = 'aB3$xQ9pLm0_kEwZ';
var s = seed;
while (s.length < 131072) s += s;
var small = 'a,b,c'.split(',');
var big = (s + '|tail').split('|');       // [s, 'tail']: first segment is ~the whole source -> view");

        // small segments are copied (not views)
        _engine.Evaluate("small[0]").GetType().Name.Should().NotContain("Sliced");
        // a large-enough segment of a large string is kept as a zero-copy view
        _engine.Evaluate("big[0]").GetType().Name.Should().Contain("Sliced");
        _engine.Evaluate("big.length === 2 && big[0] === s && big[1] === 'tail'").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The special-casing pre-scans decide whether a conversion can delegate to the plain
    /// framework ToUpper/ToLower or must walk the string character by character. The upper scan
    /// rejects a string outright when every character sits below U+00DF, since no SpecialCasing.txt
    /// expansion exists below that point — so U+00DF itself is the boundary that must NOT be
    /// rejected, and these cases pin both sides of it.
    /// </summary>
    // below the boundary: plain framework casing
    [TestCase("abc", "ABC", "abc")]
    [TestCase("Þ", "Þ", "þ")]                      // U+00DE, last character with no expansion
    // at and above the boundary: must reach the per-character expansion path
    [TestCase("ß", "SS", "ß")]                     // U+00DF LATIN SMALL LETTER SHARP S
    [TestCase("aßb", "ASSB", "aßb")]               // expansion in the middle of ASCII
    [TestCase("ﬄ", "FFL", "ﬄ")]                    // U+FB04 LATIN SMALL LIGATURE FFL
    [TestCase("İ", "İ", "i̇")]                // U+0130, lower-cases to an expansion
    [TestCase("ΑΣ", "ΑΣ", "ας")]                   // Final_Sigma: sigma at end lower-cases to final form
    [TestCase("ΑΣΒ", "ΑΣΒ", "ασβ")]                // non-final sigma keeps the medial form
    public void CasingHandlesTheSpecialCasingBoundary(string input, string upper, string lower)
    {
        _engine.Evaluate($"('{input}').toUpperCase()").AsString().Should().Be(upper);
        _engine.Evaluate($"('{input}').toLowerCase()").AsString().Should().Be(lower);
    }

    /// <summary>
    /// toUpperCase and toLowerCase are defined over the Unicode Default Case Conversion algorithm
    /// (https://tc39.es/ecma262/#sec-string.prototype.tolowercase), which the framework's
    /// ToUpperInvariant/ToLowerInvariant do not implement: they keep U+0131 unchanged by design,
    /// supply only the simple mappings, and carry whichever Unicode version the host's ICU — or
    /// Windows NLS, on .NET Framework — happens to ship, so a script added in a recent version
    /// maps differently from machine to machine. Both sides are evaluated so the supplementary
    /// expectations stay readable; String.fromCodePoint is independent of case conversion.
    /// </summary>
    // U+0131 LATIN SMALL LETTER DOTLESS I. ToUpperInvariant deliberately maps it to itself.
    [TestCase("'\\u0131'.toUpperCase()", "'I'")]
    // The locale entry points must agree with the language-insensitive ones when no locale tailors casing.
    [TestCase("'\\u0131'.toLocaleUpperCase()", "'I'")]
    [TestCase("'\\u0131'.toLocaleUpperCase('en')", "'I'")]
    // GARAY, added in Unicode 16.0 — newer than the ICU most hosts carry.
    [TestCase("String.fromCodePoint(0x10D70).toUpperCase()", "String.fromCodePoint(0x10D50)")]
    [TestCase("String.fromCodePoint(0x10D50).toLowerCase()", "String.fromCodePoint(0x10D70)")]
    // VITHKUQI, added in Unicode 14.0.
    [TestCase("String.fromCodePoint(0x10597).toUpperCase()", "String.fromCodePoint(0x10570)")]
    // A supplementary code point in a string that also needs a SpecialCasing expansion: the
    // expansion path used to pass every surrogate pair through unmapped.
    [TestCase("('\\u00DF' + String.fromCodePoint(0x10428)).toUpperCase()", "'SS' + String.fromCodePoint(0x10400)")]
    [TestCase("('\\u0130' + String.fromCodePoint(0x10400)).toLowerCase()", "'i\\u0307' + String.fromCodePoint(0x10428)")]
    public void CaseConversionUsesTheUnicodeDefaultCaseConversionAlgorithm(string actual, string expected)
    {
        var actualValue = _engine.Evaluate(actual).AsString();
        var expectedValue = _engine.Evaluate(expected).AsString();
        actualValue.Should().Be(expectedValue);
    }

    /// <summary>
    /// Turkish, Azeri and Lithuanian are the languages the Unicode Character Database tailors case
    /// mapping for, and ECMA-402's toLocale{Upper,Lower}Case must keep honouring them even though
    /// every other locale now goes through the language-insensitive tables.
    /// https://tc39.es/ecma402/#sup-string.prototype.tolocaleuppercase
    /// </summary>
    [TestCase("'i'.toLocaleUpperCase('tr')", "İ")]
    [TestCase("'i'.toLocaleUpperCase('tr-TR')", "İ")]
    [TestCase("'i'.toLocaleUpperCase('az')", "İ")]
    [TestCase("'I'.toLocaleLowerCase('tr')", "ı")]
    [TestCase("'I'.toLocaleLowerCase('az')", "ı")]
    [TestCase("'İ'.toLocaleLowerCase('tr')", "i")]
    [TestCase("'İ'.toLocaleLowerCase('und')", "i̇")]
    public void LocaleTailoredCasingIsPreserved(string expression, string expected)
    {
        _engine.Evaluate(expression).AsString().Should().Be(expected);
    }

    /// <summary>
    /// lastIndexOf's position argument bounds where a match may <em>start</em>, not where the search
    /// begins, which is easy to get off by one. Every expectation here was verified against V8.
    /// </summary>
    // no position: whole string
    [TestCase("'abcabc'.lastIndexOf('abc')", 3)]
    [TestCase("'abcabc'.lastIndexOf('abcabc')", 0)]
    [TestCase("'abcabc'.lastIndexOf('x')", -1)]
    [TestCase("'abc'.lastIndexOf('abcd')", -1)]
    [TestCase("'aaa'.lastIndexOf('aa')", 1)]
    // position clamps the start of the match, so a match may extend past it
    [TestCase("'abcabc'.lastIndexOf('abc', 2)", 0)]
    [TestCase("'abcabc'.lastIndexOf('abc', 3)", 3)]
    [TestCase("'abcabc'.lastIndexOf('bc', 1)", 1)]
    [TestCase("'abcabc'.lastIndexOf('bc', 0)", -1)]
    [TestCase("'abcabc'.lastIndexOf('c', 4)", 2)]
    [TestCase("'hello world hello'.lastIndexOf('hello', 11)", 0)]
    [TestCase("'hello world hello'.lastIndexOf('hello', 12)", 12)]
    // out-of-range, fractional and negative-zero positions
    [TestCase("'abcabc'.lastIndexOf('abc', 0)", 0)]
    [TestCase("'abcabc'.lastIndexOf('abc', -1)", 0)]
    [TestCase("'abcabc'.lastIndexOf('abc', -100)", 0)]
    [TestCase("'abcabc'.lastIndexOf('abc', -0)", 0)]
    [TestCase("'abcabc'.lastIndexOf('abc', 1.9)", 0)]
    [TestCase("'abcabc'.lastIndexOf('abc', 100)", 3)]
    [TestCase("'abcabc'.lastIndexOf('abc', NaN)", 3)]
    // the empty needle matches at the clamped position
    [TestCase("'abcabc'.lastIndexOf('')", 6)]
    [TestCase("'abcabc'.lastIndexOf('', 3)", 3)]
    [TestCase("'abcabc'.lastIndexOf('', 100)", 6)]
    [TestCase("'abcabc'.lastIndexOf('', -5)", 0)]
    [TestCase("''.lastIndexOf('')", 0)]
    [TestCase("''.lastIndexOf('a')", -1)]
    public void LastIndexOfMatchesSpecPositionSemantics(string expression, int expected)
    {
        _engine.Evaluate(expression).AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// isWellFormed/toWellFormed short-circuit on a vectorized "does this string contain any surrogate
    /// code unit at all?" scan, and toWellFormed additionally hands back the receiver's own string when
    /// nothing needs replacing. These cases pin the surrogate range boundary and every unpaired shape;
    /// all expectations were verified against V8.
    /// </summary>
    // no surrogates at all — the vectorized reject path
    [TestCase("''", true, "")]
    [TestCase("'abc'", true, "abc")]
    // just outside the surrogate range on both sides, so still well-formed
    [TestCase("'\\uD7FF\\uE000'", true, "퟿")]
    // valid pairs
    [TestCase("'a\\uD83D\\uDE00b'", true, "a😀b")]
    // unpaired leading surrogate
    [TestCase("'a\\uD800'", false, "a�")]
    [TestCase("'ab\\uD83D'", false, "ab�")]
    [TestCase("'\\uD800\\uD800'", false, "��")]
    // unpaired trailing surrogate
    [TestCase("'a\\uDC00b'", false, "a�b")]
    [TestCase("'x\\uDFFFy'", false, "x�y")]
    // reversed pair: both halves are unpaired
    [TestCase("'\\uDC00\\uD800'", false, "��")]
    [TestCase("'\\uD800a\\uDC00'", false, "�a�")]
    // an unpaired lead immediately followed by a valid pair
    [TestCase("'\\uD83D\\uD83D\\uDE00'", false, "�😀")]
    public void WellFormedHandlesSurrogateBoundaries(string literal, bool wellFormed, string toWellFormed)
    {
        _engine.Evaluate($"({literal}).isWellFormed()").AsBoolean().Should().Be(wellFormed);
        _engine.Evaluate($"({literal}).toWellFormed()").AsString().Should().Be(toWellFormed);
    }

    [Test]
    public void ToWellFormedReturnsAnEquivalentPrimitiveForObjectReceivers()
    {
        // toWellFormed must produce a primitive string even when `this` is a String object, including
        // on the already-well-formed path where the receiver's own value is reused.
        _engine.Evaluate("typeof new String('abc').toWellFormed()").AsString().Should().Be("string");
        _engine.Evaluate("new String('abc').toWellFormed()").AsString().Should().Be("abc");
        _engine.Evaluate("new String('a\\uD800b').toWellFormed()").AsString().Should().Be("a�b");

        // an already-well-formed primitive maps to the same sequence of code units
        _engine.Evaluate("var s = 'abcde'.repeat(1000); s.toWellFormed() === s").AsBoolean().Should().BeTrue();
    }

    // ---- coercion order and receiver coercion ----

    [Test]
    public void SliceCoercesTheReceiverBeforeItsArguments()
    {
        // https://tc39.es/ecma262/#sec-string.prototype.slice - step 2 (ToString of the receiver)
        // precedes ToIntegerOrInfinity of start (step 4) and of end (step 8).
        var order = _engine.Evaluate("""
            var log = [];
            var o = { toString: function () { log.push('this'); return 'abcdef'; } };
            var start = { valueOf: function () { log.push('start'); return 1; } };
            var end = { valueOf: function () { log.push('end'); return 3; } };
            String.prototype.slice.call(o, start, end) + '|' + log.join(',')
            """).AsString();

        order.Should().Be("bc|this,start,end");
    }

    [Test]
    public void SliceThrowsForASymbolReceiverWhateverTheStartIs()
    {
        Invoking(() => _engine.Evaluate("String.prototype.slice.call(Symbol(), Infinity)"))
            .Should().Throw<JavaScriptException>().WithMessage("Cannot convert a Symbol value to a string");
    }

    [Test]
    public void SliceCoercesEndEvenWhenStartIsInfinite()
    {
        // An infinite start clamps to the end of the string, but step 8 still coerces end.
        var coerced = _engine.Evaluate("""
            var seen = false;
            var end = { valueOf: function () { seen = true; return 3; } };
            'abcdef'.slice(Infinity, end) + '|' + seen
            """).AsString();

        coerced.Should().Be("|true");
    }

    [Test]
    public void AtCoercesTheReceiverPerSpec()
    {
        // https://tc39.es/ecma262/#sec-string.prototype.at - step 2 is ToString(O), which rejects a Symbol
        // and honours a user-defined toString.
        Invoking(() => _engine.Evaluate("String.prototype.at.call(Symbol('x'), 0)"))
            .Should().Throw<JavaScriptException>().WithMessage("Cannot convert a Symbol value to a string");

        _engine.Evaluate("String.prototype.at.call({ toString: function () { return 'zq'; } }, 1)")
            .AsString().Should().Be("q");
    }

    [Test]
    public void IncludesHandlesAnOutOfRangeFromIndex()
    {
        // https://tc39.es/ecma262/#sec-string.prototype.includes step 8 clamps pos between 0 and the
        // length of S, so a position no int can hold must still answer rather than fault.
        _engine.Evaluate("'abc'.includes('b', 1e20)").AsBoolean().Should().BeFalse();
        _engine.Evaluate("'abc'.includes('', 1e20)").AsBoolean().Should().BeTrue();
        _engine.Evaluate("'abc'.includes('b', -1e20)").AsBoolean().Should().BeTrue();
    }

    // ---- split's empty-regexp shortcut ----

    [TestCase("u")]
    [TestCase("v")]
    public void SplitOnAnEmptyUnicodeRegExpKeepsSurrogatePairsTogether(string flags)
    {
        // RegExp.prototype[@@split] advances by code point under u/v, so the shortcut that rewrites
        // /(?:)/ into the empty string separator must not take unicode-mode regexps.
        var result = _engine.Evaluate($"var a = '\\u{{1F600}}'.split(/(?:)/{flags}); a.length + ':' + a[0]").AsString();

        result.Should().Be("1:\uD83D\uDE00");
    }

    [Test]
    public void SplitOnAnEmptyRegExpHonoursAUserSuppliedSplitSymbol()
    {
        // https://tc39.es/ecma262/#sec-string.prototype.split step 2: an own @@split wins over the
        // built-in behaviour, empty pattern or not.
        var result = _engine.Evaluate("""
            var re = /(?:)/;
            re[Symbol.split] = function () { return 'custom'; };
            'abc'.split(re)
            """).AsString();

        result.Should().Be("custom");
    }

    [Test]
    public void SplitOnAnEmptyNonUnicodeRegExpStillYieldsCodeUnits()
    {
        _engine.Evaluate("""var a = '\u{1F600}'.split(/(?:)/); a.length + ':' + a[0].charCodeAt(0)""")
            .AsString().Should().Be("2:55357");
        _engine.Evaluate("""JSON.stringify('abc'.split(/(?:)/))""").AsString().Should().Be("""["a","b","c"]""");
        _engine.Evaluate("""JSON.stringify('abc'.split(/(?:)/, 2))""").AsString().Should().Be("""["a","b"]""");
    }

    [Test]
    public void SubstitutionTailIsEmptyWhenTheMatchRunsPastTheEndOfTheString()
    {
        // GetSubstitution step 5.e (https://tc39.es/ecma262/#sec-getsubstitution): tailPos is
        // position + the length of matched, and $' is the substring of str from
        // min(tailPos, stringLength) -- so a tailPos at or past the end yields the empty string.
        // tailPos can only exceed stringLength when @@replace ran against an object whose "exec"
        // is not the intrinsic one, which is exactly what these lying execs are.
        _engine.Execute("var evil = new RegExp();");

        _engine.Execute("""evil.exec = () => ({ 0: "1234567", length: 1, index: 0 });""");
        _engine.Evaluate("""'abc'.replace(evil, "$'")""").AsString().Should().Be("");

        _engine.Execute("""evil.exec = () => ({ 0: "x", length: 1, index: 3 });""");
        _engine.Evaluate("""'abc'.replace(evil, "$'")""").AsString().Should().Be("abc");

        _engine.Execute("""evil.exec = () => ({ 0: "x", length: 1, index: 2 });""");
        _engine.Evaluate("""'abc'.replace(evil, "$'")""").AsString().Should().Be("ab");
        _engine.Evaluate("""'abcd'.replace(evil, "$'")""").AsString().Should().Be("abdd");
        _engine.Evaluate("""'abcde'.replace(evil, "$'")""").AsString().Should().Be("abdede");
    }

    [Test]
    public void SubstitutionClampsAMatchIndexTheExecLiedAbout()
    {
        // @@replace step 14.e (https://tc39.es/ecma262/#sec-regexp.prototype-@@replace) clamps
        // ToIntegerOrInfinity(result.index) between 0 and the length of the string, which is what
        // establishes GetSubstitution's "position <= stringLength" assertion. The clamp has to happen
        // before the value is narrowed to an index: an out-of-range double-to-int conversion saturates
        // on .NET but is unspecified on .NET Framework, where it yields int.MinValue and would turn a
        // far-right index into 0.
        _engine.Execute("var evil = new RegExp();");

        _engine.Execute("""evil.exec = () => ({ 0: "x", length: 1, index: 1e300 });""");
        _engine.Evaluate("""'abc'.replace(evil, "[$`]")""").AsString().Should().Be("abc[abc]");
        _engine.Evaluate("""'abc'.replace(evil, "[$']")""").AsString().Should().Be("abc[]");

        _engine.Execute("""evil.exec = () => ({ 0: "x", length: 1, index: -5 });""");
        _engine.Evaluate("""'abc'.replace(evil, "[$`]")""").AsString().Should().Be("[]bc");
        _engine.Evaluate("""'abc'.replace(evil, "[$']")""").AsString().Should().Be("[bc]bc");

        _engine.Execute("""evil.exec = () => ({ 0: "x", length: 1, index: NaN });""");
        _engine.Evaluate("""'abc'.replace(evil, "[$`|$']")""").AsString().Should().Be("[|bc]bc");
    }

    [Test]
    public void ReplaceAllRunsTheSameSubstitutionClamps()
    {
        // replaceAll reaches GetSubstitution through the very same @@replace, so a lying exec has to be
        // clamped identically there. The regexp must be global for replaceAll to accept it, and its exec
        // answers once and then reports exhaustion so the accumulation loop terminates.
        _engine.Execute("""
            function lying(match, index) {
                var re = /x/g;
                var served = false;
                re.exec = function () {
                    if (served) { return null; }
                    served = true;
                    return { 0: match, length: 1, index: index };
                };
                return re;
            }
            """);

        _engine.Evaluate("""'abc'.replaceAll(lying('1234567', 0), "$'")""").AsString().Should().Be("");
        _engine.Evaluate("""'abc'.replaceAll(lying('x', 3), "[$']")""").AsString().Should().Be("abc[]");
        _engine.Evaluate("""'abc'.replaceAll(lying('x', 1e300), "[$`]")""").AsString().Should().Be("abc[abc]");
        _engine.Evaluate("""'abc'.replaceAll(lying('x', -5), "[$`|$']")""").AsString().Should().Be("[|bc]bc");
    }

    [Test]
    public void SubstitutionOfAWellBehavedMatchIsUnchanged()
    {
        // The control: an honest exec, so position and matched agree with the string and no clamp bites.
        _engine.Evaluate("""'abcde'.replace(/c/, "[$`|$&|$']")""").AsString().Should().Be("ab[ab|c|de]de");
        _engine.Evaluate("""'abcde'.replace(/a/, "[$`|$&|$']")""").AsString().Should().Be("[|a|bcde]bcde");
        _engine.Evaluate("""'abcde'.replace(/e/, "[$`|$&|$']")""").AsString().Should().Be("abcd[abcd|e|]");
        _engine.Evaluate("""'abcde'.replaceAll(/[bd]/g, "[$`|$&|$']")""").AsString()
            .Should().Be("a[a|b|cde]c[abc|d|e]e");

        // $n capture references keep resolving against the captures array, and an out-of-range one is
        // still emitted literally.
        _engine.Evaluate("""'2026-08-11'.replace(/(\d+)-(\d+)-(\d+)/, "$3.$2.$1")""").AsString()
            .Should().Be("11.08.2026");
        _engine.Evaluate("""'abc'.replace(/b/, "$1$&")""").AsString().Should().Be("a$1bc");
    }

    public static TestCases<string, string> GetLithuaniaTestsData()
    {
        return new StringTetsLithuaniaData().TestData();
    }

    /// <summary>
    /// Lithuanian case is special and Test262 suite tests cover only correct parsing by character. See:
    /// https://github.com/tc39/test262/blob/main/test/intl402/String/prototype/toLocaleUpperCase/special_casing_Lithuanian.js
    /// Added logic in the engine needs to parse full strings and not only spare characters. This is what these tests cover.
    /// </summary>
    [TestCaseSource(nameof(GetLithuaniaTestsData))]
    public void LithuanianToLocaleUpperCase(string parseStr, string result)
    {
        var value = _engine.Evaluate($"('{parseStr}').toLocaleUpperCase('lt')").AsString();
        value.Should().Be(result);
    }
}
