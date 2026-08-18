namespace Jint.Tests.Runtime;

/// <summary>
/// A ComputedPropertyName is <c>ToPropertyKey(? GetValue(? Evaluation(AssignmentExpression)))</c>
/// (https://tc39.es/ecma262/#sec-runtime-semantics-propertydefinitionevaluation), so a computed key
/// whose expression merely happens to be a literal node still has to go through that conversion. The
/// key-extraction helper used to match <c>Literal</c> before it consulted the computed flag, which made
/// <c>{ [/a/]: 0 }</c> and <c>{ [true]: 1 }</c> bind CLR-formatted names (<c>"a"</c>, <c>"True"</c>)
/// and never call a user-visible <c>toString</c>.
/// </summary>
public class ComputedPropertyKeyTests
{
    [Fact]
    public void RegExpLiteralComputedKeyUsesRegExpToString()
    {
        var engine = new Engine();
        engine.Evaluate("JSON.stringify(Object.keys({ [/a/]: 0 }))").AsString().Should().Be("[\"/a/\"]");
        engine.Evaluate("({ [/a/]: 0 })[/a/]").AsNumber().Should().Be(0);
        engine.Evaluate("JSON.stringify(Object.keys({ [/a/gi]: 0 }))").AsString().Should().Be("[\"/a/gi\"]");
    }

    [Fact]
    public void RegExpLiteralComputedKeyHonoursOverriddenToString()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
RegExp.prototype.toString = function () { return 'overridden'; };
JSON.stringify(Object.keys({ [/a/]: 0 }));").AsString();

        result.Should().Be("[\"overridden\"]");
    }

    [Fact]
    public void ComputedKeyConversionIsObservableAndCanThrow()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
RegExp.prototype.toString = function () { throw 42; };
var thrown;
try { ({ [/regex/]: 0 }); } catch (e) { thrown = e; }
thrown;").AsNumber();

        result.Should().Be(42);
    }

    [Fact]
    public void BooleanLiteralComputedKeyUsesJavaScriptSpelling()
    {
        var engine = new Engine();
        engine.Evaluate("JSON.stringify(Object.keys({ [true]: 1, [false]: 2 }))").AsString().Should().Be("[\"true\",\"false\"]");
        engine.Evaluate("({ [true]: 1 })['true']").AsNumber().Should().Be(1);
        engine.Evaluate("({ [true]: 1 })[true]").AsNumber().Should().Be(1);
    }

    [Fact]
    public void NullLiteralComputedKeyIsNullString()
    {
        var engine = new Engine();
        engine.Evaluate("JSON.stringify(Object.keys({ [null]: 1 }))").AsString().Should().Be("[\"null\"]");
        engine.Evaluate("({ [null]: 1 })['null']").AsNumber().Should().Be(1);
    }

    [Fact]
    public void NonComputedKeywordKeysAreUnaffected()
    {
        var engine = new Engine();
        engine.Evaluate("({ true: 1 }).true").AsNumber().Should().Be(1);
        engine.Evaluate("JSON.stringify(Object.keys({ true: 1, false: 2, null: 3 }))").AsString().Should().Be("[\"true\",\"false\",\"null\"]");
        engine.Evaluate("({ true: 1 })['true']").AsNumber().Should().Be(1);
    }

    [Fact]
    public void StringAndNumberComputedKeysAreUnchanged()
    {
        var engine = new Engine();
        engine.Evaluate("JSON.stringify(Object.keys({ ['foo']: 1 }))").AsString().Should().Be("[\"foo\"]");
        engine.Evaluate("({ ['foo']: 1 }).foo").AsNumber().Should().Be(1);
        engine.Evaluate("JSON.stringify(Object.keys({ [0]: 1 }))").AsString().Should().Be("[\"0\"]");
        engine.Evaluate("({ [0]: 1 })[0]").AsNumber().Should().Be(1);
        engine.Evaluate("JSON.stringify(Object.keys({ [1e21]: 1 }))").AsString().Should().Be("[\"1e+21\"]");
        engine.Evaluate("JSON.stringify(Object.keys({ [1n]: 1 }))").AsString().Should().Be("[\"1\"]");
        engine.Evaluate("JSON.stringify(Object.keys({ foo: 1, 0: 2, 'bar': 3 }))").AsString().Should().Be("[\"0\",\"foo\",\"bar\"]");
    }

    [Fact]
    public void ClassMethodComputedRegExpKey()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
class C { [/re/]() { return 7; } }
JSON.stringify(Object.getOwnPropertyNames(C.prototype)) + '|' + new C()[/re/]() + '|' + C.prototype[/re/].name;").AsString();

        result.Should().Be("[\"constructor\",\"/re/\"]|7|/re/");
    }

    [Fact]
    public void ClassMethodComputedBooleanKey()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
class C { [true]() { return 8; } }
JSON.stringify(Object.getOwnPropertyNames(C.prototype)) + '|' + new C()[true]() + '|' + C.prototype[true].name;").AsString();

        result.Should().Be("[\"constructor\",\"true\"]|8|true");
    }

    [Fact]
    public void ClassAccessorsAndFieldsUseConvertedComputedKeys()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
class C {
    [/f/] = 1;
    get [/g/]() { return 2; }
    static [/s/]() { return 3; }
}
var c = new C();
c[/f/] + '|' + c[/g/] + '|' + C[/s/]() + '|' + Object.getOwnPropertyDescriptor(C.prototype, '/g/').get.name;").AsString();

        result.Should().Be("1|2|3|get /g/");
    }

    [Fact]
    public void AnonymousFunctionNamedFromComputedRegExpKey()
    {
        var engine = new Engine();
        engine.Evaluate("({ [/a/]: function () {} })[/a/].name").AsString().Should().Be("/a/");
        engine.Evaluate("({ [/a/]() {} })[/a/].name").AsString().Should().Be("/a/");
    }

    [Fact]
    public void ComputedKeysInDestructuringPatternsAreConverted()
    {
        var engine = new Engine();
        engine.Evaluate("(function ({ [/a/]: x }) { return x; })({ '/a/': 5 })").AsNumber().Should().Be(5);
        engine.Evaluate("(function ({ [true]: x }) { return x; })({ 'true': 6 })").AsNumber().Should().Be(6);

        var destructured = engine.Evaluate("var y; ({ [/a/]: y } = { '/a/': 9 }); y;").AsNumber();
        destructured.Should().Be(9);
    }
}
