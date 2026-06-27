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
