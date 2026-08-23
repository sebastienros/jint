#nullable enable

using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>GetIterator</c> reads <c>@@iterator</c> once, and the value it read is what decides how the iteration
/// runs. Jint's string fast lane used to ask a second time: it consulted
/// <c>StringPrototype.HasOriginalIterator</c> — a read of its own — to decide whether to iterate the text
/// directly, and then let the general path read again. An accessor on
/// <c>String.prototype[Symbol.iterator]</c> therefore observed two gets for one <c>Array.from("hello")</c>.
/// Every expectation here was read off node 24.
/// </summary>
public class StringIterationTests
{
    /// <summary>
    /// Installs a counting accessor over <paramref name="prototype"/>'s <c>@@iterator</c>, runs
    /// <paramref name="consume"/> as a function body over <paramref name="source"/>, and reports how many
    /// times the accessor ran, what <c>this</c> each call saw, and what the iteration produced.
    /// </summary>
    private static string Probe(string prototype, string source, string consume)
    {
        return new Engine().Evaluate($$"""
            var reads = [];
            var original = {{prototype}}[Symbol.iterator];
            Object.defineProperty({{prototype}}, Symbol.iterator, {
                configurable: true,
                get: function () { 'use strict'; reads.push(typeof this); return original; }
            });
            var result = JSON.stringify((function (v) { {{consume}} })({{source}}));
            'reads=' + reads.length + ' this=' + reads.join(',') + ' result=' + result;
            """).AsString();
    }

    private const string SpreadBody = "return [...v];";
    private const string ArrayFromBody = "return Array.from(v);";
    private const string ForOfBody = "var r = []; for (var c of v) r.push(c); return r;";

    /// <summary>
    /// One read, and its receiver is the primitive — <c>GetV(V, @@iterator)</c> resolves on the wrapper with
    /// V itself as the receiver, and the wrapper carries no own <c>@@iterator</c>, so the lane never has to
    /// build one.
    /// </summary>
    [Theory]
    [InlineData(ArrayFromBody)]
    [InlineData(SpreadBody)]
    [InlineData(ForOfBody)]
    public void IteratingAPrimitiveStringReadsTheIteratorOnce(string consume)
    {
        Probe("String.prototype", "'hi'", consume)
            .Should().Be("""reads=1 this=string result=["h","i"]""");
    }

    /// <summary>
    /// Array destructuring reaches the same single read with the same receiver. It used to see a wrapper:
    /// <c>HandleArrayPattern</c> performs <c>ToObject</c> for its array fast path and for the null/undefined
    /// throw, and then asked <em>that</em> for an iterator, where ArrayBindingPattern's step is
    /// <c>GetIterator(value, sync)</c> over the value itself
    /// (https://tc39.es/ecma262/#sec-runtime-semantics-bindinginitialization). node reports <c>this</c> as
    /// "string", and so does Jint now — which also means the string lane is entered here as it is everywhere else.
    /// </summary>
    [Fact]
    public void DestructuringAPrimitiveStringReadsTheIteratorOnce()
    {
        Probe("String.prototype", "'hi'", "var [a, b] = v; return [a, b];")
            .Should().Be("""reads=1 this=string result=["h","i"]""");
    }

    [Theory]
    [InlineData(ArrayFromBody)]
    [InlineData(SpreadBody)]
    [InlineData(ForOfBody)]
    public void IteratingABoxedStringReadsTheIteratorOnce(string consume)
    {
        Probe("String.prototype", "new String('hi')", consume)
            .Should().Be("""reads=1 this=object result=["h","i"]""");
    }

    /// <summary>
    /// A receiver with no fast lane behind it was already reading once; it must stay that way.
    /// </summary>
    [Theory]
    [InlineData(ArrayFromBody)]
    [InlineData(SpreadBody)]
    [InlineData(ForOfBody)]
    public void IteratingANumberReadsTheIteratorOnce(string consume)
    {
        new Engine().Evaluate($$"""
            var reads = 0;
            Object.defineProperty(Number.prototype, Symbol.iterator, {
                configurable: true,
                get: function () { reads++; return function () { return [1, 2][Symbol.iterator](); }; }
            });
            var result = JSON.stringify((function (v) { {{consume}} })(5));
            'reads=' + reads + ' result=' + result;
            """).AsString().Should().Be("reads=1 result=[1,2]");
    }

    /// <summary>
    /// The whole point of resolving the method before deciding: an untouched <c>String.prototype</c> still
    /// takes the lane that walks the text directly instead of constructing a %StringIteratorPrototype% and
    /// driving it one <c>next()</c> call per code point. Both entry shapes are covered — the one that has to
    /// resolve <c>@@iterator</c> itself (spread, for-of) and the one handed an already-resolved method
    /// (<c>Array.from</c>, <c>Iterator.from</c>), which reaches the lane by identity and reads nothing at all.
    /// </summary>
    [Fact]
    public void AnUntouchedStringPrototypeKeepsTheDirectIterationLane()
    {
        var engine = new Engine();
        var realm = engine.Realm;
        JsValue value = new JsString("hi");

        value.TryGetIterator(realm, out var resolved).Should().BeTrue();
        resolved.Should().BeOfType<IteratorInstance.StringIterator>();

        var method = JsValue.GetMethod(realm, value, GlobalSymbolRegistry.Iterator);
        value.TryGetIterator(realm, out var fromMethod, method: method).Should().BeTrue();
        fromMethod.Should().BeOfType<IteratorInstance.StringIterator>();
    }

    /// <summary>
    /// And the converse: once <c>@@iterator</c> is something else, the lane must not engage — a fast path that
    /// cannot be turned off is a correctness bug rather than an optimization.
    /// </summary>
    [Fact]
    public void AReplacedStringIteratorLeavesTheDirectLane()
    {
        var engine = new Engine();
        engine.Execute("""
            Object.defineProperty(String.prototype, Symbol.iterator, {
                configurable: true, writable: true,
                value: function () { return ['X', 'Y'][Symbol.iterator](); }
            });
            """);

        JsValue value = new JsString("hi");
        value.TryGetIterator(engine.Realm, out var resolved).Should().BeTrue();
        resolved.Should().NotBeOfType<IteratorInstance.StringIterator>();

        engine.Evaluate("JSON.stringify([...'hi'])").AsString().Should().Be("""["X","Y"]""");
        engine.Evaluate("JSON.stringify(Array.from('hi'))").AsString().Should().Be("""["X","Y"]""");
    }

    [Fact]
    public void ANonCallableStringIteratorIsATypeError()
    {
        var engine = new Engine();
        engine.Execute("String.prototype[Symbol.iterator] = 42;");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("[...'hi']"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Array.from('hi')"));
    }

    /// <summary>
    /// With <c>@@iterator</c> gone a string is not iterable, but <c>Array.from</c> still has the array-like
    /// fallback to fall back on.
    /// </summary>
    [Fact]
    public void ADeletedStringIteratorMakesTheStringNonIterable()
    {
        var engine = new Engine();
        engine.Execute("delete String.prototype[Symbol.iterator];");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("[...'hi']"));
        engine.Evaluate("JSON.stringify(Array.from('hi'))").AsString().Should().Be("""["h","i"]""");
    }

    /// <summary>
    /// The direct lane iterates by code point, not by UTF-16 code unit.
    /// </summary>
    [Theory]
    [InlineData(ArrayFromBody)]
    [InlineData(SpreadBody)]
    [InlineData(ForOfBody)]
    public void TheDirectLaneIteratesBySurrogatePair(string consume)
    {
        new Engine().Evaluate($$"""JSON.stringify((function (v) { {{consume}} })('a\u{1F600}b'))""")
            .AsString().Should().Be("""["a","😀","b"]""");
    }

    /// <summary>
    /// The direct lane belongs to the sync hint alone. It used to be taken whatever the hint was, so
    /// <c>for await</c> over a string ignored an <c>@@asyncIterator</c> installed on
    /// <c>String.prototype</c> and iterated the text instead; the async lookup order now reaches the base
    /// implementation, which asks for <c>@@asyncIterator</c> first as
    /// https://tc39.es/ecma262/#sec-getiterator prescribes.
    /// </summary>
    [Fact]
    public void ForAwaitOverAStringHonoursAnAsyncIterator()
    {
        var engine = new Engine();
        engine.Execute("""
            var out = null;
            Object.defineProperty(String.prototype, Symbol.asyncIterator, {
                configurable: true,
                value: function () { return ['A', 'B'][Symbol.iterator](); }
            });
            (async function () {
                var r = [];
                for await (var c of 'hi') r.push(c);
                out = JSON.stringify(r);
            })();
            """);

        engine.Evaluate("out").AsString().Should().Be("""["A","B"]""");
    }

    /// <summary>
    /// Without one, <c>for await</c> still walks the string, through the sync-to-async adapter.
    /// </summary>
    [Fact]
    public void ForAwaitOverAStringFallsBackToTheSyncIterator()
    {
        var engine = new Engine();
        engine.Execute("""
            var out = null;
            (async function () {
                var r = [];
                for await (var c of 'hi') r.push(c);
                out = JSON.stringify(r);
            })();
            """);

        engine.Evaluate("out").AsString().Should().Be("""["h","i"]""");
    }

    /// <summary>
    /// <c>String.prototype</c> itself is a String object holding the empty string, and iterating it produces
    /// nothing whichever lane is chosen. It is worth pinning because it used to be the one object besides a
    /// primitive string whose <c>HasOriginalIterator</c> answered true, which routed array destructuring of it
    /// through the array-like shortcut rather than through an iterator.
    /// </summary>
    [Fact]
    public void StringPrototypeIteratesAsTheEmptyString()
    {
        var engine = new Engine();
        engine.Evaluate("JSON.stringify([...String.prototype])").AsString().Should().Be("[]");
        engine.Evaluate("JSON.stringify(Array.from(String.prototype))").AsString().Should().Be("[]");
        engine.Evaluate("(function () { var [a] = String.prototype; return a === undefined; })()")
            .AsBoolean().Should().BeTrue();
    }
}
